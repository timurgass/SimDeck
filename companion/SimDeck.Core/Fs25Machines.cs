using System.Globalization;
using System.Xml.Linq;

namespace SimDeck.Core;

/// <summary>
/// One entry in the FS25 store — a purchasable vehicle or implement.
/// </summary>
/// <param name="Type">Value of <c>&lt;vehicle type="…"&gt;</c>: <c>spreader</c>, <c>combine</c>, <c>cultivator</c>, <c>tractor</c>, etc.</param>
/// <param name="Name">Human-readable display name from <c>storeData/name</c>, e.g. <c>ZA-TS 3200</c>. NOT localised — game uses <c>$l10n_</c> tokens elsewhere but leaves names as-is.</param>
/// <param name="Brand">Brand code from <c>storeData/brand</c> (e.g. <c>AMAZONE</c>, <c>ABI</c>).</param>
/// <param name="Category">Store category from <c>storeData/category</c> (e.g. <c>fertilizerSpreaders</c>, <c>cultivators</c>, <c>barrels</c>).</param>
/// <param name="Price">Base game price in € from <c>storeData/price</c>. Actual shop price with configurations may differ.</param>
/// <param name="Functions">Localisation tokens from <c>storeData/functions/function</c>. Prefixed with <c>$l10n_</c> in the XML.</param>
/// <param name="ImagePath">Relative path (with <c>$data</c> stripped) to the shop preview PNG. May be empty when the file lacks an image.</param>
/// <param name="RelativePath">Path of the XML file relative to <c>data/vehicles/</c>, useful as an id.</param>
public sealed record Fs25Machine(
    string Type,
    string Name,
    string Brand,
    string Category,
    int Price,
    IReadOnlyList<string> Functions,
    string ImagePath,
    string RelativePath)
{
    /// <summary>Convenience: brand-and-name string for on-screen labels.</summary>
    public string FullName => string.IsNullOrEmpty(Brand) ? Name : $"{Brand} {Name}";
}

public static class Fs25MachineParser
{
    /// <summary>Parses one vehicle XML. Returns <c>null</c> when the file has no <c>storeData</c> block.</summary>
    public static Fs25Machine? Parse(Stream xml, string relativePath = "")
    {
        var doc = XDocument.Load(xml);
        var root = doc.Root ?? throw new InvalidDataException("Vehicle XML has no root element.");
        var store = root.Element("storeData");
        if (store is null) return null;

        var type = (string?)root.Attribute("type") ?? "";
        var name = ((string?)store.Element("name") ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return null;

        var brand = ((string?)store.Element("brand") ?? "").Trim();
        var category = ((string?)store.Element("category") ?? "").Trim();
        var price = 0;
        if (int.TryParse((string?)store.Element("price"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            price = parsed;
        var image = ((string?)store.Element("image") ?? "")
            .Replace("$data/", "", StringComparison.Ordinal)
            .Trim();
        var functions = store.Element("functions")?
            .Elements("function")
            .Select(f => (f.Value ?? "").Trim())
            .Where(s => s.Length > 0)
            .ToList()
            ?? new List<string>();

        return new Fs25Machine(type, name, brand, category, price, functions, image, relativePath);
    }
}

/// <summary>
/// Set of machines discovered under <c>data/vehicles/</c>. Not lazy — the whole tree is walked
/// on construction because 758 files is fast enough at start-up and callers need to iterate freely.
/// </summary>
public sealed record Fs25MachineCatalog(IReadOnlyList<Fs25Machine> Machines)
{
    public IReadOnlyList<Fs25Machine> ByCategory(string category) =>
        Machines.Where(m => string.Equals(m.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<Fs25Machine> ByBrand(string brand) =>
        Machines.Where(m => string.Equals(m.Brand, brand, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<Fs25Machine> ByType(string type) =>
        Machines.Where(m => string.Equals(m.Type, type, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<string> Categories =>
        Machines.Select(m => m.Category).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();

    /// <summary>Walk <paramref name="root"/> looking for vehicle XMLs, parsing each and skipping files without <c>storeData</c>.</summary>
    public static Fs25MachineCatalog LoadFrom(string root)
    {
        if (!Directory.Exists(root)) return new Fs25MachineCatalog(Array.Empty<Fs25Machine>());
        var list = new List<Fs25Machine>();
        foreach (var file in Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories))
        {
            // Skip sub-files like sounds/*.xml that reuse the vehicle XML shape without storeData.
            var directory = Path.GetDirectoryName(file);
            var vehicleName = Path.GetFileNameWithoutExtension(file);
            var directoryName = Path.GetFileName(directory ?? "");
            if (!string.Equals(vehicleName, directoryName, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var stream = File.OpenRead(file);
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                var machine = Fs25MachineParser.Parse(stream, relative);
                if (machine is not null) list.Add(machine);
            }
            catch (InvalidDataException) { /* skip malformed */ }
            catch (System.Xml.XmlException) { /* skip malformed */ }
        }
        return new Fs25MachineCatalog(list);
    }
}
