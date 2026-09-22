using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using SimDeck.App;
using SimDeck.Core;

if (args.Length >= 3 && args[0] == "--smoke-app") { await AppSmoke.Run(args[1], args[2]); return; }
if (args.Length >= 2 && args[0] == "--acc-live")
{
    await using var accHost = new CompanionHost(args[1]);
    accHost.SelectProfile("acc");
    await accHost.StartAsync(localOnly: true);
    await Task.Delay(500);
    var live = accHost.Telemetry.Read();
    Console.WriteLine(JsonSerializer.Serialize(new { live.Source, live.Age, live.Data }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    if (live.Data?.Acc is null || live.Age >= 500) Environment.ExitCode = 2;
    return;
}
if (args.Length == 2 && args[0] == "--render-editor") { RenderTest.Save(args[1]); return; }
if (args.Length >= 2 && args[0] == "--browser-server")
{
    await using var previewHost = new CompanionHost(args[1], new RecordingInput(Path.Combine(args[1], "input-events.json")));
    previewHost.Store.Value.Port=29443; previewHost.Store.Value.UdpPort=24444;
    previewHost.SelectProfile("f1-24");
    await previewHost.StartAsync(localOnly:true);
    await previewHost.StartBrowserAsync(28787,true);
    File.WriteAllText(Path.Combine(args[1],"browser-pin.txt"),previewHost.Browser!.Pairing.Open());
    Console.WriteLine("Browser test at http://127.0.0.1:28787; recording backend, no game injection.");
    previewHost.Backend.Enabled=true;
    for(var i=0;i<18000&&!File.Exists(Path.Combine(args[1],"stop"));i++) {
        previewHost.Telemetry.Publish(new(60,10400,6,.6,0,0,0,13000,F1:new(
            Enumerable.Range(0,4).Select(n=>new F1Wheel(86+n,95,400,23.5,12,0)).ToArray(),110,
            new Dictionary<string,double>(),[],null,new(7,5891,15,true,[new(0,"Player",1,1,1,4,2500,0,2,1,0,0,true),new(1,"NORRIS",1,4,2,4,2200,0,2,1,1000,1000,false)]))));
        await Task.Delay(100);
    }
    return;
}
if (args.Length >= 2 && args[0] == "--tablet-feedback-test")
{
    await using var visualHost = new CompanionHost(args[1], new RecordingInput(Path.Combine(args[1], "input-events.json")));
    visualHost.SelectProfile("beamng-default");
    visualHost.SaveProfile(visualHost.Profile with { Name = "ТЕСТ ПОДСВЕТКИ" });
    await visualHost.StartAsync();
    visualHost.Telemetry.Reset("fixture");
    var modeFile = Path.Combine(args[1], "feedback-mode.txt");
    for (var tick = 0; tick < 18000 && !File.Exists(Path.Combine(args[1], "stop")); tick++)
    {
        var mode = int.TryParse(File.Exists(modeFile) ? File.ReadAllText(modeFile) : "1", out var light) ? light : 1;
        visualHost.Telemetry.Publish(new(0, 900, 0, .5, 0, 0, 0, 7000, Headlights: mode,
            ActionStates: new Dictionary<string, bool> { ["hazards"] = mode > 0, ["fogLights"] = mode > 0, ["ignition"] = mode > 0, ["fourWheelDrive"] = mode > 0 }));
        await Task.Delay(33);
    }
    return;
}
if (args.Length >= 2 && args[0] == "--tablet-ignition-test")
{
    Directory.CreateDirectory(args[1]);
    var recorder = new RecordingInput(Path.Combine(args[1], "input-events.json"));
    await using var tabletHost = new CompanionHost(args[1], recorder);
    await tabletHost.StartAsync(localOnly: true);
    tabletHost.Telemetry.Reset("fixture");
    Console.WriteLine("Ignition device test ready: recording backend only, no Windows key injection.");
    using var quit = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; quit.Cancel(); };
    try
    {
        while (!quit.IsCancellationRequested)
        {
            tabletHost.Telemetry.Publish(new Telemetry(0, 0, 0, .5, 0, 0, 0));
            await Task.Delay(33, quit.Token);
        }
    }
    catch (OperationCanceledException) { }
    return;
}
if (args.Length >= 2 && args[0] == "--tablet-server")
{
    await using var tabletHost = new CompanionHost(args[1]);
    await tabletHost.StartAsync(localOnly: true);
    tabletHost.SetDemo(true);
    File.WriteAllText(Path.Combine(args[1], "test-connection.json"), JsonSerializer.Serialize(new { tabletHost.Fingerprint, Code = tabletHost.Pairing.Open() }));
    Console.WriteLine("Tablet test server ready on loopback:9443. Input disabled.");
    using var quit = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; quit.Cancel(); };
    try { await Task.Delay(Timeout.Infinite, quit.Token); } catch (OperationCanceledException) { }
    return;
}
var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS " + name); }
await ProfileTests.Run(Check, args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "simdeck-profile-tests-" + Guid.NewGuid().ToString("N")));
F1DetailTests.Run(Check);
F1RaceTests.Run(Check);
AccTelemetryTests.Run(Check);
var oldKeys = new Dictionary<string, string> { ["lights"] = "N", ["horn"] = "H", ["ignition"] = "V", ["reset"] = "F10" };
Check(BeamNgProfile.AddMissingKeys(oldKeys) && oldKeys.Count == 22 && oldKeys["reset"] == "F10", "Profile upgrade adds 18 actions and preserves user bindings");
Check(!BeamNgProfile.AddMissingKeys(oldKeys), "Profile migration is idempotent");
var retiredKeys = new[] { "shiftUp", "shiftDown", "parkingBrake", "handbrakeHold" };
foreach (var id in retiredKeys) oldKeys[id] = "P";
Check(BeamNgProfile.AddMissingKeys(oldKeys) && retiredKeys.All(id => !oldKeys.ContainsKey(id) && BeamNgProfile.Actions.All(a => a.Id != id)), "Removed shift and handbrake actions cannot remain in upgraded profile");
Check(oldKeys["recoverRoad"] == "Alt+T" && oldKeys["loadHome"] == "Home" && oldKeys["saveHome"] == "Ctrl+Home" && oldKeys["recoverAlt"] == "Ctrl+Insert", "Recovery bindings match supplied screenshot");
Check(BeamNgProfile.Actions.All(a => WindowsInput.ParseBinding(a.Id, oldKeys[a.Id], a.Gesture).ActionId == a.Id), "Every default profile binding parses including chords and punctuation");
Check(WindowsInput.ParseBinding("mode", "Alt+Shift+N", "press").Modifiers!.Length == 2, "Multiple modifiers are supported");
Check(WindowsInput.ParseKey("Down") == 0xE050 && WindowsInput.ParseKey("Right") == 0xE04D, $"Arrow scan codes preserve E0 prefix: {WindowsInput.ParseKey("Down"):X4}/{WindowsInput.ParseKey("Right"):X4}");
var scanInput = WindowsInput.DescribeInput(WindowsInput.ParseKey("Up"), true, false);
var virtualInput = WindowsInput.DescribeInput(WindowsInput.ParseKey("Up"), true, true);
Check(scanInput == (0, 0x48, 0x0009) && virtualInput == (0x26, 0, 0x0001), "Scan Code and Virtual-Key modes preserve the extended Up arrow");
Check(WindowsInput.ParseKey("NumPad2") == 0x50 && WindowsInput.ParseKey("NumPad6") == 0x4D && WindowsInput.ParseKey("Return") == 0x1C, "Numpad and main Enter retain unextended scan codes");
Check(WindowsInput.ParseKey("Delete") == 0xE053 && WindowsInput.ParseKey("RightCtrl") == 0xE01D, "Navigation and right modifiers preserve physical key identity");
try { WindowsInput.ParseBinding("bad", "A+Q", "press"); Check(false, "Unsupported modifier rejected"); } catch (ArgumentException) { Check(true, "Unsupported modifier rejected"); }
byte[] Packet(int length = 92)
{
    var b = new byte[length]; "beam"u8.CopyTo(b.AsSpan(4)); b[10] = 5;
    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(12), 40);
    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(16), 6234);
    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(28), .42f);
    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(48), .8f);
    return b;
}
var bytes = Packet();
Check(OutGaugeParser.TryParse(bytes, out var t) && t!.Gear == 4 && t.SpeedMps == 40 && t.MaxRpm is null && t.FuelLiters is null, "OutGauge units, gear, unsupported fields");
using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "telemetry-v1.json")));
var expectedTelemetry = fixture.RootElement.GetProperty("data").Deserialize<Telemetry>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
Check(expectedTelemetry!.Gear == t!.Gear && expectedTelemetry.Rpm == t.Rpm && Math.Abs(expectedTelemetry.FuelFraction!.Value - t.FuelFraction!.Value) < .00001, "Shared Kotlin/C# fixture matches parsed packet");
Check(OutGaugeParser.TryParse(Packet(96), out _), "Optional OutGauge ID");
Check(!OutGaugeParser.TryParse(new byte[91], out _), "Truncated packet rejected");
Check(!OutGaugeParser.TryParse(new byte[93], out _), "Wrong packet size rejected");
bytes[4] = 0; Check(!OutGaugeParser.TryParse(bytes, out _), "Wrong source signature rejected");
bytes = Packet(); BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(16), float.NaN);
Check(!OutGaugeParser.TryParse(bytes, out _), "NaN rejected");
bytes = Packet(); bytes[10] = 0; OutGaugeParser.TryParse(bytes, out t); Check(t?.Gear == -1, "Reverse mapping");
bytes[10] = 1; OutGaugeParser.TryParse(bytes, out t); Check(t?.Gear == 0, "Neutral mapping");
bytes = Packet(); BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(28), 42);
Check(!OutGaugeParser.TryParse(bytes, out _), "Fuel fraction validated");

