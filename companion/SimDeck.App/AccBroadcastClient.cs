using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SimDeck.Core;

namespace SimDeck.App;

internal sealed class AccBroadcastClient : IDisposable
{
    readonly AccRaceReader reader = new();
    UdpClient? socket;
    long nextAttempt, lastMessage, lastEntryRequest;
    AccRace? lastRace;
    public string Status { get; private set; } = "Карта ACC: ожидание Broadcasting.";
    public static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Assetto Corsa Competizione", "Config", "broadcasting.json");
    internal static (int Port, string Password) ReadConfig(byte[] bytes)
    {
        // ACC writes UTF-16 LE, sometimes without BOM; preserve passwords locally.
        var utf16 = bytes.Length >= 2 && ((bytes[0] == 255 && bytes[1] == 254) || bytes[1] == 0);
        var text = (utf16 ? Encoding.Unicode : new UTF8Encoding(false, true)).GetString(bytes).TrimStart('\uFEFF');
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        var port = root.TryGetProperty("updListenerPort", out var p) && p.TryGetInt32(out var value) ? value : 0;
        if (port is < 1024 or > 65535) throw new InvalidDataException("Broadcasting выключен: включите канал ACC установщиком SimDeck и перезапустите игру.");
        var password = root.TryGetProperty("connectionPassword", out var s) ? s.GetString() ?? "" : "";
        if (Encoding.UTF8.GetByteCount(password) > 4096) throw new InvalidDataException("Слишком длинный пароль Broadcasting ACC.");
        return (port, password);
    }
    public AccRace Snapshot(int? playerId)
    {
        var now = Environment.TickCount64;
        try
        {
            if (socket is null && now >= nextAttempt)
            {
                nextAttempt = now + 5000;
                var config = ReadConfig(File.ReadAllBytes(ConfigPath));
                socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
                socket.Connect(IPAddress.Loopback, config.Port);
                var payload = AccRaceReader.Register(config.Password); socket.Send(payload, payload.Length);
                lastMessage = now; Status = "Карта ACC: подключение к Broadcasting…";
            }
            for (var i = 0; socket is not null && socket.Available > 0 && i < 100; i++)
            {
                IPEndPoint peer = new(IPAddress.Loopback, 0); var packet = socket.Receive(ref peer);
                if (!reader.TryRead(packet)) continue;
                lastMessage = now;
                if (packet[0] == 1)
                {
                    if (!reader.Registered) {
                        Status = "Карта ACC: игра отклонила подключение. Проверьте broadcasting.json и перезапустите ACC.";
                        ResetSocket(); nextAttempt = now + 5000; break;
                    }
                    Send(10); Send(11); reader.EntryListRequested(); lastEntryRequest = now;
                }
                if (reader.Registered && reader.NeedsEntryList && now - lastEntryRequest > 1000) {
                    Send(10); Send(11); reader.EntryListRequested(); lastEntryRequest = now;
                }
            }
            if (socket is not null && now - lastMessage > 5000) {
                ResetSocket(); nextAttempt = now + 1000; Status = "Карта ACC: нет ответов Broadcasting. Запустите заезд после включения канала.";
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException or DecoderFallbackException or SocketException or UnauthorizedAccessException or InvalidOperationException)
        {
            Status = e is InvalidDataException ? "Карта ACC: " + e.Message : "Карта ACC: канал недоступен. Проверьте broadcasting.json и запустите заезд.";
            ResetSocket(); nextAttempt = now + 5000;
        }
        var race = reader.Snapshot(playerId);
        if (race.TrackId >= 0) lastRace = race;
        else if (lastRace is not null) race = lastRace with { Fresh = false, Drivers = lastRace.Drivers.Select(d => d with { Fresh = false }).ToArray() };
        if (race.Fresh) Status = $"Карта ACC: {race.Drivers.Length} машин · Broadcasting 5 Hz.";
        return race;
    }
    void Send(int type) { var b = AccRaceReader.Request(type, reader.ConnectionId); socket!.Send(b, b.Length); }
    void ResetSocket() { socket?.Dispose(); socket = null; reader.Reset(); }
    public void Reset() { ResetSocket(); lastRace = null; nextAttempt = 0; Status = "Карта ACC: ожидание Broadcasting."; }
    public void Dispose() => ResetSocket();
}
