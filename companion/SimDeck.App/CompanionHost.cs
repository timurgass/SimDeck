using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Makaretu.Dns;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SimDeck.Core;

namespace SimDeck.App;

public sealed class CompanionHost : IAsyncDisposable
{
    public SettingsStore Store { get; }
    public TelemetryHub Telemetry { get; } = new();
    readonly Ets2MapService ets2Map;
    readonly Ets2MapService atsMap;
    public Ets2MapService Ets2Map => Profile.Id == "ats" ? atsMap : ets2Map;
    public PairingGate Pairing { get; } = new();
    public WindowsInput Backend { get; } = new();
    public InputEngine Input { get; }
    readonly IInputBackend inputBackend;
    public string Fingerprint { get; private set; } = "";
    public string Status { get; private set; } = "Запуск…";
    public string LastCommand { get; private set; } = "Команд пока нет";
    public string Device { get; private set; } = "Не подключено";
    public string? ActiveTransport { get; private set; }
    public int InvalidPackets;
    public int ReceivedPackets;
    public BrowserHost? Browser { get; private set; }
    public async Task StartBrowserAsync(int port = 8787, bool localOnly = false)
    {
        if (Browser is not null) return;
        var browser = new BrowserHost(this);
        try { await browser.StartAsync(port, localOnly); Browser = browser; }
        catch { await browser.DisposeAsync(); throw; }
    }
    public GameProfile Profile => Store.Value.ActiveProfile;
    bool IsF1 => Profile.Id is "f1-24" or "f1-25";
    bool IsBeamNg => Profile.Id == "beamng-default";
    bool IsAcc => Profile.Id == "acc";
    bool IsEts2 => Profile.Id is "ets2" or "ats";
    bool IsFs25 => Profile.Id == "fs25";
    string GameId => IsBeamNg ? "beamng" : Profile.Id;
    public string TelemetryDiagnostic => IsF1
        ? f1Udp is null ? "UDP 20777 занят. Закройте другую программу телеметрии на этом порту."
        : ReceivedPackets == 0 ? $"Нет UDP-пакетов F1. Проверьте 127.0.0.1:20777, 60 Hz, формат {(Profile.Id == "f1-25" ? "2025 или 2024" : "2024")} и выйдите на трассу. Получено: 0."
        : InvalidPackets >= ReceivedPackets ? $"Получено UDP-пакетов: {ReceivedPackets}, но все отклонены. Проверьте профиль игры и формат UDP."
        : $"Нет свежих данных. Получено: {ReceivedPackets}, отклонено: {InvalidPackets}. Вернитесь на трассу."
        : IsAcc ? accDiagnostic
        : IsEts2 ? scsDiagnostic
        : IsFs25 ? fs25Diagnostic
        : !IsBeamNg ? "Профиль управления готов. Телеметрия для этой игры пока не подключена."
        : udp is null ? "UDP-порт занят: закройте другой экземпляр Companion."
        : ReceivedPackets == 0 ? "Нет пакетов от игры. Установите мод SimDeck и перезагрузите машину (Ctrl+R)."
        : $"UDP {Store.Value.UdpPort}: принято {ReceivedPackets}, отклонено {InvalidPackets}";
    long lastExtendedPacket;
    public volatile bool Demo;
    readonly CancellationTokenSource stop = new();
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly SemaphoreSlim controller = new(1, 1);
    readonly object controllerGate = new();
    CancellationTokenSource? clientStop;
    string? activeControllerKey;
    WebApplication? app;
    UdpClient? udp;
    UdpClient? f1Udp;
    F1TelemetryParser f1Parser = new();
    readonly AccSharedMemoryReader accReader = new();
    readonly ScsSharedMemoryReader scsReader = new();
    ulong lastScsTimestamp;
    long lastScsPacketAt;
    string scsDiagnostic = "Ожидание SCS Telemetry. Установите плагин и перезапустите выбранную игру ETS2/ATS.";
    string fs25Diagnostic = "Ожидание сохранения Farming Simulator 25.";
    Fs25SaveWatcher? fs25Watcher;
    string? fs25SavePath;
    Fs25CropCatalogView? fs25Crops;
    Fs25Plan? fs25Plan;
    readonly Func<Fs25SavegameDir, DateTime, Fs25Details>? readFs25Save;
    int lastAccPacket = int.MinValue;
    long lastAccPacketAt;
    long lastAccReconnect;
    string accDiagnostic = "Ожидание ACC Shared Memory. Запустите заезд и выйдите на трассу.";
    readonly object profileGate = new();
    ServiceDiscovery? discovery;
    X509Certificate2? certificate;
    Task[] loops = [];

