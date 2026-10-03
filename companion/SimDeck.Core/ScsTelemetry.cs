using System.Buffers.Binary;

namespace SimDeck.Core;

// RenCloud scs-sdk-plugin revision 12, Local\SCSTelemetry, x64 shared-memory layout.
// Offsets follow SCSSdkConvert.cs in its MIT-licensed C# client.
public static class ScsTelemetryParser
{
    public const int SnapshotSize = 21620;
    public const int MinimumSnapshotSize = 2048;
    public const int PluginRevision = 12;

    public static bool TryParse(ReadOnlySpan<byte> data, out ulong timestamp, out Telemetry? telemetry)
    {
        timestamp = 0;
        telemetry = null;
        if (data.Length < MinimumSnapshotSize || data[0] != 1 || data[4] != 0 || U(data, 40) != PluginRevision || U(data, 52) != 1)
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
        // SDK revision 12: eighth zone is dplacement at byte 2200; heading is turns, anticlockwise.
        if(data.Length>=2232)
        {
            var x=BinaryPrimitives.ReadDoubleLittleEndian(data[2200..]);
            var z=BinaryPrimitives.ReadDoubleLittleEndian(data[2216..]);
            var heading=BinaryPrimitives.ReadDoubleLittleEndian(data[2224..]);
            if(double.IsFinite(x) && double.IsFinite(z) && Math.Abs(x)<=1_000_000 && Math.Abs(z)<=1_000_000 &&
                double.IsFinite(heading) && heading is >=0 and <=1)
                navigation=navigation with { WorldX=x,WorldZ=z,Heading=heading==1?0:heading };
        }
        telemetry = new(Math.Abs(speed), rpm, gear, Math.Clamp(fuelLiters / fuelCapacity, 0, 1),
            throttle, brake, clutch, maxRpm, fuelLiters, "realistic", (int)forwardGears,
            highBeam ? 2 : lowBeam ? 1 : 0, states, Ets2Navigation: navigation, Vehicle: Vehicle(data));
        return true;
    }

    static int I(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    static uint U(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    static float F(ReadOnlySpan<byte> data, int offset) => BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
    static bool On(ReadOnlySpan<byte> data, int offset) => data[offset] != 0;
    static bool Finite(float value, float min, float max) => float.IsFinite(value) && value >= min && value <= max;
    static bool Fraction(float value) => Finite(value, 0, 1);

    static VehicleInfo? Vehicle(ReadOnlySpan<byte> data) {
        if (data.Length < 2556) return null; // Old short buffers retain all existing telemetry.
        var id = VehicleKinds.Text(data.Slice(2428,64));
        var name = string.Join(" ", new[]{VehicleKinds.Text(data.Slice(2364,64)), VehicleKinds.Text(data.Slice(2492,64))}.Where(s => s.Length > 0));
        if (id.Length == 0 && name.Length == 0) return null;
        if (id.Length == 0) id = "truck";
        var wheels = Wheels(data, U(data,80), 1676,1804,1532);
        var attachments = new List<VehicleAttachment>();
        for(var i=0;i<10;i++) {
            var offset=6000+i*1560;
            if(data.Length < offset+1560) break;
            var t=data.Slice(offset,1560);
            if(t[80]!=1) continue; // Detached trailers never remain on the schematic.
            var trailerId = VehicleKinds.Text(t.Slice(920,64));
            var trailerName = VehicleKinds.Text(t.Slice(1240,64));
            var body = VehicleKinds.Text(t.Slice(1048,64));
            attachments.Add(new("trailer-"+i+":"+trailerId, i == 0 || attachments.Count == 0 ? id : attachments[^1].Id, trailerName.Length>0?trailerName:body,
                "trailer", Wheels(t,U(t,148),676,804,32)));
        }
        var wear=new Dictionary<string,double>();
        string[] names=["engine","transmission","cabin","chassis","wheels"];
        for(var i=0;i<names.Length;i++) { var d=F(data,1036+i*4);if(Fraction(d))wear[names[i]]=d; }
        return new(id, name, "truck", wheels, attachments, Wear:wear);
    }
    static IReadOnlyList<VehicleWheel> Wheels(ReadOnlySpan<byte> data, uint count, int xOffset, int zOffset, int poweredOffset) {
        if(count is <1 or >16) return [];
        var result=new List<VehicleWheel>();
        for(var i=0;i<count;i++) {
            var x=F(data,xOffset+i*4);var z=F(data,zOffset+i*4);
            if(!Finite(x,-100,100)||!Finite(z,-100,100))return [];
            result.Add(new(x,-z, data[poweredOffset+i] is 0 or 1 ? data[poweredOffset+i]==1 : null));
        }
        // All-zero/uninitialised coordinates convey no wheel geometry.
        return result.All(w=>w.X==0&&w.Z==0)?[]:result;
    }
}
