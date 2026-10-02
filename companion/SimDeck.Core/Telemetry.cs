using System.Buffers.Binary;

namespace SimDeck.Core;

public sealed record Ets2Navigation(double? RemainingKm, double? RemainingMinutes, double? SpeedLimitKmh);

public sealed record Telemetry(double SpeedMps, double Rpm, int Gear, double? FuelFraction,
    double Throttle, double Brake, double Clutch, double? MaxRpm = null, double? FuelLiters = null,
    string? GearboxMode = null, int? MaxGear = null, int? Headlights = null,
    IReadOnlyDictionary<string, bool>? ActionStates = null, F1Details? F1 = null, AccDetails? Acc = null,
    Ets2Navigation? Ets2Navigation = null, Fs25Details? Fs25 = null,
    Fs25AdvisorReport? Fs25Advisor = null, VehicleInfo? Vehicle = null)
{
    public string GearDisplay => Gear < 0 ? "R" : Gear == 0 ? "N" : GearboxMode == "arcade" ? "D" : Gear.ToString();
}

public static class SimDeckParser
{
    public static bool TryParse(ReadOnlySpan<byte> b, out Telemetry? result)
    {
        result = null;
        var v3 = b.Length == 448 && b[..4].SequenceEqual("SMD3"u8) && BinaryPrimitives.ReadUInt32LittleEndian(b[4..]) == 3;
        var v2 = b.Length == 60 && b[..4].SequenceEqual("SMD2"u8) && BinaryPrimitives.ReadUInt32LittleEndian(b[4..]) == 2;
        var v1 = b.Length == 48 && b[..4].SequenceEqual("SMD1"u8) && BinaryPrimitives.ReadUInt32LittleEndian(b[4..]) == 1;
        if (!v1 && !v2 && !v3) return false;
        var speed = BinaryPrimitives.ReadSingleLittleEndian(b[8..]);
        var rpm = BinaryPrimitives.ReadSingleLittleEndian(b[12..]);
        var gear = BinaryPrimitives.ReadInt32LittleEndian(b[16..]);
        var mode = BinaryPrimitives.ReadUInt32LittleEndian(b[20..]);
        var fuel = BinaryPrimitives.ReadSingleLittleEndian(b[24..]);
        var throttle = BinaryPrimitives.ReadSingleLittleEndian(b[28..]);
        var brake = BinaryPrimitives.ReadSingleLittleEndian(b[32..]);
        var clutch = BinaryPrimitives.ReadSingleLittleEndian(b[36..]);
        var maxRpm = BinaryPrimitives.ReadSingleLittleEndian(b[40..]);
        var maxGear = BinaryPrimitives.ReadInt32LittleEndian(b[44..]);
        if (!float.IsFinite(speed) || speed is < 0 or > 2000 || !float.IsFinite(rpm) || rpm is < 0 or > 100000 ||
            gear is < -32 or > 128 || mode > 2 || !Fraction(fuel) || !Fraction(throttle) || !Fraction(brake) || !Fraction(clutch) ||
            !float.IsFinite(maxRpm) || maxRpm is < 0 or > 100000 || maxGear is < 0 or > 128) return false;
        int? headlights = null;
        Dictionary<string, bool>? states = null;
        if (v2 || v3)
        {
            var known = BinaryPrimitives.ReadUInt32LittleEndian(b[48..]);
            var active = BinaryPrimitives.ReadUInt32LittleEndian(b[52..]);
            var light = BinaryPrimitives.ReadInt32LittleEndian(b[56..]);
            if (light is < -1 or > 2 || (active & ~known) != 0) return false;
            headlights = light >= 0 ? light : null;
            string[] actions = ["hazards", "leftSignal", "rightSignal", "fogLights", "fourWheelDrive", "range", "differentials", "couplers", "ignition", "lightbar"];
            states = new();
            for (var i = 0; i < actions.Length; i++)
                if ((known & (1u << i)) != 0) states[actions[i]] = (active & (1u << i)) != 0;
        }
        VehicleInfo? vehicle = null;
        if (v3)
        {
            var count = BinaryPrimitives.ReadUInt32LittleEndian(b[252..]);
            if (count > 16) return false;
            var wheelList = new List<VehicleWheel>();
            for (var i = 0; i < count; i++) {
                var offset = 256 + i * 12;
                var x = BinaryPrimitives.ReadSingleLittleEndian(b[offset..]);
                var z = BinaryPrimitives.ReadSingleLittleEndian(b[(offset + 4)..]);
                var flags = BinaryPrimitives.ReadUInt32LittleEndian(b[(offset + 8)..]);
                if (!float.IsFinite(x) || !float.IsFinite(z) || Math.Abs(x) > 100 || Math.Abs(z) > 100 || flags > 3 || flags == 2) return false;
                wheelList.Add(new(x, z, (flags & 1) != 0 ? (flags & 2) != 0 : null));
            }
            var id = VehicleKinds.Text(b.Slice(60, 64));
            vehicle = new(id, VehicleKinds.Text(b.Slice(124, 96)), VehicleKinds.Resolve(VehicleKinds.Text(b.Slice(220, 32))), wheelList, []);
        }
        result = new(speed, rpm, gear, fuel, throttle, brake, clutch, maxRpm > 0 ? maxRpm : null, null,
            mode == 1 ? "arcade" : mode == 2 ? "realistic" : null, maxGear > 0 ? maxGear : null, headlights, states, Vehicle: vehicle);
        return true;
    }
    static bool Fraction(float x) => float.IsFinite(x) && x is >= 0 and <= 1;
}

