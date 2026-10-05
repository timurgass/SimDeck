using System.IO;
using System.Text;
using SimDeck.Core;

static class AccRaceTests
{
    static byte[] Packet(int type, Action<BinaryWriter> body) { using var m = new MemoryStream(); using var w = new BinaryWriter(m); w.Write((byte)type); body(w); return m.ToArray(); }
    static void Text(BinaryWriter w, string s) { var b = Encoding.UTF8.GetBytes(s); w.Write((ushort)b.Length); w.Write(b); }
    static void Lap(BinaryWriter w, int time) { w.Write(time); w.Write((ushort)1); w.Write((ushort)0); w.Write((byte)3); w.Write(30000); w.Write(32000); w.Write(33000); w.Write(new byte[4]); }
    static byte[] Session(int session = 1, bool replay = false, int type = 10) => Packet(2, w => {
        w.Write((ushort)1); w.Write((ushort)session); w.Write((byte)type); w.Write((byte)5); w.Write(10000f); w.Write(610000f);
        w.Write(99); Text(w, "TV"); Text(w, "Helicam"); Text(w, ""); w.Write((byte)(replay ? 1 : 0));
        if (replay) { w.Write(10f); w.Write(20f); } w.Write(12000000f); w.Write(new byte[] { 22, 31, 0, 0, 0 }); Lap(w, 95000);
    });
    static byte[] Track(string name = "monza", int id = 1) => Packet(5, w => { w.Write(7); Text(w, name); w.Write(id); w.Write(5793); w.Write((byte)0); w.Write((byte)0); });
    static byte[] Entry(int index, string first, string last) => Packet(6, w => {
        w.Write((ushort)index); w.Write((byte)7); Text(w, "Team"); w.Write(index * 10); w.Write((byte)3); w.Write((byte)0); w.Write((ushort)0); w.Write((byte)1);
        Text(w, first); Text(w, last); Text(w, last[..Math.Min(3, last.Length)]); w.Write((byte)2); w.Write((ushort)0);
    });
    static byte[] Car(int id, int position, float spline, int driver = 0, int location = 1) => Packet(3, w => {
        w.Write((ushort)id); w.Write((ushort)driver); w.Write((byte)1); w.Write((byte)5);
        w.Write(spline * 1000); w.Write(30f); w.Write(0f); w.Write((byte)location); w.Write((ushort)150);
        w.Write((ushort)position); w.Write((ushort)position); w.Write((ushort)position); w.Write(spline); w.Write((ushort)2); w.Write(123);
        Lap(w, 95000); Lap(w, 96000); Lap(w, 10000);
    });
    public static void Run(Action<bool,string> check)
    {
        long now = 1000; var r = new AccRaceReader(() => now);
        var registered = Packet(1, w => { w.Write(7); w.Write((byte)1); w.Write((byte)0); Text(w, ""); });
        check(AccRaceReader.Register("test")[1] == 4 && AccRaceReader.Request(10, 7).SequenceEqual(new byte[] { 10, 7, 0, 0, 0 }), "ACC uses SDK v4 and read-only entry/track requests");
        check(!r.TryRead(Car(1, 1, .4f)), "ACC ignores unregistered race packets");
        check(r.TryRead(registered) && r.Registered && r.ConnectionId == 7 && r.TryRead(Track()) && r.TryRead(Session()), "ACC registers and parses track/session identity");
        r.TryRead(Packet(4, w => { w.Write(7); w.Write((ushort)2); w.Write((ushort)1); w.Write((ushort)2); }));
        r.TryRead(Entry(1, "Leader", "Driver")); r.TryRead(Entry(2, "Игрок", "Тест"));
        r.TryRead(Car(1, 1, .4f)); r.TryRead(Car(2, 2, .3f));
        now = 2000; r.TryRead(Car(1, 1, .5f)); r.TryRead(Car(2, 2, .4f)); var race = r.Snapshot(2);
        check(race is { Track: "monza", TrackLength: 5793, Fresh: true, RemainingSeconds: 600 } && race.Drivers.Length == 2, "ACC exposes live race with remaining time");
        check(race.Drivers[1].Name == "Игрок Тест" && race.Drivers[1].Player && !race.Drivers[0].Player && race.Drivers[1].Cup == 3 && race.Drivers[1].LastLapMs == 96000, "ACC player uses shared-memory car ID, never spectator focus; UTF-8 names and lap times preserved");
        check(race.Drivers[1].GapAheadMs is >= 990 and <= 1010, "ACC estimates gaps from actual same-point passage history");
        var count = race.Points.Length;
        r.TryRead(Car(2, 2, .6f, location: 2));
        check(r.Snapshot(2).Points.Length == count && r.Snapshot(2).Drivers[1].GapAheadMs is null, "ACC excludes pit lane from track learning and gap estimates");
        var before = r.Snapshot(2); var packet = Car(1, 1, .7f);
        check(Enumerable.Range(1, packet.Length - 1).All(n => !r.TryRead(packet.AsSpan(0, n))) && r.Snapshot(2).Points.SequenceEqual(before.Points), "ACC rejects every truncated car packet without partial map mutation");
        check(!r.TryRead(Car(1, 1, float.NaN)) && !r.TryRead(Car(1, 1, .5f, driver: 2)), "ACC rejects invalid coordinates and driver indexes");
        now = 6000; race = r.Snapshot(2);
        check(!race.Fresh && race.Drivers.All(d => !d.Fresh) && race.Points.Length == count, "ACC retains last map while marking stopped telemetry stale");
        r.TryRead(Session(2)); check(r.Snapshot(2).Drivers.Length == 0, "ACC clears previous opponents on a new session");
        r.TryRead(Car(1, 1, .8f)); r.TryRead(Session(2, replay: true)); r.TryRead(Car(1, 1, .9f));
        check(r.Snapshot(2).Replay && !r.Snapshot(2).Fresh && r.Snapshot(2).Points.Length == count + 1, "ACC replay does not extend learned track or become live driving data");
        r.TryRead(Track("spa", 2)); check(r.Snapshot(2).Points.Length == 0 && r.Snapshot(2).Drivers.Length == 0, "ACC track change cannot reuse old geometry or markers");
        r.Reset(); check(!r.Registered && r.Snapshot(2).TrackId == -1, "ACC reconnect resets session identity");
    }
}
