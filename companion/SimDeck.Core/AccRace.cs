using System.IO;
using System.Text;

namespace SimDeck.Core;

public sealed record AccTrackPoint(int Bin, double X, double Z);
public sealed record AccDriver(int Index, string Name, string ShortName, string Team, int Number,
    int Model, int Cup, int Position, int CupPosition, int Lap, double Spline, double X, double Z,
    int Location, int? BestLapMs, int? LastLapMs, int? GapAheadMs, int? GapLeaderMs, bool Player, bool Fresh);
public sealed record AccRace(string Track, int TrackId, int TrackLength, int SessionType, int Phase,
    double? RemainingSeconds, bool Fresh, bool Replay, AccTrackPoint[] Points, int Bins, AccDriver[] Drivers);

// Independent decoder of the Kunos Broadcasting SDK v4 wire protocol. UDP messages
// never refresh the driving telemetry clock. Gaps are calculated passage-time estimates.
public sealed class AccRaceReader(Func<long>? clock = null)
{
    readonly Func<long> now = clock ?? (() => Environment.TickCount64);
    readonly Dictionary<int, Entry> entries = new();
    readonly Dictionary<int, Car> cars = new();
    readonly AccTrackPoint?[] points = new AccTrackPoint?[256];
    readonly double[] pointErrors = Enumerable.Repeat(double.MaxValue, 256).ToArray();
    string track = "";
    int trackId = -1, trackLength, sessionType, phase;
    (int Event, int Session)? session;
    double? remaining;
    bool replay;
    long sessionAt = long.MinValue;
    public int ConnectionId { get; private set; } = -1;
    public bool Registered { get; private set; }
    public bool NeedsEntryList { get; private set; }
    public string RegistrationError { get; private set; } = "";
    record Entry(string Team, int Number, int Model, int Cup, string[] Names, string[] Shorts);
    record Sample(double Progress, long At);
    record Car(int Index, int Driver, int Position, int CupPosition, int Lap, double Spline,
        double X, double Z, int Location, int? Best, int? Last, long At, List<Sample> History);

