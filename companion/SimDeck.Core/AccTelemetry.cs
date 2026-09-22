using System.Buffers.Binary;

namespace SimDeck.Core;

// ACC shared-memory tyre order: front-left, front-right, rear-left, rear-right.
public sealed record AccWheel(double Pressure, double CoreTemperature, double BrakeTemperature,
    double Wear, double PadLife, double DiscLife, double SuspensionDamage);

public sealed record AccDetails(AccWheel[] Wheels, double AirTemperature, double RoadTemperature,
    double WaterTemperature, double BrakeBias, bool PitLimiter, bool Ignition,
    bool Starter, bool EngineRunning);

public static class AccTelemetryParser
{
    public const int PhysicsSize = 800;
    public const int StaticSize = 420;

    public static bool TryParse(ReadOnlySpan<byte> physics, ReadOnlySpan<byte> staticInfo, out Telemetry? result)
    {
        result = null;
        if (physics.Length < PhysicsSize || staticInfo.Length < StaticSize) return false;

        var speedKmh = F(physics, 28);
        var rpm = I(physics, 20);
        var rawGear = I(physics, 16);
        var fuel = F(physics, 12);
        var maxFuel = F(staticInfo, 416);
        var maxRpm = I(physics, 588);
        if (maxRpm <= 0) maxRpm = I(staticInfo, 412);
        var gas = F(physics, 4);
        var brake = F(physics, 8);
        var clutch = F(physics, 364);
        if (!Finite(speedKmh, 0, 1000) || rpm is < 0 or > 30000 || rawGear is < 0 or > 20 ||
            !Finite(fuel, 0, 1000) || !Finite(maxFuel, 1, 2000) || maxRpm is < 1000 or > 30000 ||
            !Fraction(gas) || !Fraction(brake) || !Fraction(clutch)) return false;

        var wheels = new AccWheel[4];
        for (var i = 0; i < wheels.Length; i++)
        {
            var pressure = F(physics, 88 + i * 4);
            var wear = F(physics, 120 + i * 4);
            var core = F(physics, 152 + i * 4);
            var brakes = F(physics, 348 + i * 4);
            var suspension = F(physics, 680 + i * 4);
            var pad = F(physics, 740 + i * 4);
            var disc = F(physics, 756 + i * 4);
            if (!Finite(pressure, 0, 100) || !Finite(wear, 0, 100) || !Finite(core, -100, 500) ||
                !Finite(brakes, -100, 2000) || !Finite(suspension, 0, 100) ||
                !Finite(pad, 0, 100) || !Finite(disc, 0, 100)) return false;
            wheels[i] = new(pressure, core, brakes, wear * 100, pad, disc, suspension * 100);
        }

        var air = F(physics, 288);
        var road = F(physics, 292);
        var water = F(physics, 712);
        var bias = F(physics, 564);
        if (!Finite(air, -100, 100) || !Finite(road, -100, 150) || !Finite(water, -100, 300) || !Fraction(bias)) return false;

        var pitLimiter = I(physics, 248) != 0;
        var ignition = I(physics, 772) != 0;
        var starter = I(physics, 776) != 0;
        var engine = I(physics, 780) != 0;
        var states = new Dictionary<string, bool>
        {
            ["accPitLimiter"] = pitLimiter,
            ["accIgnition"] = ignition,
            ["accStarter"] = starter
        };
        var details = new AccDetails(wheels, air, road, water, bias * 100, pitLimiter, ignition, starter, engine);
        result = new(speedKmh / 3.6, rpm, rawGear - 1, Math.Clamp(fuel / maxFuel, 0, 1),
            gas, brake, clutch, maxRpm, fuel, ActionStates: states, Acc: details);
        return true;
    }

    static int I(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    static float F(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
    static bool Fraction(float value) => Finite(value, 0, 1);
    static bool Finite(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
}
