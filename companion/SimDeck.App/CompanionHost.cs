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
using SimDeck.Core;

namespace SimDeck.App;

public sealed class CompanionHost : IAsyncDisposable
{
    public SettingsStore Store { get; }
    public TelemetryHub Telemetry { get; } = new();
    public PairingGate Pairing { get; } = new();
    public WindowsInput Backend { get; } = new();
    public InputEngine Input { get; }
    readonly IInputBackend inputBackend;
    public string Fingerprint { get; private set; } = "";
    public string Status { get; private set; } = "Запуск…";
    public string LastCommand { get; private set; } = "Команд пока нет";
    public string Device { get; private set; } = "Не подключено";
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
    string GameId => IsF1 ? Profile.Id : "beamng";
    public string TelemetryDiagnostic => IsF1
        ? f1Udp is null ? "UDP 20777 занят. Закройте другую программу телеметрии на этом порту." : $"{Profile.Name}: включите UDP, IP 127.0.0.1, порт 20777, формат {(Profile.Id == "f1-25" ? "2025 или 2024" : "2024")}. Выйдите на трассу."
        : udp is null ? "UDP-порт занят: закройте другой экземпляр Companion."
        : ReceivedPackets == 0 ? "Нет пакетов от игры. Установите мод SimDeck и перезагрузите машину (Ctrl+R)."
        : $"UDP {Store.Value.UdpPort}: принято {ReceivedPackets}, отклонено {InvalidPackets}";
    long lastExtendedPacket;
    public volatile bool Demo;
    readonly CancellationTokenSource stop = new();
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    readonly SemaphoreSlim controller = new(1, 1);
    CancellationTokenSource? clientStop;
    WebApplication? app;
    UdpClient? udp;
    UdpClient? f1Udp;
    F1TelemetryParser f1Parser = new();
    readonly object profileGate = new();
    ServiceDiscovery? discovery;
    X509Certificate2? certificate;
    Task[] loops = [];

    public CompanionHost(string dataDirectory, IInputBackend? inputBackend = null)
    {
        Store = new(dataDirectory);
        this.inputBackend = inputBackend ?? Backend;
        Input = new(this.inputBackend);
        ApplyBindings();
    }
    public void ApplyBindings()
    {
        var bindings = Profile.Actions.Select(x => WindowsInput.ParseBinding(x.Id, x.Key, x.Gesture)).ToArray();
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
            Store.Save(); ApplyBindings();
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
            Telemetry.Reset(Demo ? "demo" : GameId);
        }
    }
    public void SetDemo(bool value)
    {
        Input.ReleaseAll();
        Demo = value; Backend.Demo = value;
        Telemetry.Reset(value ? "demo" : GameId);
    }
    public void RevokeDevices() { Store.RevokeAll(); Browser?.Revoke(); clientStop?.Cancel(); Input.ReleaseAll(); }
    internal async Task DisconnectControllerAsync(CancellationToken cancellationToken)
    {
        try { clientStop?.Cancel(); } catch (ObjectDisposedException) { }
        Input.ReleaseAll();
        // A newly paired browser must not lose the controller race to an old tab.
        // Wait until ServeController has released the single-controller gate.
        if (await controller.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken)) controller.Release();
    }
    public async Task StartAsync(X509Certificate2? serverCertificate = null, bool localOnly = false)
    {
        certificate = serverCertificate ?? Store.Certificate();
        Fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData)).ToLowerInvariant();
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => { o.Limits.MaxRequestBodySize = 4096;
            if (localOnly) o.Listen(IPAddress.Loopback, Store.Value.Port, l => l.UseHttps(certificate));
            else o.ListenAnyIP(Store.Value.Port, l => l.UseHttps(certificate)); });
        app = builder.Build();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(10) });
        app.MapGet("/health", () => Results.Json(new { name = "SimDeck", protocolMajor = 1 }));
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
            if (localOnly) { loops = [ReceiveTelemetry(), ReceiveF1(), RunTick()]; return; }
            discovery = new ServiceDiscovery();
            var service = new ServiceProfile("SimDeck-" + Environment.MachineName, "_simdeck._tcp", (ushort)Store.Value.Port);
            service.AddProperty("fingerprint", Fingerprint);
            service.AddProperty("version", "1");
            discovery.Advertise(service);
        }
        catch (Exception ex) { Status += " · автопоиск: " + ex.GetType().Name; }
        loops = [ReceiveTelemetry(), ReceiveF1(), RunTick()];
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
            }
        }
        catch (OperationCanceledException) { }
    }
    async Task HandleSocket(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization[7..] : "";
        if (!Store.IsTrusted(token)) { context.Response.StatusCode = 401; return; }
        await ServeController(context, "Android");
    }
    internal async Task ServeController(HttpContext context, string device)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        if (!await controller.WaitAsync(0)) { context.Response.StatusCode = 409; return; }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(stop.Token, context.RequestAborted);
        clientStop = lifetime;
        var session = Guid.NewGuid().ToString("N");
        try
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            Input.BeginSession(session);
            Device = device + " · подключено";
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
        finally { Input.EndSession(session); Device = "Не подключено"; clientStop = null; controller.Release(); }
    }
    async Task SendLoop(WebSocket socket, ChannelReader<object> responses, string session, CancellationToken ct)
    {
        long lastInputRevision = -1;
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
            await Send(socket, Telemetry.Snapshot(session), ct);
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
        if (Browser is not null) await Browser.DisposeAsync();
        udp?.Dispose();
        f1Udp?.Dispose();
        discovery?.Dispose();
        if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
        await Task.WhenAll(loops);
        certificate?.Dispose();
        stop.Dispose();
    }
}
