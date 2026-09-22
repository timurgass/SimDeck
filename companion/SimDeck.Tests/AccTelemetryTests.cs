using System.Buffers.Binary;
using SimDeck.Core;

static class AccTelemetryTests
{
    public static void Run(Action<bool, string> check)
    {
        var physics = new byte[AccTelemetryParser.PhysicsSize];
        var statics = new byte[AccTelemetryParser.StaticSize];
        I(physics, 0, 42); F(physics, 4, .8f); F(physics, 8, .2f); F(physics, 12, 60); I(physics, 16, 5);
        I(physics, 20, 6200); F(physics, 28, 180); I(physics, 248, 1); F(physics, 288, 23); F(physics, 292, 32);
        F(physics, 364, .4f); F(physics, 564, .61f); I(physics, 588, 8000); F(physics, 712, 89);
        I(physics, 772, 1); I(physics, 776, 0); I(physics, 780, 1); F(statics, 416, 120); I(statics, 412, 8000);
        for (var n = 0; n < 4; n++)
        {
            F(physics, 88 + n * 4, 26.5f + n / 10f); F(physics, 120 + n * 4, .01f * n);
            F(physics, 152 + n * 4, 80 + n); F(physics, 348 + n * 4, 400 + n * 10);
            F(physics, 680 + n * 4, .02f * n); F(physics, 740 + n * 4, 29 - n); F(physics, 756 + n * 4, 32 - n);
        }

        check(AccTelemetryParser.TryParse(physics, statics, out var t) && t is not null &&
            Math.Abs(t.SpeedMps - 50) < .001 && t.Rpm == 6200 && t.Gear == 4 &&
            Math.Abs(t.FuelFraction!.Value - .5) < .001 && t.FuelLiters == 60 && t.MaxRpm == 8000,
            "ACC Shared Memory converts speed, gear, fuel and RPM");
        var acc = t!.Acc;
        check(acc is { Wheels.Length: 4 } && Math.Abs(acc.Wheels[2].Pressure - 26.7) < .01 &&
            acc.Wheels[3].CoreTemperature == 83 && Math.Abs(acc.Wheels[3].Wear - 3) < .01 &&
            acc.WaterTemperature == 89 && Math.Abs(acc.BrakeBias - 61) < .01,
            "ACC exposes four tyres, brakes and environment values");
        check(t.ActionStates!["accPitLimiter"] && t.ActionStates["accIgnition"] && !t.ActionStates["accStarter"] && acc!.EngineRunning,
            "ACC exposes confirmed pit limiter and engine switch states");
        check(!AccTelemetryParser.TryParse(physics[..799], statics, out _), "ACC rejects truncated shared-memory pages");
        F(physics, 28, float.NaN);
        check(!AccTelemetryParser.TryParse(physics, statics, out _), "ACC rejects non-finite live values");
    }

    static void I(Span<byte> data, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data[offset..], value);
    static void F(Span<byte> data, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(data[offset..], value);
}
