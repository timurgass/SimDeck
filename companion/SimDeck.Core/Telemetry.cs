using System.Buffers.Binary;

namespace SimDeck.Core;

public sealed record Telemetry(double SpeedMps, double Rpm, int Gear, double? FuelFraction,
    double Throttle, double Brake, double Clutch, double? MaxRpm = null, double? FuelLiters = null,
    string? GearboxMode = null, int? MaxGear = null, int? Headlights = null,
    IReadOnlyDictionary<string, bool>? ActionStates = null, F1Details? F1 = null)
{
    public string GearDisplay => Gear < 0 ? "R" : Gear == 0 ? "N" : GearboxMode == "arcade" ? "D" : Gear.ToString();
}

public static class SimDeckParser
{
    public static bool TryParse(ReadOnlySpan<byte> b, out Telemetry? result)
    {
        result = null;
        var v2 = b.Length == 60 && b[..4].SequenceEqual("SMD2"u8) && BinaryPrimitives.ReadUInt32LittleEndian(b[4..]) == 2;
        var v1 = b.Length == 48 && b[..4].SequenceEqual("SMD1"u8) && BinaryPrimitives.ReadUInt32LittleEndian(b[4..]) == 1;
        if (!v1 && !v2) return false;
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
        if (v2)
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
        result = new(speed, rpm, gear, fuel, throttle, brake, clutch, maxRpm > 0 ? maxRpm : null, null,
            mode == 1 ? "arcade" : mode == 2 ? "realistic" : null, maxGear > 0 ? maxGear : null, headlights, states);
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
    public void Reset(string newSource)
    {
        lock (gate) { source = newSource; latest = null; receivedAt = 0; sequence = 0; streamId = Guid.NewGuid().ToString("N"); }
    }
    public void Publish(Telemetry data)
    {
        lock (gate) { latest = data; receivedAt = Environment.TickCount64; sequence++; }
    }
    public object Snapshot(string sessionId)
    {
        lock (gate) return new { protocolMajor = 1, type = "telemetry.snapshot", sessionId, streamId, sequence,
            serverMonotonicMs = Environment.TickCount64, source, gameId = source,
            ageMs = latest is null ? (long?)null : Math.Max(0, Environment.TickCount64 - receivedAt), data = latest };
    }
    public (Telemetry? Data, long Age, string Source) Read()
    {
        lock (gate) return (latest, latest is null ? long.MaxValue : Math.Max(0, Environment.TickCount64 - receivedAt), source);
    }
}