long now = 1000;
var chordBackend = new FaultInput();
var chords = new InputEngine(chordBackend, () => now);
chords.Configure([new("esc", 16, "press", [29]), new("drive", 31, "press", [56]), new("horn", 35, "hold")]);
chords.BeginSession("chords");
Check(chords.Invoke("chords", "c1", "esc", "press", "p1").Success && chordBackend.Events.SequenceEqual(new (ushort, bool)[] { (29, true), (16, true) }), "Chord presses modifier before action key");
Check(chords.Invoke("chords", "c2", "horn", "down", "p2").Code == "input_busy" && chordBackend.Events.Count == 2, "Overlapping control cannot inherit held modifier");
now += 100; chords.Tick();
Check(chordBackend.Events.TakeLast(2).SequenceEqual(new (ushort, bool)[] { (16, false), (29, false) }) && chords.HeldCount == 0, "Chord releases main key before modifier");
chords.Invoke("chords", "c3", "horn", "down", "p3");
Check(chords.Invoke("chords", "c4", "drive", "press", "p4").Code == "input_busy", "Chord waits for existing hold to end");
chords.ReleaseAll(); chordBackend.Events.Clear(); chordBackend.FailDown = 16;
Check(!chords.Invoke("chords", "c5", "esc", "press", "p5").Success && chords.HeldCount == 0 && chordBackend.Events.SequenceEqual(new (ushort, bool)[] { (29, true), (29, false) }), "Failed main key rolls back acquired modifier");
chordBackend.FailDown = null; chords.Invoke("chords", "c6", "esc", "press", "p6"); chordBackend.FailUpOnce = 29;
now += 100; chords.Tick();
Check(chords.HeldCount == 1 && chords.LastFault.Length > 0, "Failed modifier release retains ownership for retry");
chords.Tick(); Check(chords.HeldCount == 0 && chordBackend.Events.Last() == (29, false), "Failed modifier release is retried");
var focusBackend = new FakeInput(); var focusChords = new InputEngine(focusBackend, () => now);
focusChords.Configure([new("mode", 16, "press", [29])]); focusChords.BeginSession("focus"); focusChords.Invoke("focus", "c", "mode", "press", "p");
focusBackend.CanInject = false; focusChords.Tick(); Check(focusChords.HeldCount == 0 && focusBackend.Events.TakeLast(2).SequenceEqual(new (ushort, bool)[] { (16, false), (29, false) }), "Focus loss releases complete chord");
byte[] ExtendedPacket(int gear = 8, uint mode = 1)
{
    var b = new byte[48]; "SMD1"u8.CopyTo(b);
    BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), 1);
    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(8), 25);
    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(12), 3210);
    BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(16), gear);
    BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(20), mode);
    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(24), .5f);
    BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(40), 7000);
    BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(44), 8);
    return b;
}
Check(SimDeckParser.TryParse(ExtendedPacket(), out var extended) && extended!.SpeedMps == 25 && extended.Rpm == 3210 && extended.GearDisplay == "D" && extended.MaxGear == 8 && extended.MaxRpm == 7000, "Extended telemetry preserves measurements and arcade mode");
Check(SimDeckParser.TryParse(ExtendedPacket(12, 2), out extended) && extended!.GearDisplay == "12", "Realistic gear count is not hardcoded");
Check(SimDeckParser.TryParse(ExtendedPacket(-2, 1), out extended) && extended!.GearDisplay == "R", "Multiple reverse gears display R");
Check(SimDeckParser.TryParse(ExtendedPacket(0, 2), out extended) && extended!.GearDisplay == "N", "Extended neutral mapping");
Check(!SimDeckParser.TryParse(ExtendedPacket(1, 3), out _) && !SimDeckParser.TryParse(new byte[47], out _), "Invalid extended mode and truncated packet rejected");
var badExtended = ExtendedPacket(); BinaryPrimitives.WriteSingleLittleEndian(badExtended.AsSpan(8), float.NaN);
Check(!SimDeckParser.TryParse(badExtended, out _), "Non-finite extended speed rejected");
byte[] StatePacket(int lights = 1, uint known = 9, uint active = 1)
{
    var b = new byte[60]; ExtendedPacket().CopyTo(b, 0); "SMD2"u8.CopyTo(b);
    BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), 2);
    BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(48), known);
    BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(52), active);
    BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(56), lights); return b;
}
Check(SimDeckParser.TryParse(StatePacket(), out var withStates) && withStates!.Headlights == 1 && withStates.ActionStates!["hazards"] && !withStates.ActionStates["fogLights"] && !withStates.ActionStates.ContainsKey("fourWheelDrive"), "State packet distinguishes active, inactive and unsupported systems");
Check(SimDeckParser.TryParse(StatePacket(2), out withStates) && withStates!.Headlights == 2, "High beam state is distinct from low beam");
Check(SimDeckParser.TryParse(StatePacket(-1, 0, 0), out withStates) && withStates!.Headlights is null && withStates.ActionStates!.Count == 0, "Unknown light state is not reported as off");
Check(!SimDeckParser.TryParse(StatePacket(3), out _) && !SimDeckParser.TryParse(StatePacket(0, 0, 1), out _), "Invalid headlight and unknown active bit rejected");
Check(SimDeckParser.TryParse(ExtendedPacket(), out withStates) && withStates!.ActionStates is null && withStates.Headlights is null, "Old telemetry mod remains compatible without false button states");
var backend = new FakeInput();
var engine = new InputEngine(backend, () => now);
engine.Configure([new("horn", 35, "hold"), new("ptt", 35, "hold"), new("reset", 19)]);
engine.BeginSession("s1");
Check(engine.Invoke("s1", "c1", "horn", "down", "p1").Success && engine.HeldCount == 1, "Hold begins");
Check(engine.Invoke("s1", "c1", "horn", "down", "p1").Code == "duplicate" && backend.Events.Count == 1, "Duplicate command cannot inject twice");
now = 1400; engine.Renew("s1", ["p1"]); now = 1600; engine.Tick(); Check(engine.HeldCount == 1, "Active lease renewed");
now = 1900; engine.Tick(); Check(engine.HeldCount == 0 && backend.Events.Last() == (35, false), "Expired lease releases");
engine.Renew("s1", ["p1"]); Check(engine.HeldCount == 0, "Late renew cannot revive hold");
Check(!engine.Invoke("s1", "c2", "horn", "down", "p1").Success, "Closed press ID cannot revive hold");
engine.Invoke("s1", "c3", "horn", "down", "p2"); engine.Invoke("s1", "c4", "ptt", "down", "p3");
var count = backend.Events.Count;
engine.Invoke("s1", "c5", "horn", "up", "p2"); Check(backend.Events.Count == count && engine.HeldCount == 1, "Shared key ownership respected");
engine.Invoke("s1", "c6", "ptt", "up", "p3"); Check(backend.Events.Count == count + 1 && engine.HeldCount == 0, "Last owner releases key");
engine.Invoke("s1", "c7", "reset", "press", "p4"); now += 100; engine.Tick(); Check(engine.HeldCount == 0, "Press has bounded pulse");
engine.Invoke("s1", "c8", "horn", "down", "p5"); backend.CanInject = false; engine.Tick();
Check(engine.HeldCount == 0, "Focus loss releases hold");
Check(!engine.Invoke("s1", "c9", "reset", "press", "p6").Success, "Unfocused injection rejected");
backend.CanInject = true; engine.BeginSession("s2"); Check(!engine.Invoke("s1", "c10", "horn", "down", "p7").Success, "Old session rejected");
engine.Invoke("s2", "c11", "horn", "down", "p8"); engine.EndSession("s2"); Check(engine.HeldCount == 0, "Disconnect releases hold");

