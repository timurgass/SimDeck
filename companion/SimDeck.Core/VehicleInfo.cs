using System.Text;

namespace SimDeck.Core;

// Positions are metres relative to the vehicle, with negative Z towards its front.
// Missing fields remain unknown; a pressed control never supplies a vehicle state.
public sealed record VehicleWheel(double X, double Z, bool? Powered = null);
public sealed record VehicleAttachment(string Id, string ParentId, string Name, string Kind,
    IReadOnlyList<VehicleWheel> Wheels, bool? Lowered = null, bool? TurnedOn = null, double? Fold = null, string Mount = "unknown");
public sealed record VehicleInfo(string Id, string Name, string Kind, IReadOnlyList<VehicleWheel> Wheels,
    IReadOnlyList<VehicleAttachment> Attachments, bool Controlled = true,
    IReadOnlyDictionary<string, double>? Wear = null)
{
    public int? AxleCount => Wheels.Count == 0 ? null : Axles(Wheels).Count;
    public static IReadOnlyList<double> Axles(IReadOnlyList<VehicleWheel> wheels)
    {
        var result = new List<double>();
        foreach (var z in wheels.Select(w => w.Z).Order())
            if (result.Count == 0 || z - result[^1] > .35) result.Add(z);
        return result;
    }
}

public static class VehicleKinds
{
    public static readonly string[] All = ["unknown", "car", "suv", "pickup", "van", "bus", "truck", "trailer",
        "tractor", "combine", "loader", "telehandler", "forestry", "sprayer", "tracked", "implement", "header", "cultivator", "plow", "seeder", .. Fs25EquipmentRegistry.Labels.Keys];
    public static string Resolve(string? category)
    {
        var value = (category ?? "").Replace("_", "").Replace(" ", "").Replace("-", "").ToLowerInvariant();
        if (All.Contains(value)) return value;
        if (value.Contains("crawler") || value.Contains("tracked")) return "tracked";
        if (value.Contains("tractor")) return "tractor";
        if (value.Contains("combine") || value.Contains("harvester") && !value.Contains("wood") && !value.Contains("tree")) return "combine";
        if (value.Contains("telehandler")) return "telehandler";
        if (value.Contains("loader")) return "loader";
        if (value.Contains("forestry") || value.Contains("wood") || value.Contains("treeharvester")) return "forestry";
        if (value.Contains("sprayer")) return "sprayer";
        if (value.Contains("semitrailer") || value.Contains("trailer")) return "trailer";
        if (value.Contains("truck") || value.Contains("semi")) return "truck";
        if (value.Contains("bus")) return "bus";
        if (value.Contains("pickup")) return "pickup";
        if (value.Contains("suv") || value.Contains("offroad")) return "suv";
        if (value.Contains("van")) return "van";
        if (value.Contains("sedan") || value.Contains("coupe") || value.Contains("hatch") || value.Contains("wagon")) return "car";
        return "unknown";
    }
    public static string Label(string kind) => Fs25EquipmentRegistry.Labels.TryGetValue(kind, out var label) ? label : kind switch {
        "car" => "Легковая", "suv" => "Внедорожник", "pickup" => "Пикап", "van" => "Фургон", "bus" => "Автобус",
        "truck" => "Грузовик / тягач", "trailer" => "Прицеп", "tractor" => "Трактор", "combine" => "Комбайн",
        "loader" => "Погрузчик", "telehandler" => "Телескопический погрузчик", "forestry" => "Лесная техника",
        "sprayer" => "Опрыскиватель", "tracked" => "Гусеничная техника", "implement" => "Орудие", "header" => "Жатка", "cultivator" => "Культиватор", "plow" => "Плуг", "seeder" => "Сеялка", _ => "Общая схема" };
    public static string Text(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0); if (end >= 0) bytes = bytes[..end];
        try { return Clean(new UTF8Encoding(false, true).GetString(bytes)); }
        catch (DecoderFallbackException) { return ""; }
    }
    public static string Clean(string? value) => new((value ?? "").Where(c => !char.IsControl(c)).Take(160).ToArray());
}
