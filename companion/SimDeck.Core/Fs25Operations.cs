using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Text.Json;

namespace SimDeck.Core;

public sealed record Fs25FinanceEntry(string Category, double Amount);
public sealed record Fs25FinanceDay(int Day, Fs25FinanceEntry[] Entries, double Income, double Expenses, double Net);
public sealed record Fs25Stock(int FarmId, string Crop, double Litres, string Location, string Source);
public sealed record Fs25FleetItem(int FarmId, string Model, string Property, double? Hours, double? Damage);
public sealed record Fs25Production(int FarmId, string Building, string Recipe, bool? Enabled);
public sealed record Fs25FarmLedger(int FarmId, Fs25FinanceDay[] Days);
public sealed record Fs25Operations(Fs25FarmLedger[] Ledgers, Fs25Stock[] Stocks, Fs25FleetItem[] Fleet,
    Fs25Production[] Productions, bool StorageAvailable, bool FleetAvailable, bool Truncated = false);

/// Read-only subset of the saved farm. Never treats enabled recipes as proof of production.
public static class Fs25OperationsReader
{
    static XDocument Load(Stream stream, string root)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32 * 1024 * 1024 });
        var doc = XDocument.Load(reader);
        if (doc.Root?.Name != root) throw new InvalidDataException("Unexpected FS25 " + root + " root");
        return doc;
    }
    static int Id(XElement e, string name) => int.TryParse((string?)e.Attribute(name), out var id) && id is >= 0 and <= 1000000 ? id : -1;
    static string Text(string? text, int limit = 160) => new((text ?? "").Where(c => !char.IsControl(c)).Take(limit).ToArray());
    static double? Number(string? text, double min = 0, double max = 1e12) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) && n >= min && n <= max ? n : null;
    static string Model(XElement item, int index)
    {
        var file = ((string?)item.Attribute("filename") ?? "").Replace('\\', '/');
        if (file.Length > 0) return Text(Path.GetFileNameWithoutExtension(file));
        var preplaced = (string?)item.Attribute("uniqueId") ?? "";
        if (preplaced.StartsWith("preplaced_", StringComparison.Ordinal)) {
            preplaced = preplaced[10..]; var split = preplaced.LastIndexOf('_');
            if (split > 0 && preplaced.Length - split > 16) preplaced = preplaced[..split];
            return Text(preplaced);
        }
        return "Объект №" + (index + 1);
    }
    public static Fs25Operations Parse(Stream farms, Stream? placeables, Stream? vehicles)
    {
        var farmXml = Load(farms, "farms");
        var limited = false;
        IEnumerable<XElement> Limit(IEnumerable<XElement> items, int count) {
            var subset = items.Take(count + 1).ToArray();
            if (subset.Length > count) limited = true;
            return subset.Take(count);
        }
        var known = Limit(farmXml.Root!.Elements("farm"), 64).Select(f => Id(f, "farmId")).Where(id => id > 0).ToHashSet();
        var ledgers = farmXml.Root.Elements("farm").Take(64).Where(f => known.Contains(Id(f, "farmId"))).Select(f => {
            var days = f.Element("finances")?.Elements("stats").Take(366).Where(s => Id(s, "day") >= 0)
                .GroupBy(s => Id(s, "day")).Select(g => {
                    var entries = g.SelectMany(s => s.Elements().Take(128)).Select(e => new { Key = Text(e.Name.LocalName), Value = Number(e.Value, -1e12) })
                        .Where(e => e.Value is not null).GroupBy(e => e.Key).Select(e => new Fs25FinanceEntry(e.Key, e.Sum(v => v.Value!.Value))).ToArray();
                    return new Fs25FinanceDay(g.Key, entries.Where(e => e.Amount != 0).ToArray(), entries.Sum(e => Math.Max(0, e.Amount)), -entries.Sum(e => Math.Min(0, e.Amount)), entries.Sum(e => e.Amount));
                }).OrderByDescending(d => d.Day).Take(31).ToArray() ?? [];
            return new Fs25FarmLedger(Id(f, "farmId"), days);
        }).ToArray();
        var stocks = new List<Fs25Stock>(); var fleet = new List<Fs25FleetItem>(); var productions = new List<Fs25Production>();
        void Stock(XElement node, int farm, string location, string source)
        {
            var crop = Text((string?)node.Attribute("fillType"), 80).ToUpperInvariant();
            var litres = Number((string?)node.Attribute("fillLevel"), 0, 1e10);
            if (known.Contains(farm) && crop.Length > 0 && crop is not ("UNKNOWN" or "AIR" or "ELECTRICCHARGE") && litres > 0) {
                if (stocks.Count < 2048) stocks.Add(new(farm, crop, litres.Value, location, source));
                else limited = true;
            }
        }
        if (placeables is not null) {
            var xml = Load(placeables, "placeables");
            foreach (var (p, i) in Limit(xml.Root!.Elements("placeable"), 4096).Select((p, i) => (p, i))) {
                var farm = Id(p, "farmId"); var name = Model(p, i);
                foreach (var storage in (p.Element("silo")?.Elements("storage") ?? []).Concat(p.Element("productionPoint")?.Elements("storage") ?? []).Take(64)) {
                    var owner = storage.Attribute("farmId") is not null ? Id(storage, "farmId") : farm;
                    foreach (var node in storage.Elements("node").Take(256)) Stock(node, owner, name, "storage");
                }
                if (!known.Contains(farm)) continue;
                foreach (var recipe in p.Element("productionPoint")?.Elements("production").Take(64) ?? []) {
                    if (productions.Count >= 1024) { limited = true; break; }
                    var id = Text((string?)recipe.Attribute("id"), 80); if (id.Length == 0) continue;
                    productions.Add(new(farm, name, id, bool.TryParse((string?)recipe.Attribute("isEnabled"), out var enabled) ? enabled : null));
                }
            }
        }
        if (vehicles is not null) {
            var xml = Load(vehicles, "vehicles");
            foreach (var (v, i) in Limit(xml.Root!.Elements("vehicle"), 2048).Select((v, i) => (v, i))) {
                var farm = Id(v, "farmId"); if (!known.Contains(farm)) continue;
                var name = Model(v, i);
                // Cargo/pallets and silo contents are different sources, each counted once.
                foreach (var unit in v.Element("fillUnit")?.Elements("unit").Take(64) ?? []) Stock(unit, farm, name, "vehicle");
                var property = Text((string?)v.Attribute("propertyState"), 20);
                if (property is not ("OWNED" or "LEASED" or "MISSION") || v.Element("pallet") is not null || v.Element("bale") is not null) continue;
                fleet.Add(new(farm, name, property, Number((string?)v.Attribute("operatingTime")) / 3600000,
                    Number((string?)v.Element("wearable")?.Attribute("damage"), 0, 1) * 100));
            }
        }
        var result = new Fs25Operations(ledgers, stocks.ToArray(), fleet.ToArray(), productions.ToArray(), placeables is not null, vehicles is not null, limited);
        // Leave room in the client's 1-MiB message budget for fields and live prices.
        // Kept financial totals remain complete; detail rows may be shortened.
        while (JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length > 262144) {
            result = result with { Truncated = true,
                Stocks = result.Stocks.Take(result.Stocks.Length / 2).ToArray(),
                Fleet = result.Fleet.Take(result.Fleet.Length / 2).ToArray(),
                Productions = result.Productions.Take(result.Productions.Length / 2).ToArray(),
                Ledgers = result.Ledgers.Select(l => l with { Days = l.Days.Take(Math.Max(1, l.Days.Length / 2))
                    .Select(d => d with { Entries = d.Entries.Take(d.Entries.Length / 2).ToArray() }).ToArray() }).ToArray() };
        }
        return result;
    }
    public static Fs25Operations Read(Fs25SavegameDir dir)
    {
        using var farms = File.OpenRead(dir.File("farms.xml"));
        using var buildings = File.Exists(dir.File("placeables.xml")) ? File.OpenRead(dir.File("placeables.xml")) : null;
        using var vehicles = File.Exists(dir.File("vehicles.xml")) ? File.OpenRead(dir.File("vehicles.xml")) : null;
        return Parse(farms, buildings, vehicles);
    }
}