var ignitionBackend = new FakeInput();
var ignition = new InputEngine(ignitionBackend, () => now);
ignition.Configure([new("ignition", 47, "tapThenHold"), new("lights", 49), new("reset", 19)]);
ignition.BeginSession("ignition-session");
Check(ignition.Invoke("ignition-session", "i0", "ignition", "down", "blocked").Code == "ignition_tap_required" && ignitionBackend.Events.Count == 0, "Starter cannot run before a separate short ignition tap");
Check(ignition.Invoke("ignition-session", "i1", "ignition", "press", "tap1").Success, "Ignition accepts a short pulse for older clients");
Check(!ignition.ControlState.IgnitionReady, "Starter is not armed until the ignition pulse has finished");
now += 100; ignition.Tick();
Check(ignition.ControlState.IgnitionReady && ignition.HeldCount == 0 && ignitionBackend.Events.SequenceEqual(new (ushort, bool)[] { (47, true), (47, false) }), "Completed short tap arms starter and produces exactly one down/up pair");
Check(ignition.Invoke("ignition-session", "i2", "ignition", "down", "hold1").Success, "Same ignition binding accepts a starter hold");
Check(!ignition.ControlState.IgnitionReady, "Starter hold consumes the completed short tap");
for (var tick = 0; tick < 20; tick++) { now += 100; ignition.Renew("ignition-session", ["hold1"]); ignition.Tick(); }
Check(ignition.HeldCount == 1 && ignitionBackend.Events.Count == 3, "Ignition stays held for two seconds without repeated key-down events");
ignition.Invoke("ignition-session", "i3", "ignition", "up", "hold1");
now += 100; ignition.Tick();
Check(ignition.HeldCount == 0 && ignitionBackend.Events.Count == 4 && ignitionBackend.Events.Last() == (47, false), "Releasing starter emits only key-up, no extra ignition tap");
Check(ignition.Invoke("ignition-session", "i4-blocked", "ignition", "down", "no-tap").Code == "ignition_tap_required", "Next starter attempt requires a new separate short tap");
ignition.Invoke("ignition-session", "i4-tap", "ignition", "press", "tap2"); now += 100; ignition.Tick();
Check(ignition.Invoke("ignition-session", "i4", "ignition", "down", "hold2").Success, "A new tap permits the next starter hold"); now += 500; ignition.Tick();
Check(ignition.HeldCount == 0 && ignitionBackend.Events.Last() == (47, false), "Ignition hold is released when heartbeat expires");
Check(!ignition.Invoke("ignition-session", "i5", "lights", "down", "light1").Success, "Lights remains press-only");
ignition.Invoke("ignition-session", "i6", "ignition", "press", "tap3"); now += 100; ignition.Tick();
ignitionBackend.CanInject = false; ignition.Tick();
Check(!ignition.ControlState.IgnitionReady, "Losing game focus clears starter permission");
ignitionBackend.CanInject = true;
ignition.Invoke("ignition-session", "i7", "ignition", "press", "tap4");
ignition.Invoke("ignition-session", "i8", "reset", "press", "reset1"); now += 100; ignition.Tick();
Check(!ignition.ControlState.IgnitionReady, "Vehicle reset invalidates a pending ignition pulse");
ignition.Invoke("ignition-session", "i9", "ignition", "press", "tap5"); now += 100; ignition.Tick();
ignition.EndSession("ignition-session"); ignition.BeginSession("new-session");
Check(!ignition.ControlState.IgnitionReady, "A new connection requires a new ignition tap");

