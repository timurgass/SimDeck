using System.Globalization;
using System.Xml.Linq;

namespace SimDeck.Core;

/// <summary>Colour from FS25 XML files. Values are in linear space — convert to sRGB before display.</summary>
/// <remarks>
/// CLAUDE.md ловушка: «`mapColors` в файлах игры — линейный RGB. Конвертировать в sRGB перед показом.»
/// </remarks>
public readonly record struct LinearRgba(double R, double G, double B, double A)
{
    public (double R, double G, double B) ToSrgb() => (Encode(R), Encode(G), Encode(B));
    private static double Encode(double linear) => linear <= 0.0031308
        ? 12.92 * linear
        : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
}

/// <summary>
/// One planted crop from <c>data/foliage/&lt;raw&gt;/&lt;raw&gt;.xml</c>.
/// </summary>
/// <param name="Name">Uppercase canonical name used by <c>maps_fruitTypes.xml</c> categories and by fill types (WHEAT, SUGARBEET).</param>
/// <param name="RawName">Original case from the XML (<c>wheat</c>, <c>sugarBeet</c>). Not always equal to the folder name.</param>
/// <param name="MapColor">Default map colour, linear RGB+alpha. Convert to sRGB before display.</param>
/// <param name="HarvestLitersPerSqm">Yield in litres per square metre at 100% growth. 0 for unharvestable crops.</param>
/// <param name="SeedingLitersPerSqm">Seed rate in litres per square metre, or <c>null</c> when the crop is not seeded via a sowing machine.</param>
/// <param name="NeedsRolling">The soil must be rolled after seeding.</param>
/// <param name="GrowthRequiresLime">The crop dies or halts without lime on the field.</param>
/// <param name="FoliageStates">Growth stages in order. <c>save/fields.xml growthState</c> is a 1-based index into this list.</param>
/// <param name="PlantingPeriods">Periods when the crop can be seeded (<c>plantingAllowed="true"</c> inside <c>growth/seasonal/period</c>).</param>
public sealed record Fs25Crop(
    string Name,
    string RawName,
    LinearRgba MapColor,
    double HarvestLitersPerSqm,
    double? SeedingLitersPerSqm,
    bool NeedsRolling,
    bool GrowthRequiresLime,
    IReadOnlyList<string> FoliageStates,
    IReadOnlyList<Period> PlantingPeriods)
{
    /// <summary>Resolve a 1-based <c>growthState</c> from the save into the stage name.</summary>
    public string? StageAt(int growthState) => growthState >= 1 && growthState <= FoliageStates.Count
        ? FoliageStates[growthState - 1]
        : null;
}

public static class Fs25CropParser
{
    /// <summary>Parses one foliage XML into a <see cref="Fs25Crop"/>. Owns no I/O.</summary>
    public static Fs25Crop Parse(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var root = doc.Root ?? throw new InvalidDataException("Foliage XML has no root element.");
        var fruit = root.Element("fruitType")
            ?? throw new InvalidDataException("foliageType/fruitType is missing.");

        var rawName = (string?)fruit.Attribute("name")
            ?? throw new InvalidDataException("fruitType/@name is missing.");
        var name = rawName.ToUpperInvariant();

        var colorAttr = (string?)fruit.Element("mapColors")?.Attribute("default") ?? "0 0 0 1";
        var color = ParseColor(colorAttr);

        var harvest = (double?)fruit.Element("harvest")?.Attribute("litersPerSqm") ?? 0.0;
        var seeding = (double?)fruit.Element("seeding")?.Attribute("litersPerSqm");
        var needsRolling = (bool?)fruit.Element("seeding")?.Attribute("needsRolling") ?? false;
        var needsLime = (bool?)fruit.Element("growth")?.Attribute("growthRequiresLime") ?? false;

        var states = root
            .Element("foliageLayer")?
            .Elements("foliageState")
            .Select(s => (string?)s.Attribute("name") ?? "")
            .Where(n => n.Length > 0)
            .ToList()
            ?? new List<string>();

        var planting = root
            .Element("growth")?
            .Element("seasonal")?
            .Elements("period")
            .Where(p => (bool?)p.Attribute("plantingAllowed") == true)
            .Select(p => Period.Parse((string)p.Attribute("name")!))
            .ToList()
            ?? new List<Period>();

        return new Fs25Crop(name, rawName, color, harvest, seeding, needsRolling, needsLime, states, planting);
    }