public static class OutGaugeParser
{
    // BeamNG OutGauge: little-endian, 92 bytes plus optional 4-byte ID.
    public static bool TryParse(ReadOnlySpan<byte> bytes, out Telemetry? result)
    {
        result = null;
        if (bytes.Length is not (92 or 96) || !bytes.Slice(4, 4).SequenceEqual("beam"u8)) return false;
        var speed = ReadFloat(bytes, 12);
        var rpm = ReadFloat(bytes, 16);
        var fuel = ReadFloat(bytes, 28);
        var throttle = ReadFloat(bytes, 48);
        var brake = ReadFloat(bytes, 52);
        var clutch = ReadFloat(bytes, 56);
        if (!float.IsFinite(speed) || speed < 0 || speed > 2000 ||
            !float.IsFinite(rpm) || rpm < 0 || rpm > 100000 || bytes[10] > 32 ||
            !Fraction(fuel) || !Fraction(throttle) || !Fraction(brake) || !Fraction(clutch)) return false;
        result = new(speed, rpm, bytes[10] - 1, fuel, throttle, brake, clutch);
        return true;
    }
    static float ReadFloat(ReadOnlySpan<byte> b, int offset) => BinaryPrimitives.ReadSingleLittleEndian(b[offset..]);
    static bool Fraction(float x) => float.IsFinite(x) && x is >= 0 and <= 1;
}

public sealed class TelemetryHub
{
    readonly object gate = new();
    Telemetry? latest;
    long receivedAt;
    long sequence;
    string source = "beamng";
    string streamId = Guid.NewGuid().ToString("N");
    IReadOnlyDictionary<string, bool>? fs25LiveStates;
    VehicleInfo? fs25Vehicle;
    long fs25LiveAt;
    public void Reset(string newSource)
    {
        lock (gate) { source = newSource; latest = null; receivedAt = 0; fs25LiveStates = null; fs25Vehicle = null; fs25LiveAt = 0; sequence = 0; streamId = Guid.NewGuid().ToString("N"); }
    }
    public void Publish(Telemetry data)
    {
        lock (gate) { latest = data; receivedAt = Environment.TickCount64; sequence++; }
    }
    public void PublishFs25Live(IReadOnlyDictionary<string, bool> states, VehicleInfo? vehicle = null)
    {
        lock (gate)
        {
            if (source != "fs25") return;
            fs25LiveStates = new Dictionary<string, bool>(states);
            fs25Vehicle = vehicle;
            fs25LiveAt = Environment.TickCount64;
            sequence++;
        }
    }
    public void ClearFs25Live()
    {
        lock (gate) { if (fs25LiveStates is not null) { fs25LiveStates = null; fs25Vehicle = null; fs25LiveAt = 0; sequence++; } }
    }
    public bool HasFs25Live
    {
        get { lock (gate) return source == "fs25" && fs25LiveStates is not null && Environment.TickCount64 - fs25LiveAt < 1500; }
    }
    (Telemetry? Data, long Age) Current()
    {
        var now = Environment.TickCount64;
        if (source == "fs25" && fs25LiveStates is not null && now - fs25LiveAt < 1500)
            return ((latest ?? new Telemetry(0, 0, 0, null, 0, 0, 0)) with { ActionStates = fs25LiveStates, Vehicle = fs25Vehicle }, now - fs25LiveAt);
        return (latest, latest is null ? long.MaxValue : Math.Max(0, now - receivedAt));
    }
    public object Snapshot(string sessionId)
    {
        lock (gate) { var current = Current(); return new { protocolMajor = 1, type = "telemetry.snapshot", sessionId, streamId, sequence,
            serverMonotonicMs = Environment.TickCount64, source, gameId = source,
            ageMs = current.Data is null ? (long?)null : current.Age, data = current.Data }; }
    }
    public (Telemetry? Data, long Age, string Source) Read()
    {
        lock (gate) { var current = Current(); return (current.Data, current.Age, source); }
    }
}
