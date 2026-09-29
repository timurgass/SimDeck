using System.Buffers.Binary;

namespace SimDeck.Core;

// RenCloud scs-sdk-plugin revision 12, Local\SCSTelemetry, x64 shared-memory layout.
// Offsets follow SCSSdkConvert.cs in its MIT-licensed C# client.
public static class ScsTelemetryParser
{
    public const int SnapshotSize = 2048;
    public const int PluginRevision = 12;

    public static bool TryParse(ReadOnlySpan<byte> data, out ulong timestamp, out Telemetry? telemetry)
    {
        timestamp = 0;
        telemetry = null;
        if (data.Length < SnapshotSize || data[0] != 1 || data[4] != 0 || U(data, 40) != PluginRevision || U(data, 52) != 1)
            return false;

        timestamp = BinaryPrimitives.ReadUInt64LittleEndian(data[8..]);
        var speed = F(data, 948);
        var rpm = F(data, 952);
        var maxRpm = F(data, 740);
        var fuelCapacity = F(data, 704);
        var fuelLiters = F(data, 1000);
        var throttle = F(data, 976);
        var brake = F(data, 980);
        var clutch = F(data, 984);
        var gear = I(data, 504);
        var forwardGears = U(data, 68);
        if (timestamp == 0 || !Finite(speed, -200, 200) || !Finite(rpm, 0, 15000) ||
            !Finite(maxRpm, 500, 15000) || !Finite(fuelCapacity, 1, 5000) ||
            !Finite(fuelLiters, 0, 5500) || !Fraction(throttle) || !Fraction(brake) || !Fraction(clutch) ||
            gear is < -32 or > 64 || forwardGears is < 1 or > 64)
            return false;

        var lowBeam = On(data, 1583);
        var highBeam = On(data, 1584);
        var states = new Dictionary<string, bool>
        {
            ["etsEngine"] = On(data, 1576),
            ["etsParkingBrake"] = On(data, 1566),
            ["etsDifferential"] = On(data, 1608),
            ["etsLiftAxle"] = On(data, 1609),
            ["etsLights"] = lowBeam || highBeam,
            ["etsHighBeam"] = highBeam,
            ["etsBeacon"] = On(data, 1585),
            ["etsHazards"] = On(data, 1588),
            ["etsLeftSignal"] = On(data, 1578),
            ["etsRightSignal"] = On(data, 1579),
            ["etsWipers"] = On(data, 1577),
            ["etsCruise"] = On(data, 1589)
        };
        var remainingMetres = F(data, 1060);
        var remainingSeconds = F(data, 1064);
        var speedLimitMps = F(data, 1068);
        var navigation = new Ets2Navigation(
            Finite(remainingMetres, 0, 10_000_000) && remainingMetres > 0 ? remainingMetres / 1000.0 : null,
            Finite(remainingSeconds, 0, 10_000_000) && remainingSeconds > 0 ? remainingSeconds / 60.0 : null,
            Finite(speedLimitMps, 0, 100) && speedLimitMps > 0 ? speedLimitMps * 3.6 : null);
        telemetry = new(Math.Abs(speed), rpm, gear, Math.Clamp(fuelLiters / fuelCapacity, 0, 1),
            throttle, brake, clutch, maxRpm, fuelLiters, "realistic", (int)forwardGears,
            highBeam ? 2 : lowBeam ? 1 : 0, states, Ets2Navigation: navigation);
        return true;
    }

    static int I(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    static uint U(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    static float F(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
    static bool On(ReadOnlySpan<byte> data, int offset) => data[offset] != 0;
    static bool Finite(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
    static bool Fraction(float value) => Finite(value, 0, 1);
}