    public CompanionHost(string dataDirectory, IInputBackend? inputBackend = null,
        Func<Fs25SavegameDir, DateTime, Fs25Details>? readFs25Save = null)
    {
        Store = new(dataDirectory);
        ets2Map = new(dataDirectory);
        atsMap = new(dataDirectory, "ats");
        this.inputBackend = inputBackend ?? Backend;
        this.readFs25Save = readFs25Save;
        Input = new(this.inputBackend);
        Backend.UseVirtualKey = Store.Value.UseVirtualKeyInput;
        Backend.Enabled = Store.Value.KeyboardInputEnabled;
        if (!string.IsNullOrWhiteSpace(Store.Value.Fs25PlanPath))
            try { fs25Plan = Fs25PlanLoader.LoadFile(Store.Value.Fs25PlanPath); }
            catch (Exception ex) when (ex is Fs25PlanException or UnauthorizedAccessException)
            { fs25Diagnostic = "План FS25 не прочитан: " + ex.Message; }
        ApplyBindings();
    }
    public string LoadFs25Plan(string path)
    {
        var loaded = Fs25PlanLoader.LoadFile(path);
        lock (profileGate)
        {
            fs25Plan = loaded;
            Store.Value.Fs25PlanPath = path;
            Store.Save();
        }
        return loaded.Name;
    }
    public void ApplyBindings()
    {
        var bindings = Profile.Actions.Select(x =>
        {
            var binding = WindowsInput.ParseBinding(x.Id, x.Key, x.Gesture);
            if (Profile.Id == "acc" && x.Id == "accIgnitionOff")
                binding = binding with
                {
                    Sequence = [WindowsInput.ParseKey("F2"),
                        .. Enumerable.Repeat(WindowsInput.ParseKey("Down"), 12), WindowsInput.ParseKey("Left")]
                };
            return binding;
        }).ToArray();
        Input.Configure(bindings);
        Backend.TargetProcess = Profile.TargetProcess;
    }
    public void SaveProfile(GameProfile profile)
    {
        GameProfiles.Validate(profile);
        lock (profileGate)
        {
            Backend.Enabled = false; Input.ReleaseAll(); clientStop?.Cancel();
            var index = Store.Value.Profiles.FindIndex(p => p.Id == profile.Id);
            if (index < 0) throw new ArgumentException("Профиль не найден.");
            Store.Value.Profiles[index] = profile with { Actions = profile.Actions.ToList(), Revision = checked(Store.Value.Profiles[index].Revision + 1) };
            Store.Save(); ApplyBindings(); Backend.Enabled = Store.Value.KeyboardInputEnabled;
        }
    }
    public void SelectProfile(string id)
    {
        lock (profileGate)
        {
            if (Profile.Id == id) return;
            if (!Store.Value.Profiles.Any(p => p.Id == id)) throw new ArgumentException("Профиль не найден.");
            Backend.Enabled = false; Input.ReleaseAll(); clientStop?.Cancel();
            Store.Value.ActiveProfileId = id; ApplyBindings(); Store.Save();
            f1Parser = new(); lastExtendedPacket = 0; ReceivedPackets = InvalidPackets = 0;
            accReader.Reset(); lastAccPacket = int.MinValue; lastAccPacketAt = lastAccReconnect = 0;
            accDiagnostic = "Ожидание ACC Shared Memory. Запустите заезд и выйдите на трассу.";
            scsReader.Reset(); lastScsTimestamp = 0; lastScsPacketAt = 0;
            scsDiagnostic = "Ожидание SCS Telemetry. Установите плагин и перезапустите выбранную игру ETS2/ATS.";
            fs25Watcher = null; fs25SavePath = null; fs25Crops = null;
            fs25Diagnostic = "Ожидание сохранения Farming Simulator 25.";
            Telemetry.Reset(Demo ? "demo" : GameId);
            Backend.Enabled = Store.Value.KeyboardInputEnabled;
        }
    }
    public void SetDemo(bool value)
    {
        Input.ReleaseAll();
        Demo = value; Backend.Demo = value;
        Telemetry.Reset(value ? "demo" : GameId);
    }
    public void SetCompatibleInput(bool value)
    {
        Input.ReleaseAll();
        Backend.UseVirtualKey = value;
        Store.Value.UseVirtualKeyInput = value;
        Store.Save();
    }
    public void RevokeDevices() { Store.RevokeAll(); Browser?.Revoke(); CancelActiveController(); Input.ReleaseAll(); }
    void CancelActiveController()
    {
        lock (controllerGate)
            try { clientStop?.Cancel(); } catch (ObjectDisposedException) { }
    }
    internal async Task DisconnectControllerAsync(CancellationToken cancellationToken)
    {
        CancelActiveController();
        Input.ReleaseAll();
        // A newly paired browser must not lose the controller race to an old tab.
        // Wait until ServeController has released the single-controller gate.
        if (await controller.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken)) controller.Release();
    }
    bool readGameMemory = true;
    public async Task StartAsync(X509Certificate2? serverCertificate = null, bool localOnly = false, bool pollGameMemory = true)
    {
        readGameMemory = pollGameMemory;
        certificate = serverCertificate ?? Store.Certificate();
        Fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData)).ToLowerInvariant();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 16384;
            if (localOnly) o.Listen(IPAddress.Loopback, Store.Value.Port, l => l.UseHttps(certificate));
            else o.ListenAnyIP(Store.Value.Port, l => l.UseHttps(certificate)); });
        app = builder.Build();
        app.UseResponseCompression();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(10) });
        app.MapGet("/health", () => Results.Json(new { name = "SimDeck", protocolMajor = 1 }));
        app.MapGet("/ets2-map", (HttpContext context) =>
        {
            var auth=context.Request.Headers.Authorization.ToString();
            if(!auth.StartsWith("Bearer ",StringComparison.Ordinal) || !Store.IsTrusted(auth[7..])) return Results.StatusCode(401);
            return Ets2Map.Response(context);
        });
        bool NavigationAuthorized(HttpContext c) {var a=c.Request.Headers.Authorization.ToString();return Profile.Id is "ets2" or "ats" && a.StartsWith("Bearer ",StringComparison.Ordinal)&&Store.IsTrusted(a[7..]);}
        app.MapGet("/truck-nav/places",(HttpContext c)=>NavigationAuthorized(c)?Ets2Map.PlacesResponse(c):Results.StatusCode(401));
        app.MapGet("/truck-nav/landscape",(HttpContext c)=>NavigationAuthorized(c)?Ets2Map.LandscapeResponse(c):Results.StatusCode(401));
        app.MapGet("/truck-nav/game-route",async(HttpContext c)=>NavigationAuthorized(c)?await Ets2Map.GameGpsResponse(c):Results.StatusCode(401));
        app.MapPost("/truck-nav/route",async(HttpContext c)=>NavigationAuthorized(c)?await Ets2Map.RouteResponse(c):Results.StatusCode(401));
        app.MapPost("/truck-nav/game-waypoint/delete",async(HttpContext c)=>NavigationAuthorized(c)?await DeleteGameWaypoint(c,()=>NavigationAuthorized(c)):Results.StatusCode(401));
        app.MapPost("/pair", async (HttpContext context) =>
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                var root = doc.RootElement;
                var code = root.GetProperty("code").GetString() ?? "";
                var name = root.GetProperty("name").GetString() ?? "Android";
                if (name.Length is < 1 or > 80 || !Pairing.Consume(code)) return Results.Json(new { error = "pairing_denied" }, statusCode: 403);
                var token = PairingGate.NewToken();
                Store.Trust(name, token);
                return Results.Json(new { token, protocolMajor = 1 });
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            { return Results.BadRequest(new { error = "invalid_request" }); }
        });
        app.Map("/ws", HandleSocket);
        await app.StartAsync(stop.Token);
        Status = "WSS готов · порт " + Store.Value.Port;
        try
        {
            udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, Store.Value.UdpPort));
        }
        catch (SocketException) { Status += " · UDP-порт занят"; }
        try { f1Udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 20777)); }
        catch (SocketException) { Status += " · F1 UDP 20777 занят"; }
        Telemetry.Reset(GameId);
        try
        {
            if (localOnly) { loops = [Task.Run(ReceiveTelemetry), Task.Run(ReceiveF1), Task.Run(RunTick), Task.Run(MonitorFs25), Task.Run(MonitorFs25Live)]; return; }
            discovery = new ServiceDiscovery();
            var service = new ServiceProfile("SimDeck-" + Environment.MachineName, "_simdeck._tcp", (ushort)Store.Value.Port);
            service.AddProperty("fingerprint", Fingerprint);
            service.AddProperty("version", "1");
            discovery.Advertise(service);
        }
        catch (Exception ex) { Status += " · автопоиск: " + ex.GetType().Name; }
        loops = [Task.Run(ReceiveTelemetry), Task.Run(ReceiveF1), Task.Run(RunTick), Task.Run(MonitorFs25), Task.Run(MonitorFs25Live)];
    }
    // Live polling must never wait for a large save, its retry ladder, or the profile lock.
    async Task MonitorFs25Live()
    {
        string? userDataDir=null;
        DateTime? lastWritten=null;
        DateTime? lastPricesWritten=null;
        long nextPricesPoll=0;
        try {
            while(!stop.IsCancellationRequested) {
                bool active;lock(profileGate) active=IsFs25 && !Demo;
                if(active) try {
                    userDataDir ??= Fs25GamePaths.FindUserDataDir();
                    var live=userDataDir is null?null:Fs25LiveReader.ReadSnapshot(userDataDir,DateTime.UtcNow,lastWritten);
                    if(live is not null) lock(profileGate) {
                        if(IsFs25 && !Demo) { Telemetry.PublishFs25Live(live.States,live.Vehicle,live.AgeMs);lastWritten=live.WrittenAtUtc; }
                    }
                    if(userDataDir is not null && Environment.TickCount64>=nextPricesPoll) {
                        nextPricesPoll=Environment.TickCount64+1000;
                        var prices=Fs25PricesReader.Read(userDataDir,DateTime.UtcNow,lastPricesWritten);
                        if(prices is not null) lock(profileGate) {
                            if(IsFs25 && !Demo) { Telemetry.PublishFs25Prices(prices.Data);lastPricesWritten=prices.WrittenAtUtc; }
                        }
                    }
                } catch(Exception ex) { fs25Diagnostic="FS25: ошибка чтения живого состояния ("+ex.GetType().Name+")."; }
                if(!active) { lastWritten=null;lastPricesWritten=null;nextPricesPoll=0; }
                await Task.Delay(active?50:250,stop.Token);
            }
        } catch(OperationCanceledException) { }
    }
    async Task MonitorFs25()
    {
        try
        {
            var nextSavePoll = 0L;
            while (!stop.IsCancellationRequested)
            {
                bool active; lock (profileGate) active = IsFs25 && !Demo;
                if (active)
                {
                        try
                        {
                            if (Environment.TickCount64 >= nextSavePoll)
                            {
                                nextSavePoll = Environment.TickCount64 + 1000;
                                var save = Fs25GamePaths.FindSavegames().FirstOrDefault();
                                if (save is null) fs25Diagnostic = "Сохранение FS25 не найдено. Сохраните игру и проверьте папку My Games/FarmingSimulator2025.";
                                else
                                {
                                    // Discover/read files outside profileGate; only publish to the still-active profile.
                                    Fs25SaveWatcher? watcher;lock(profileGate) watcher=fs25SavePath==save.Path?fs25Watcher:null;
                                    if (watcher is null)
                                    {
                                        var crops=LoadFs25Crops();
                                        watcher = new Fs25SaveWatcher(Telemetry, save,
                                            plan: () => fs25Plan, catalog: () => crops, read: readFs25Save,
                                            publish: data => { lock(profileGate) { if(IsFs25 && !Demo) Telemetry.Publish(data); } });
                                        lock(profileGate) if(IsFs25 && !Demo) {
                                            fs25SavePath=save.Path;fs25Crops=crops;fs25Watcher=watcher;
                                        }
                                    }
                                    var poll = watcher.PollOnce();
                                    var diagnostic = poll.Error is not null
                                        ? "FS25: сохранение пока не читается (" + poll.Error.GetType().Name + "). Повторим через 5 с."
                                        : $"FS25: {save.Name} · {poll.Details?.Period.RussianMonth ?? "ожидание"} · "
                                            + (poll.IsStale ? "данные устарели" : "сохранение прочитано") + ".";
                                    lock(profileGate) if(IsFs25 && !Demo) fs25Diagnostic=diagnostic;
                                }
                            }
                        }
                        catch (Exception ex) { fs25Diagnostic = "FS25: ошибка чтения сохранения (" + ex.GetType().Name + ")."; }
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250), stop.Token);
            }
        }
        catch (OperationCanceledException) { }
    }
    static Fs25CropCatalogView? LoadFs25Crops()
    {
        var install = Fs25GamePaths.FindInstall();
        if (install is null) return null;
        try
        {
            using var index = File.OpenRead(install.FruitTypesXml);
            var files = Fs25FruitTypesParser.Parse(index).FoliageFiles;
            var dataRoot = Path.GetFullPath(install.DataDir) + Path.DirectorySeparatorChar;
            var crops = new List<Fs25Crop>();
            foreach (var relative in files)
            {
                var path = Path.GetFullPath(Path.Combine(install.DataDir, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) continue;
                try { using var stream = File.OpenRead(path); crops.Add(Fs25CropParser.Parse(stream)); }
                catch (Exception ex) when (ex is IOException or System.Xml.XmlException or InvalidDataException) { }
            }
            return crops.Count > 0 ? new Fs25CropListCatalogView(crops) : null;
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException or UnauthorizedAccessException) { return null; }
    }
    async Task ReceiveTelemetry()
    {
        if (udp is null) return;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var packet = await udp.ReceiveAsync(stop.Token);
                lock (profileGate) {
                if (Demo || Profile.Id != "beamng-default") continue;
                Interlocked.Increment(ref ReceivedPackets);
                if (SimDeckParser.TryParse(packet.Buffer, out var frame))
                {
                    lastExtendedPacket = Environment.TickCount64;
                    Telemetry.Publish(frame!);
                }
                else if (OutGaugeParser.TryParse(packet.Buffer, out frame))
                {
                    if (lastExtendedPacket == 0 || Environment.TickCount64 - lastExtendedPacket > 500) Telemetry.Publish(frame!);
                }
                else Interlocked.Increment(ref InvalidPackets);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { if (!stop.IsCancellationRequested) Status = "Ошибка UDP. Перезапустите Companion."; }
    }
    async Task ReceiveF1()
    {
        if (f1Udp is null) return;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var packet = await f1Udp.ReceiveAsync(stop.Token);
                lock (profileGate)
                {
                    if (Demo || !IsF1) continue;
                    Interlocked.Increment(ref ReceivedPackets);
                    if (!f1Parser.TryParse(packet.Buffer, out var frame)) Interlocked.Increment(ref InvalidPackets);
                    else if (frame is not null) Telemetry.Publish(frame);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { }
        catch (SocketException) { if (!stop.IsCancellationRequested) Status = "Ошибка UDP F1. Перезапустите Companion."; }
    }
    async Task RunTick()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(stop.Token))
            {
                Input.Tick();
                if (Demo)
                {
                    var t = Environment.TickCount64 / 1000.0;
                    var wave = (Math.Sin(t * 0.5) + 1) / 2;
                    Telemetry.Publish(new(15 + 45 * wave, 1500 + 5500 * wave, 2 + (int)(wave * 4), .64, wave, 0, 0, 8000));
                }
                else
                {
                    lock (profileGate)
                    {
                        if (IsEts2 && !readGameMemory) { Ets2Map.Ensure(); continue; }
                        if (IsAcc)
                        {
                            if (accReader.TryRead(out var packet, out var frame, out var error))
                            {
                                if (packet != lastAccPacket)
                                {
                                    lastAccPacket = packet;
                                    lastAccPacketAt = Environment.TickCount64;
                                    Interlocked.Increment(ref ReceivedPackets);
                                    Telemetry.Publish(frame!);
                                    accDiagnostic = $"ACC Shared Memory: принято {ReceivedPackets} кадров.";
                                }
                                else if (lastAccPacketAt > 0 && Environment.TickCount64 - lastAccPacketAt > 3000 && Environment.TickCount64 - lastAccReconnect > 2000)
                                {
                                    lastAccReconnect = Environment.TickCount64;
                                    accReader.Reset();
                                    accDiagnostic = "ACC не обновляет данные. Вернитесь на трассу.";
                                }
                            }
                            else accDiagnostic = error;
                        }
                        else if (IsEts2)
                        {
                            Ets2Map.Ensure();
                            if (scsReader.TryRead(out var timestamp, out var frame, out var error, Profile.Id == "ats" ? 2u : 1u))
                            {
                                if (timestamp != lastScsTimestamp)
                                {
                                    lastScsTimestamp = timestamp;
                                    lastScsPacketAt = Environment.TickCount64;
                                    Interlocked.Increment(ref ReceivedPackets);
                                    Telemetry.Publish(frame!);
                                    scsDiagnostic = $"SCS Telemetry: принято {ReceivedPackets} кадров.";
                                }
                                else if (lastScsPacketAt > 0 && Environment.TickCount64 - lastScsPacketAt > 1000)
                                    scsDiagnostic = "ETS2 не обновляет телеметрию. Вернитесь в грузовик.";
                            }
                            else scsDiagnostic = error;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }
    public async Task<IResult> DeleteGameWaypoint(HttpContext context,Func<bool> authorized)
    {
        var profile=Profile.Id;var service=Ets2Map;
        var authorization=context.Request.Headers.Authorization.ToString();
        var key=authorization.StartsWith("Bearer ",StringComparison.Ordinal)?"android:"+PairingGate.Hash(authorization[7..]):
            context.Request.Cookies.TryGetValue("simdeck_browser",out var cookie)?"browser:"+PairingGate.Hash(cookie):"";
        CancellationTokenSource? owner;
        lock(controllerGate){owner=clientStop;if(owner is null||owner.IsCancellationRequested||activeControllerKey!=key)return Results.Json(new{deleted=false,message="Подключите этот пульт к Companion перед изменением GPS"});}
        using var lease=Input.TryExclusive();
        if(lease is null)return Results.Json(new{deleted=false,message="Отпустите кнопки пульта: ввод занят"});
        bool Allowed(){lock(controllerGate)if(clientStop!=owner||owner.IsCancellationRequested||activeControllerKey!=key)return false;
            lock(profileGate)return !context.RequestAborted.IsCancellationRequested&&!stop.IsCancellationRequested&&authorized()&&Profile.Id==profile&&inputBackend==Backend&&!Demo&&Backend.CanInject&&ScsSharedMemoryReader.MapEditingAllowed(profile=="ats"?2u:1u);}
        var result=await service.DeleteGameWaypoint(context,Backend,Allowed);
        LastCommand="GPS · проверка удаления точки";return result;
    }
    async Task HandleSocket(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization[7..] : "";
        if (!Store.IsTrusted(token)) { context.Response.StatusCode = 401; return; }
        await ServeController(context, "Android", "android:" + PairingGate.Hash(token));
    }
    internal async Task ServeController(HttpContext context, string device, string? controllerKey = null)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        var replacingSameController = false;
        if (controllerKey is not null)
        {
            lock (controllerGate)
            {
                replacingSameController = activeControllerKey == controllerKey && clientStop is not null;
                if (replacingSameController)
                    try { clientStop!.Cancel(); } catch (ObjectDisposedException) { }
            }
        }
        if (!await controller.WaitAsync(replacingSameController ? TimeSpan.FromSeconds(2) : TimeSpan.Zero))
        { context.Response.StatusCode = 409; return; }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, context.RequestAborted);
        lock (controllerGate) { clientStop = lifetime; activeControllerKey = controllerKey; }
        var session = Guid.NewGuid().ToString("N");
        try
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            Input.BeginSession(session);
            Device = device + " · подключено";
            ActiveTransport = device == "Android" ? context.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address) ? "usb" : "wifi" : "browser";
            var queue = Channel.CreateBounded<object>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });
            var profile = Profile;
            await queue.Writer.WriteAsync(new { protocolMajor = 1, type = "hello", sessionId = session, profileId = profile.Id, profileRevision = profile.Revision,
                profileName = profile.Name, controls = profile.Actions.ToArray(),
                capabilities = new { telemetry = new[] { "speedMps", "rpm", "gear", "gearboxMode", "maxGear", "headlights", "actionStates", "fuelFraction", "throttle", "brake", "clutch" }, actions = profile.Actions.Select(x => x.Id).ToArray(), gestures = new { ignition = "tapThenHold" } } }, lifetime.Token);
            var send = SendLoop(socket, queue.Reader, session, lifetime.Token);
            var receive = ReceiveLoop(socket, queue.Writer, session, lifetime.Token);
            await Task.WhenAny(send, receive);
            lifetime.Cancel();
            await Task.WhenAll(send, receive);
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or IOException or JsonException or InvalidOperationException or KeyNotFoundException)
        { LastCommand = "Соединение закрыто · " + ex.GetType().Name; }
        finally
        {
            Input.EndSession(session);
            lock (controllerGate)
            {
                if (ReferenceEquals(clientStop, lifetime))
                { Device = "Не подключено"; ActiveTransport = null; clientStop = null; activeControllerKey = null; }
            }
            controller.Release();
        }
    }
    async Task SendLoop(WebSocket socket, ChannelReader<object> responses, string session, CancellationToken ct)
    {
        long lastInputRevision = -1;
        long lastFs25Snapshot = 0;
        (string Stream,long Sequence) lastTelemetryRevision=default;
        string? lastAvailability = null;
        // Full race snapshots include all cars. Bound traffic to 10 Hz on tablet Wi-Fi.
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(ct))
        {
            while (responses.TryRead(out var response)) await Send(socket, response, ct);
            var inputState = Input.ControlState;
            var availability = Backend.Demo ? "demo" : !Backend.Enabled ? "disabled" : inputBackend.CanInject ? "ready" : "unfocused";
            if (inputState.Revision != lastInputRevision || availability != lastAvailability)
            {
                await Send(socket, new { protocolMajor = 1, type = "input.state", sessionId = session, ignitionReady = inputState.IgnitionReady, availability }, ct);
                lastInputRevision = inputState.Revision;
                lastAvailability = availability;
            }
            var now = Environment.TickCount64;
            var telemetryRevision=Telemetry.Revision;
            if (!IsFs25 || telemetryRevision!=lastTelemetryRevision || now-lastFs25Snapshot>=1000)
            {
                await Send(socket, Telemetry.Snapshot(session), ct);
                lastFs25Snapshot = now;
                lastTelemetryRevision=telemetryRevision;
            }
        }
    }
    static async Task Send(WebSocket socket, object value, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(8));
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value, Json), WebSocketMessageType.Text, true, deadline.Token);
    }
    async Task ReceiveLoop(WebSocket socket, ChannelWriter<object> responses, string session, CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (!ct.IsCancellationRequested)
        {
            var length = 0;
            WebSocketReceiveResult message;
            do
            {
                if (length == buffer.Length) throw new InvalidOperationException("Message too large");
                message = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), ct);
                if (message.MessageType == WebSocketMessageType.Close) return;
                if (message.MessageType != WebSocketMessageType.Text) throw new InvalidOperationException("Text only");
                length += message.Count;
            } while (!message.EndOfMessage);
            using var doc = JsonDocument.Parse(buffer.AsMemory(0, length));
            var root = doc.RootElement;
            if (root.GetProperty("protocolMajor").GetInt32() != 1 || root.GetProperty("sessionId").GetString() != session)
                throw new InvalidOperationException("Incompatible session");
            switch (root.GetProperty("type").GetString())
            {
                case "input.releaseAll": Input.ReleaseAll(); break;
                case "input.renew": Input.Renew(session, root.GetProperty("pressIds").EnumerateArray().Select(x => x.GetString() ?? "")); break;
                case "ping": await responses.WriteAsync(new { protocolMajor = 1, type = "pong", sessionId = session }, ct); break;
                case "control.invoke":
                    var id = root.GetProperty("commandId").GetString() ?? "";
                    InputResult result;
                    lock (profileGate) {
                    if (ct.IsCancellationRequested || root.GetProperty("profileRevision").GetInt32() != Profile.Revision || root.GetProperty("profileId").GetString() != Profile.Id) result = new(false, "profile_mismatch");
                    else result = Input.Invoke(session, id, root.GetProperty("actionId").GetString() ?? "", root.GetProperty("phase").GetString() ?? "", root.GetProperty("pressId").GetString() ?? "");
                    }
                    var actionId = root.GetProperty("actionId").GetString();
                    LastCommand = actionId + " · " + result.Code;
                    await responses.WriteAsync(new { protocolMajor = 1, type = "control.ack", sessionId = session, commandId = id, actionId, success = result.Success, code = result.Code }, ct);
                    break;
                default: throw new InvalidOperationException("Unknown message");
            }
        }
    }
    public async ValueTask DisposeAsync()
    {
        Backend.Enabled = false;
        Input.ReleaseAll();
        stop.Cancel();
        ets2Map.Dispose(); atsMap.Dispose();
        if (Browser is not null) await Browser.DisposeAsync();
        udp?.Dispose();
        f1Udp?.Dispose();
        accReader.Dispose();
        scsReader.Dispose();
        discovery?.Dispose();
        if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
        await Task.WhenAll(loops);
        certificate?.Dispose();
        stop.Dispose();
    }
}
