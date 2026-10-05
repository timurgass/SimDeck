namespace SimDeck.Core;

public sealed record SnowRunnerComponent(string Id, string Name, int Damage, int Capacity)
{
    public double DamageFraction => (double)Damage / Capacity;
    public int Remaining => Capacity - Damage;
}

// Null states stay unknown. This is a read-only game snapshot, never a history of button presses.
public sealed record SnowRunnerDetails(string Build, double FuelCapacity,
    SnowRunnerComponent[] Components, string? GearLabel = null,
    bool? AwdAvailable = null, bool? DifferentialAvailable = null, string[]? AvailableGears = null);

public static class SnowRunnerTelemetry
{
    public static Dictionary<string,bool> ActionStates(ReadOnlySpan<byte> engine, ReadOnlySpan<byte> driveline)
    {
        var states=new Dictionary<string,bool>();
        if(engine.Length>0 && engine[0] is 0 or 1) states["snowEngine"]=engine[0]==1;
        if(driveline.Length>=7)
        {
            if(driveline[5] is 0 or 1) states["snowDifferential"]=driveline[5]==1;
            if(driveline[6] is 0 or 1) states["snowAwd"]=driveline[6]==1;
        }
        return states;
    }
    public static SnowRunnerComponent? Component(string id, string name, int damage, int capacity) =>
        capacity is > 0 and <= 100000 && damage >= 0 && damage <= capacity
            ? new(id, name, damage, capacity) : null;

    public static bool Valid(double fuel, double capacity, double vx, double vy, double vz, int wheels) =>
        double.IsFinite(fuel) && double.IsFinite(capacity) && capacity is > 0 and <= 10000 &&
        fuel >= 0 && fuel <= capacity + .01 && wheels is >= 4 and <= 16 && wheels % 2 == 0 &&
        new[] { vx, vy, vz }.All(v => double.IsFinite(v) && Math.Abs(v) < 200);
}
