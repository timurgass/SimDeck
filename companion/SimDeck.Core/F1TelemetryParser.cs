using System.Buffers.Binary;

namespace SimDeck.Core;

// F1 24/25 UDP v1. Packed little-endian data. F1 25 changes the participants
// record and inserts tyre blisters in the damage record; the dashboard packets
// otherwise retain their F1 24 layout.
// Only packet 6 advances the dashboard clock; status packets cannot keep old speed alive.
public sealed class F1TelemetryParser
{
    ulong? session;
    byte player;
    readonly Dictionary<byte, uint> frames = new();
    readonly HashSet<ulong> retired = new();
    long statusAt;
    double? fuel, maxRpm;
    int? maxGear;
    bool limiter, ers;
    F1DetailCache details = new();
    F1RaceCache race = new();
    readonly Func<long> clock;
    public F1TelemetryParser(Func<long>? clock = null) => this.clock = clock ?? (() => Environment.TickCount64);
    public bool TryParse(ReadOnlySpan<byte> b, out Telemetry? result)
    {
        result = null;
        if (b.Length < 29) return false;
        var format = BinaryPrimitives.ReadUInt16LittleEndian(b);
        if (format is not (2024 or 2025) || (format == 2024 ? b[2] != 24 : b[2] is not (0 or 25)) || b[5] != 1 || b[27] >= 22 || b[6] > (format == 2025 ? 15 : 14)) return false;
        var id = b[6];
        var size = id switch { 0 => 1349, 1 => 753, 2 => 1285, 4 => format == 2025 ? 1284 : 1350, 5 => 1133, 6 => 1352, 7 => 1239, 10 => format == 2025 ? 1041 : 953, _ => 0 };
        if (size == 0) return true;
        if (b.Length != size) return false;
        var uid = BinaryPrimitives.ReadUInt64LittleEndian(b[7..]);
        if (retired.Contains(uid)) return true;
        if (session != uid || player != b[27])
        {
            if (session.HasValue && session != uid) { if (retired.Count >= 32) retired.Clear(); retired.Add(session.Value); }
            session = uid; player = b[27]; frames.Clear(); statusAt = 0; fuel = maxRpm = null; maxGear = null;
            details = new(); race = new();
        }
        var frame = BinaryPrimitives.ReadUInt32LittleEndian(b[23..]);
        if (frames.TryGetValue(id, out var previous) && unchecked((int)(frame - previous)) <= 0) return true;
        var stride = id switch { 0 or 6 => 60, 5 => 50, 7 => 55, 10 => format == 2025 ? 46 : 42, _ => 0 };
        var c = b[(29 + player * stride)..];
        var now = clock();
        var values = new Dictionary<string, double>();
        if (id is 2 or 4)
        {
            var valid = id == 2 ? race.Laps(b, now, player) : race.Participants(b, now, format);
            if (valid) frames[id] = frame;
            return valid;
        }
        if (id == 0)
        {
            var x = F(c, 0); var z = F(c, 8);
            if (!float.IsFinite(x) || !float.IsFinite(z) || Math.Abs(x) > 100000 || Math.Abs(z) > 100000) return false;
            details.Motion(now, x, z); frames[id] = frame; return true;
        }
        if (id == 1)
        {
            race.Session(c);
            values["trackId"] = unchecked((sbyte)c[7]); values["sessionType"] = c[6];
            values["trackTemperature"] = unchecked((sbyte)c[1]); values["airTemperature"] = unchecked((sbyte)c[2]);
        }
        if (id == 5)
        {
            var nextWing = F(b, 1129);
            if (!float.IsFinite(nextWing) || nextWing is < 0 or > 100 || c[0] > 100 || c[1] > 100 || c[2] > 100 || c[27] > 100) return false;
            values["frontWing"] = c[0]; values["rearWing"] = c[1]; values["differential"] = c[2];
            values["nextFrontWing"] = nextWing;
        }
        if (id == 10)
        {
            for (var i = 0; i < 4; i++)
            {
                var wear = F(c, i*4);
                if (!float.IsFinite(wear) || wear is < 0 or > 100 || c[16+i] > 100 || c[20+i] > 100) return false;
                values[$"wear{i}"] = wear; values[$"tyreDamage{i}"] = c[16+i]; values[$"brakeDamage{i}"] = c[20+i];
            }
            var damageOffset = 24;
            if (format == 2025) { for (var i = 0; i < 4; i++) { if (c[24+i] > 100) return false; values[$"tyreBlister{i}"] = c[24+i]; } damageOffset += 4; }
            string[] names = ["frontLeftWingDamage", "frontRightWingDamage", "rearWingDamage", "floorDamage", "diffuserDamage", "sidepodDamage", "drsFault", "ersFault", "gearboxDamage", "engineDamage", "mguHWear", "esWear", "ceWear", "iceWear", "mguKWear", "tcWear", "engineBlown", "engineSeized"];
            for (var i = 0; i < names.Length; i++) { if (c[damageOffset+i] > 100) return false; values[names[i]] = c[damageOffset+i]; }
        }
        if (id is 1 or 5 or 10) { details.Set(id, now, values); frames[id] = frame; return true; }
        if (id == 7)
        {
            var amount = F(c, 5); var capacity = F(c, 9);
            var rpmLimit = U(c, 17);
            // Live F1 24 career cars report maxGears=9. This field is not the
            // current forward gear; rejecting it discarded fuel and tyre status.
            if (!float.IsFinite(amount) || !float.IsFinite(capacity) || amount < 0 || capacity < 0 || amount > capacity || c[4] > 1 || c[21] > 9 || c[41] > 3) return false;
            fuel = capacity > 0 ? amount / capacity : null;
            maxRpm = rpmLimit > 0 ? rpmLimit : null; maxGear = c[21] > 0 ? c[21] : null;
            limiter = c[4] == 1; ers = c[41] == 3;
            values["compound"] = c[26]; values["tyreAge"] = c[27]; values["brakeBias"] = c[3];
            values["fuelMix"] = c[2]; values["ersMode"] = c[41];
            var energy = F(c, 37); if (float.IsFinite(energy) && energy >= 0) values["ersEnergy"] = energy;
            details.Set(id, now, values);
            statusAt = clock(); frames[id] = frame;
            return true;
        }
        var speed = U(c, 0); var throttle = F(c, 2); var brake = F(c, 10);
        var gear = unchecked((sbyte)c[15]);
        if (speed > 600 || !Fraction(throttle) || !Fraction(brake) || c[14] > 100 || gear is < -1 or > 8 || c[18] > 1) return false;
        for (var i = 0; i < 4; i++) if (!float.IsFinite(F(c, 40+i*4)) || F(c, 40+i*4) is < 0 or > 100) return false;
        var freshStatus = statusAt != 0 && clock() - statusAt < 500;
        var states = new Dictionary<string, bool> { ["drs"] = c[18] == 1 };
        if (freshStatus) { states["pitLimiter"] = limiter; states["ers"] = ers; }
        result = new(speed / 3.6, U(c, 16), gear, freshStatus ? fuel : null, throttle, brake, c[14] / 100.0,
            freshStatus ? maxRpm : null, MaxGear: freshStatus ? maxGear : null, ActionStates: states, F1: details.Snapshot(now, c) with { Race = race.Snapshot(now), MfdPanelIndex = b[1349] <= 4 ? b[1349] : null });
        frames[id] = frame;
        return true;
    }
    static ushort U(ReadOnlySpan<byte> b, int i) => BinaryPrimitives.ReadUInt16LittleEndian(b[i..]);
    static float F(ReadOnlySpan<byte> b, int i) => BinaryPrimitives.ReadSingleLittleEndian(b[i..]);
    static bool Fraction(float x) => float.IsFinite(x) && x is >= 0 and <= 1;
}