    private static LinearRgba ParseColor(string tuple)
    {
        var parts = tuple.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        static double P(string[] arr, int i) => double.Parse(arr[i], CultureInfo.InvariantCulture);
        var r = parts.Length > 0 ? P(parts, 0) : 0;
        var g = parts.Length > 1 ? P(parts, 1) : 0;
        var b = parts.Length > 2 ? P(parts, 2) : 0;
        var a = parts.Length > 3 ? P(parts, 3) : 1;
        return new LinearRgba(r, g, b, a);
    }
}

/// <summary>
/// Parsed <c>maps/maps_fruitTypes.xml</c>: the list of crops the game knows about plus
/// tool-compatibility categories (which sowing machine can plant which crop, which combine
/// header can cut which crop, and so on).
/// </summary>
public sealed record Fs25FruitTypeCatalog(
    IReadOnlyList<string> FoliageFiles,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Categories)
{
    /// <summary>Which categories a crop belongs to (uppercase). Empty when the crop is unmanaged (e.g. weed, grass in some categories).</summary>
    public IReadOnlyList<string> CategoriesFor(string cropName)
    {
        var up = cropName.ToUpperInvariant();
        return Categories
            .Where(kv => kv.Value.Any(c => string.Equals(c, up, StringComparison.OrdinalIgnoreCase)))
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>Which crops fall under the given category.</summary>
    public IReadOnlyList<string> CropsIn(string category) =>
        Categories.TryGetValue(category.ToUpperInvariant(), out var list) ? list : Array.Empty<string>();
}

public static class Fs25FruitTypesParser
{
    public static Fs25FruitTypeCatalog Parse(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var root = doc.Root ?? throw new InvalidDataException("maps_fruitTypes.xml has no root element.");

        var files = root
            .Descendants("fruitType")
            .Select(f => (string?)f.Attribute("filename"))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Replace("$data/", "", StringComparison.Ordinal))
            .ToList();

        var cats = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in root.Descendants("fruitTypeCategory"))
        {
            var name = (string?)c.Attribute("name");
            if (string.IsNullOrEmpty(name)) continue;
            var members = (c.Value ?? "")
                .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.ToUpperInvariant())
                .ToList();
            cats[name.ToUpperInvariant()] = members;
        }

        return new Fs25FruitTypeCatalog(files, cats);
    }
}

/// <summary>
/// One entry in <c>maps/maps_fillTypes.xml</c>. Prices are per litre in game currency (€).
/// </summary>
/// <remarks>
/// The 12 <c>&lt;factor period="N" value="X"/&gt;</c> children are the seasonal price multipliers.
/// These describe the base map's average — actual station prices are simulated with sine curves
/// on top and are NOT in the save. CLAUDE.md: «цены станций игра считает синусоидами и не хранит».
/// </remarks>
public sealed record Fs25FillType(
    string Name,
    double PricePerLiter,
    double? MassPerLiter,
    IReadOnlyList<double>? MonthlyFactors)
{
    /// <summary>Base-map estimate of €/litre for a given period. Returns the base price when no factors are declared.</summary>
    public double PriceAt(Period period) => MonthlyFactors is null
        ? PricePerLiter
        : PricePerLiter * MonthlyFactors[period.Number - 1];
}

public sealed record Fs25FillTypeCatalog(IReadOnlyDictionary<string, Fs25FillType> ByName)
{
    public Fs25FillType? Get(string name) =>
        ByName.TryGetValue(name.ToUpperInvariant(), out var t) ? t : null;

    public double? PriceAt(string name, Period period) => Get(name)?.PriceAt(period);
}

public static class Fs25FillTypesParser
{
    public static Fs25FillTypeCatalog Parse(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var byName = new Dictionary<string, Fs25FillType>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in doc.Descendants("fillType"))
        {
            var name = (string?)e.Attribute("name");
            if (string.IsNullOrEmpty(name)) continue;
            var economy = e.Element("economy");
            if (economy is null) continue;
            var price = (double?)economy.Attribute("pricePerLiter") ?? 0.0;
            var mass = (double?)e.Element("physics")?.Attribute("massPerLiter");
            List<double>? factors = null;
            var factorsNode = economy.Element("factors");
            if (factorsNode is not null)
            {
                var listed = factorsNode.Elements("factor")
                    .Select(x => (period: (int?)x.Attribute("period") ?? 0, value: (double?)x.Attribute("value") ?? 0))
                    .Where(t => t.period is >= 1 and <= Period.Count)
                    .OrderBy(t => t.period)
                    .ToList();
                if (listed.Count == Period.Count)
                    factors = listed.Select(t => t.value).ToList();
            }
            byName[name.ToUpperInvariant()] = new Fs25FillType(name.ToUpperInvariant(), price, mass, factors);
        }
        return new Fs25FillTypeCatalog(byName);
    }
}
