namespace SimDeck.Core;

// Wheel order is RL, RR, FL, FR throughout the wire protocol (EA F1 24).
public sealed record F1Wheel(int Surface, int Inner, int Brake, double Pressure, double? Wear, int? Damage);
public sealed record TrackPoint(double X, double Y);
public sealed record F1Details(F1Wheel[] Wheels, int EngineTemperature,
    IReadOnlyDictionary<string, double> Values, TrackPoint[] Trail, TrackPoint? Position, F1Race? Race = null, int? MfdPanelIndex = null);

internal sealed class F1DetailCache
{
    readonly Dictionary<byte, (long Time, Dictionary<string, double> Values)> packets = new();
    readonly List<TrackPoint> trail = new();
    TrackPoint? position;
    long motionAt = long.MinValue;
    public void Set(byte id, long time, Dictionary<string, double> values) => packets[id] = (time, values);
    public void Motion(long time, double x, double y)
    {
        var p = new TrackPoint(Math.Round(x, 1), Math.Round(y, 1));
        if (position is not null && Distance(position, p) > 300) trail.Clear();
        position = p; motionAt = time;
        if (trail.Count == 0 || Distance(trail[^1], p) >= 12) trail.Add(p);
        // Keep the whole driven outline, progressively simplifying instead of dropping its start.
        if (trail.Count > 256) { var reduced = trail.Where((_, i) => i % 2 == 0).ToArray(); trail.Clear(); trail.AddRange(reduced); }
    }
    static double Distance(TrackPoint a, TrackPoint b) => Math.Sqrt(Math.Pow(a.X-b.X, 2)+Math.Pow(a.Y-b.Y, 2));
    public F1Details Snapshot(long now, ReadOnlySpan<byte> c)
    {
        var values = new Dictionary<string, double>();
        foreach (var (id, entry) in packets)
            if (now - entry.Time < (id == 1 ? 2500 : id == 7 ? 500 : 1500))
                foreach (var item in entry.Values) values[item.Key] = item.Value;
        double? V(string key) => values.TryGetValue(key, out var value) ? value : null;
        var wheels = new F1Wheel[4];
        for (var i = 0; i < 4; i++) wheels[i] = new(c[30+i], c[34+i],
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(c[(22+i*2)..]),
            System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(c[(40+i*4)..]), V($"wear{i}"), (int?)V($"tyreDamage{i}"));
        return new(wheels, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(c[38..]), values,
            trail.ToArray(), now-motionAt >= 0 && now-motionAt < 500 ? position : null);
    }
}