var pairing = new PairingGate(() => now);
var pin = pairing.Open(); Check(pairing.Consume(pin) && !pairing.Consume(pin), "Pair code is single use");
pin = pairing.Open(); now += 120001; Check(!pairing.Consume(pin), "Expired PIN rejected");
pin = pairing.Open(); for (var i = 0; i < 5; i++) pairing.Consume("bad"); Check(!pairing.Consume(pin), "PIN attempts bounded");

var dataDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "simdeck-tests-" + Guid.NewGuid().ToString("N"));
await using var host = new CompanionHost(dataDir);
int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
host.Store.Value.Port = FreePort(); host.Store.Value.UdpPort = FreePort();
using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=SimDeck Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var issued = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
using var cert = X509CertificateLoader.LoadPkcs12(issued.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.UserKeySet);
await host.StartAsync(cert, localOnly: true);
var expected = cert.GetCertHashString(HashAlgorithmName.SHA256);
using var http = new HttpClient(new HttpClientHandler { UseProxy = false, ServerCertificateCustomValidationCallback = (_, c, _, _) => c?.GetCertHashString(HashAlgorithmName.SHA256) == expected });
var baseUrl = $"https://127.0.0.1:{host.Store.Value.Port}";
Check((await http.GetAsync(baseUrl + "/health")).IsSuccessStatusCode, "HTTPS health endpoint");
var denied = await http.PostAsJsonAsync(baseUrl + "/pair", new { code = "123456", name = "Test" });
Check(denied.StatusCode == HttpStatusCode.Forbidden, "Pairing closed by default");
pin = host.Pairing.Open();
var accepted = await http.PostAsJsonAsync(baseUrl + "/pair", new { code = pin, name = "Test" });
var auth = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
var token = auth.RootElement.GetProperty("token").GetString()!;
Check(accepted.IsSuccessStatusCode && host.Store.IsTrusted(token), "HTTPS pairing issues device token");
Check(!System.IO.File.ReadAllText(Path.Combine(dataDir, "settings.json")).Contains(token), "Plain device token not stored on Windows");
using var ws = new ClientWebSocket();
ws.Options.Proxy = new WebProxy();
ws.Options.RemoteCertificateValidationCallback = (_, c, _, _) => c?.GetCertHashString(HashAlgorithmName.SHA256) == expected;
ws.Options.SetRequestHeader("Authorization", "Bearer " + token);
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await ws.ConnectAsync(new Uri(baseUrl.Replace("https:", "wss:") + "/ws"), timeout.Token);
async Task<JsonDocument> Receive()
{
    var buffer = new byte[16384]; var offset = 0; WebSocketReceiveResult part;
    do { part = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), timeout.Token); offset += part.Count; } while (!part.EndOfMessage);
    return JsonDocument.Parse(buffer.AsMemory(0, offset));
}
var hello = await Receive(); var sid = hello.RootElement.GetProperty("sessionId").GetString();
Check(hello.RootElement.GetProperty("type").GetString() == "hello", "Authenticated WebSocket handshake");
Check(hello.RootElement.GetProperty("controls").GetArrayLength() == 22 && hello.RootElement.GetProperty("profileRevision").GetInt32() == BeamNgProfile.Revision, "WSS delivers full profile and revision");
using var sender = new UdpClient(); await sender.SendAsync(Packet(), new IPEndPoint(IPAddress.Loopback, host.Store.Value.UdpPort));
JsonDocument snapshot;
do { snapshot = await Receive(); } while (snapshot.RootElement.GetProperty("type").GetString() != "telemetry.snapshot" || snapshot.RootElement.GetProperty("data").ValueKind == JsonValueKind.Null);
Check(snapshot.RootElement.GetProperty("data").GetProperty("gear").GetInt32() == 4, "Real UDP → parser → WSS snapshot");
await sender.SendAsync(ExtendedPacket(), new IPEndPoint(IPAddress.Loopback, host.Store.Value.UdpPort));
do { snapshot = await Receive(); } while (snapshot.RootElement.GetProperty("type").GetString() != "telemetry.snapshot" || !snapshot.RootElement.GetProperty("data").TryGetProperty("gearboxMode", out var modeElement) || modeElement.GetString() != "arcade");
Check(snapshot.RootElement.GetProperty("data").GetProperty("gearDisplay").GetString() == "D", "Extended UDP → WSS carries arcade gear");
await sender.SendAsync(Packet(), new IPEndPoint(IPAddress.Loopback, host.Store.Value.UdpPort));
await Task.Delay(70);
Check(host.Telemetry.Read().Data?.GearboxMode == "arcade", "OutGauge does not overwrite fresh extended mode");
await sender.SendAsync(ExtendedPacket(8, 2), new IPEndPoint(IPAddress.Loopback, host.Store.Value.UdpPort));
await Task.Delay(70);
Check(host.Telemetry.Read().Data?.GearDisplay == "8", "Live mode change updates gear without reconnecting");
await sender.SendAsync(StatePacket(2), new IPEndPoint(IPAddress.Loopback, host.Store.Value.UdpPort));
do { snapshot = await Receive(); } while (snapshot.RootElement.GetProperty("type").GetString() != "telemetry.snapshot" || !snapshot.RootElement.GetProperty("data").TryGetProperty("headlights", out var lightElement) || lightElement.ValueKind == JsonValueKind.Null);
Check(snapshot.RootElement.GetProperty("data").GetProperty("headlights").GetInt32() == 2 && snapshot.RootElement.GetProperty("data").GetProperty("actionStates").GetProperty("hazards").GetBoolean(), "Actual UDP → WSS includes confirmed light and toggle states");
await Task.Delay(550);
await sender.SendAsync(Packet(), new IPEndPoint(IPAddress.Loopback, host.Store.Value.UdpPort));
await Task.Delay(70);
Check(host.Telemetry.Read().Data?.GearboxMode is null && host.Telemetry.Read().Data?.Gear == 4, "Legacy fallback resumes when extended packets stop");
var command = JsonSerializer.SerializeToUtf8Bytes(new { protocolMajor = 1, type = "control.invoke", sessionId = sid, commandId = "test-cmd", profileId = "beamng-default", profileRevision = BeamNgProfile.Revision, actionId = "horn", phase = "down", pressId = "test-press" });
await ws.SendAsync(command, WebSocketMessageType.Text, true, timeout.Token);
JsonDocument ack;
do { ack = await Receive(); } while (ack.RootElement.GetProperty("type").GetString() != "control.ack");
Check(ack.RootElement.GetProperty("code").GetString() == "game_not_focused_or_input_disabled", "Network command cannot bypass disabled input");
host.SelectProfile("f1-24");
using var f1Socket = new ClientWebSocket();
f1Socket.Options.Proxy = new WebProxy();
f1Socket.Options.RemoteCertificateValidationCallback = (_, c, _, _) => c?.GetCertHashString(HashAlgorithmName.SHA256) == expected;
f1Socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
using var f1Timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
await f1Socket.ConnectAsync(new Uri(baseUrl.Replace("https:", "wss:") + "/ws"), f1Timeout.Token);
var fullBuffer = new byte[65536]; var fullOffset = 0; WebSocketReceiveResult fullPart;
do { fullPart = await f1Socket.ReceiveAsync(new ArraySegment<byte>(fullBuffer, fullOffset, fullBuffer.Length - fullOffset), f1Timeout.Token); fullOffset += fullPart.Count; } while (!fullPart.EndOfMessage);
using var f1Hello = JsonDocument.Parse(fullBuffer.AsMemory(0, fullOffset));
var wireActions = JsonSerializer.Deserialize<List<DeckAction>>(f1Hello.RootElement.GetProperty("controls"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
Check(wireActions.SequenceEqual(GameProfiles.F1().Actions), "Actual WSS handshake delivers all 69 F1 bindings, groups and gestures");
f1Socket.Abort();
host.RevokeDevices(); Check(!host.Store.IsTrusted(token), "Device token revoked");
ws.Abort();
await Task.Delay(200);
await BrowserTests.Run(host, Check);
if (args.Length > 1) { RenderTest.Save(args[1]); Check(File.Exists(args[1]), "WPF layout rendered without opening a desktop window"); }
Console.WriteLine($"ALL {passed} CHECKS PASSED");

sealed class FakeInput : IInputBackend
{
    public bool CanInject { get; set; } = true;
    public List<(ushort, bool)> Events { get; } = [];
    public bool Send(ushort code, bool down) { Events.Add((code, down)); return true; }
}

sealed class FaultInput : IInputBackend
{
    public bool CanInject => true;
    public ushort? FailDown, FailUpOnce;
    public List<(ushort, bool)> Events { get; } = [];
    public bool Send(ushort code, bool down)
    {
        if (down && FailDown == code) return false;
        if (!down && FailUpOnce == code) { FailUpOnce = null; return false; }
        Events.Add((code, down)); return true;
    }
}

sealed class RecordingInput(string path) : IInputBackend
{
    readonly List<object> events = [];
    public bool CanInject => true;
    public bool Send(ushort code, bool down)
    {
        events.Add(new { scanCode = code, down, atMs = Environment.TickCount64 });
        File.WriteAllText(path, JsonSerializer.Serialize(events));
        return true;
    }
}
