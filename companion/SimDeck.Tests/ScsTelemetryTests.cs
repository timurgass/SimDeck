using System.Buffers.Binary;
using SimDeck.Core;

internal static class ScsTelemetryTests
{
    public static void Run(Action<bool, string> check)
    {
        var bytes = new byte[ScsTelemetryParser.SnapshotSize];
        bytes[0] = 1;
        U64(8, 123456);
        U32(40, 12);
        U32(52, 1);
        U32(68, 12);
        I32(504, 6);
        F32(704, 600);
        F32(740, 2500);
        F32(948, 22.5f);
        F32(952, 1450);
        F32(976, .7f);
        F32(980, .1f);
        F32(984, 0);
        F32(1000, 300);
        F32(1060, 309630);
        F32(1064, 18420);
        F32(1068, 13.8889f);
        bytes[1576] = 1; // engine
        bytes[1583] = 1; // low beam
        bytes[1588] = 1; // hazard
        check(ScsTelemetryParser.TryParse(bytes, out var stamp, out var frame) && stamp == 123456 &&
              frame is { Gear: 6, Rpm: 1450, Headlights: 1, MaxGear: 12 } &&
              Math.Abs(frame.SpeedMps - 22.5) < .001 && frame.FuelFraction == .5 &&
              frame.ActionStates!["etsEngine"] && frame.ActionStates["etsHazards"] &&
              Math.Abs(frame.Ets2Navigation!.RemainingKm!.Value - 309.63) < .01 &&
              Math.Abs(frame.Ets2Navigation.SpeedLimitKmh!.Value - 50) < .01,
            "SCS revision 12 memory exposes ETS2 speed, RPM, gear, fuel and switch states");
        bytes[1584] = 1;
        check(ScsTelemetryParser.TryParse(bytes, out _, out frame) && frame!.Headlights == 2,
            "ETS2 high beam has distinct blue state");
        bytes[4] = 1;
        check(!ScsTelemetryParser.TryParse(bytes, out _, out _), "Paused ETS2 does not publish fresh telemetry");
        bytes[4] = 0;
        U32(40, 11);
        check(!ScsTelemetryParser.TryParse(bytes, out _, out _), "Unknown SCS memory revision is rejected");
        U32(40, 12);
        F32(948, float.NaN);
        check(!ScsTelemetryParser.TryParse(bytes, out _, out _), "Non-finite ETS2 speed is rejected");

        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        void U64(int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset), value);
        void I32(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
        void F32(int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset), value);
    }
}