    public void Reset()
    {
        ConnectionId = -1; Registered = false; RegistrationError = ""; NeedsEntryList = false;
        entries.Clear(); cars.Clear(); ClearTrack(); track = ""; trackId = -1; trackLength = 0;
        session = null; sessionAt = long.MinValue; remaining = null; replay = false;
    }
    void ClearTrack() { Array.Clear(points); Array.Fill(pointErrors, double.MaxValue); }
    public void EntryListRequested() => NeedsEntryList = false;
    public static byte[] Register(string password) => Write(w => {
        w.Write((byte)1); w.Write((byte)4); Text(w, "SimDeck"); Text(w, password);
        w.Write(200); Text(w, ""); // No command password; no camera/HUD/input commands.
    });
    public static byte[] Request(int type, int connectionId)
    {
        if (type is not (10 or 11)) throw new ArgumentOutOfRangeException(nameof(type));
        return Write(w => { w.Write((byte)type); w.Write(connectionId); });
    }
    static byte[] Write(Action<BinaryWriter> write)
    {
        using var m = new MemoryStream(); using var w = new BinaryWriter(m, Encoding.UTF8, true);
        write(w); return m.ToArray();
    }
    static void Text(BinaryWriter w, string value)
    {
        var b = Encoding.UTF8.GetBytes(value); if (b.Length > 4096) throw new ArgumentException("String too long");
        w.Write((ushort)b.Length); w.Write(b);
    }
    static string Text(BinaryReader r)
    {
        var n = r.ReadUInt16(); if (n > 4096) throw new InvalidDataException();
        var bytes = r.ReadBytes(n); if (bytes.Length != n) throw new EndOfStreamException();
        return new string(new UTF8Encoding(false, true).GetString(bytes).Where(c => !char.IsControl(c)).ToArray()).Trim();
    }
    static int? Lap(BinaryReader r)
    {
        var time = r.ReadInt32(); r.ReadUInt16(); r.ReadUInt16(); var splits = r.ReadByte();
        if (splits > 10) throw new InvalidDataException();
        for (var i = 0; i < splits; i++) r.ReadInt32();
        for (var i = 0; i < 4; i++) r.ReadByte();
        return time is > 0 and < 86400000 ? time : null;
    }
    static double F(BinaryReader r, double min, double max)
    {
        var x = r.ReadSingle(); if (!float.IsFinite(x) || x < min || x > max) throw new InvalidDataException();
        return x;
    }
    public bool TryRead(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < 1 or > 65507) return false;
        try
        {
            using var m = new MemoryStream(packet.ToArray()); using var r = new BinaryReader(m);
            var type = r.ReadByte(); var at = now();
            switch (type)
            {
                case 1:
                    var id = r.ReadInt32(); var success = r.ReadByte() != 0; r.ReadByte(); var error = Text(r);
                    ConnectionId = id; Registered = success && id >= 0; RegistrationError = error; return true;
                case 4:
                    if (!Registered || r.ReadInt32() != ConnectionId) return false;
                    var n = r.ReadUInt16(); if (n > 100) return false;
                    var ids = new HashSet<int>(); for (var i = 0; i < n; i++) if (!ids.Add(r.ReadUInt16())) return false;
                    foreach (var old in entries.Keys.Where(x => !ids.Contains(x)).ToArray()) entries.Remove(old);
                    foreach (var old in cars.Keys.Where(x => !ids.Contains(x)).ToArray()) cars.Remove(old);
                    return true;
                case 6:
                    if (!Registered) return false;
                    var index = r.ReadUInt16(); var model = r.ReadByte(); var team = Text(r);
                    var number = r.ReadInt32(); var cup = r.ReadByte(); r.ReadByte(); r.ReadUInt16();
                    var drivers = r.ReadByte(); if (drivers is < 1 or > 16 || number < 0 || number > 99999 || cup > 4) return false;
                    var names = new string[drivers]; var shorts = new string[drivers];
                    for (var i = 0; i < drivers; i++) {
                        var first = Text(r); var surname = Text(r); shorts[i] = Text(r); r.ReadByte(); r.ReadUInt16();
                        names[i] = (first + " " + surname).Trim();
                    }
                    if (!entries.ContainsKey(index) && entries.Count >= 100) return false;
                    entries[index] = new(team, number, model, cup, names, shorts); return true;
                case 5:
                    if (!Registered || r.ReadInt32() != ConnectionId) return false;
                    var name = Text(r); var tid = r.ReadInt32(); var metres = r.ReadInt32();
                    if (name.Length is < 1 or > 120 || tid < 0 || metres is < 100 or > 100000) return false;
                    var sets = r.ReadByte();
                    for (var i = 0; i < sets; i++) { Text(r); var count = r.ReadByte(); for (var j = 0; j < count; j++) Text(r); }
                    var hud = r.ReadByte(); for (var i = 0; i < hud; i++) Text(r);
                    if (name != track || tid != trackId || metres != trackLength) { cars.Clear(); ClearTrack(); }
                    track = name; trackId = tid; trackLength = metres; return true;
                case 2:
                    if (!Registered) return false;
                    var ev = r.ReadUInt16(); var sid = r.ReadUInt16(); var st = r.ReadByte(); var ph = r.ReadByte();
                    var elapsed = F(r, -86400000, 86400000); var end = F(r, -86400000, 86400000);
                    r.ReadInt32(); Text(r); Text(r); Text(r); var rp = r.ReadByte() != 0;
                    if (rp) { F(r, -86400000, 86400000); F(r, -86400000, 86400000); }
                    F(r, -86400000, 86400000); r.ReadByte(); r.ReadByte(); r.ReadByte(); r.ReadByte(); r.ReadByte(); Lap(r);
                    if (session != (ev, sid) || replay != rp || (phase >= 5 && ph < 5)) {
                        cars.Clear(); NeedsEntryList = true;
                    }
                    session = (ev, sid); sessionType = st; phase = ph; replay = rp;
                    remaining = end > 0 ? Math.Max(0, end - elapsed) / 1000 : null; sessionAt = at; return true;
                case 3:
                    if (!Registered) return false;
                    var ci = r.ReadUInt16(); var di = r.ReadUInt16(); var driverCount = r.ReadByte(); r.ReadByte();
                    var x = F(r, -100000, 100000); var z = F(r, -100000, 100000); F(r, -1000, 1000);
                    var loc = r.ReadByte(); var speed = r.ReadUInt16(); var pos = r.ReadUInt16(); var cp = r.ReadUInt16(); r.ReadUInt16();
                    var spline = F(r, 0, 1); var laps = r.ReadUInt16(); r.ReadInt32();
                    var best = Lap(r); var last = Lap(r); Lap(r);
                    if (loc > 4 || pos > 100 || cp > 100 || speed > 1000 || driverCount > 16 || di >= driverCount) return false;
                    if (!entries.TryGetValue(ci, out var entry) || entry.Names.Length != driverCount) NeedsEntryList = true;
                    if (!cars.TryGetValue(ci, out var previous) && cars.Count >= 100) return false;
                    var history = previous?.History ?? new List<Sample>(); var progress = laps + spline;
                    if (history.Count > 0 && (progress < history[^1].Progress - .01 || progress > history[^1].Progress + .2 || at - history[^1].At > 2000)) history.Clear();
                    if (!replay && loc == 1 && speed > 5) {
                        if (history.Count == 0 || progress > history[^1].Progress + .00001) history.Add(new(progress, at));
                        while (history.Count > 1800 || (history.Count > 0 && at - history[0].At > 300000)) history.RemoveAt(0);
                        var bin = Math.Min(255, (int)(spline * 256)); var distance = Math.Abs(spline * 256 - (bin + .5));
                        if (trackLength > 0 && distance < pointErrors[bin]) { points[bin] = new(bin, x, z); pointErrors[bin] = distance; }
                    }
                    else history.Clear();
                    cars[ci] = new(ci, di, pos, cp, laps, spline, x, z, loc, best, last, at, history); return true;
                case 7: // Broadcast events do not contain driving telemetry.
                    r.ReadByte(); Text(r); r.ReadInt32(); r.ReadInt32(); return true;
                default: return false;
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or DecoderFallbackException or ArgumentException) { return false; }
    }
    int? Gap(Car ahead, Car behind, long at)
    {
        if (sessionType != 10 || replay || phase != 5 || ahead.Location != 1 || behind.Location != 1 ||
            at - ahead.At > 1500 || at - behind.At > 1500) return null;
        var p = behind.Lap + behind.Spline; var history = ahead.History;
        for (var i = history.Count - 1; i > 0; i--) {
            var a = history[i - 1]; var b = history[i];
            if (p < a.Progress || p > b.Progress || b.Progress <= a.Progress || b.At - a.At > 1000) continue;
            var passed = a.At + (p - a.Progress) / (b.Progress - a.Progress) * (b.At - a.At);
            var gap = behind.At - passed; return gap is >= 0 and < 300000 ? (int)Math.Round(gap) : null;
        }
        return null;
    }
    public AccRace Snapshot(int? playerId)
    {
        var at = now(); var fresh = sessionAt != long.MinValue && at - sessionAt is >= 0 and < 1500 && !replay;
        var ordered = cars.Values.Where(c => at - c.At < 10000).OrderBy(c => c.Position == 0 ? 999 : c.Position).ThenBy(c => c.Index).ToArray();
        var result = ordered.Select((c, i) => {
            entries.TryGetValue(c.Index, out var e);
            var name = e is not null && c.Driver < e.Names.Length ? e.Names[c.Driver] : "";
            var shortName = e is not null && c.Driver < e.Shorts.Length ? e.Shorts[c.Driver] : "";
            return new AccDriver(c.Index, name.Length > 0 ? name[..Math.Min(name.Length, 160)] : $"Машина #{e?.Number ?? c.Index}", shortName[..Math.Min(shortName.Length, 16)],
                e?.Team is {} team ? team[..Math.Min(team.Length, 160)] : "", e?.Number ?? 0, e?.Model ?? -1, e?.Cup ?? -1, c.Position, c.CupPosition, c.Lap,
                c.Spline, c.X, c.Z, c.Location, c.Best, c.Last,
                i > 0 ? Gap(ordered[i - 1], c, at) : null, i > 0 ? Gap(ordered[0], c, at) : 0,
                playerId == c.Index, fresh && at - c.At < 1500);
        }).ToArray();
        return new(track, trackId, trackLength, sessionType, phase, remaining, fresh, replay,
            points.OfType<AccTrackPoint>().ToArray(), points.Length, result);
    }
}
