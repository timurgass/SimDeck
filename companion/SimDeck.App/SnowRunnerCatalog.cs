using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SimDeck.Core;

namespace SimDeck.App;

// Extracts names and wheel geometry from the player's installation. Game XML/artwork is not bundled.
public sealed class SnowRunnerCatalog
{
    public sealed record Truck(string Id, string Name, string Kind, string Suspension, VehicleWheel[] Wheels);
    readonly List<Truck> trucks = [];
    public IReadOnlyList<Truck> Trucks => trucks;
    public Truck? Resolve(string suspension, int wheelCount)
    {
        var candidates = trucks.Where(t => t.Suspension == suspension && t.Wheels.Length == wheelCount).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }
    public static SnowRunnerCatalog Load(string gameDirectory)
    {
        var catalog = new SnowRunnerCatalog();
        var path = Path.Combine(gameDirectory, "preload", "paks", "client", "initial.pak");
        if (!File.Exists(path)) return catalog;
        using var zip = ZipFile.OpenRead(path);
        string Text(ZipArchiveEntry entry)
        {
            if (entry.Length > 5000000) throw new InvalidDataException("SnowRunner metadata exceeds limit");
            using var stream = entry.Open(); using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return reader.ReadToEnd();
        }
        var names = new Dictionary<string,string>();
        if (zip.Entries.FirstOrDefault(e=>e.FullName.Replace('\\','/')=="[strings]/strings_english.str") is { } strings)
            foreach (Match m in Regex.Matches(Text(strings), "(?m)^([A-Z0-9_]+)\\s+\"([^\"\\r\\n]*)\""))
                names[m.Groups[1].Value] = VehicleKinds.Clean(m.Groups[2].Value);
        foreach (var entry in zip.Entries.Where(e => Regex.IsMatch(e.FullName.Replace('\\','/'), @"/classes/trucks/[^/]+\.xml$")))
        {
            try
            {
                var document = XDocument.Parse("<source>" + Regex.Replace(Text(entry), @"<\?xml[^>]*\?>", "") + "</source>");
                var truck = document.Root?.Element("Truck"); var data = truck?.Element("TruckData");
                if (data is null) continue;
                var suspension = (string?)data.Element("SuspensionSocket")?.Attribute("Type");
                if (string.IsNullOrWhiteSpace(suspension)) continue;
                var wheels = new List<VehicleWheel>();
                foreach (var wheel in data.Element("Wheels")?.Elements("Wheel") ?? [])
                {
                    var position = ((string?)wheel.Attribute("Pos") ?? "").Trim('(', ')').Split(';');
                    if (position.Length != 3 || !double.TryParse(position[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var longitudinal) ||
                        !double.TryParse(position[2], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var lateral) || Math.Abs(longitudinal)>40 || Math.Abs(lateral)>10) continue;
                    wheels.Add(new(((string?)wheel.Attribute("RightSide")) == "true" ? lateral : -lateral, -longitudinal));
                }
                if (wheels.Count is <4 or >16 || wheels.Count%2!=0) continue;
                var id = Path.GetFileNameWithoutExtension(entry.FullName.Replace('\\','/'));
                var nameKey = (string?)truck!.Element("GameData")?.Element("UiDesc")?.Attribute("UiName") ?? "";
                var name = names.GetValueOrDefault(nameKey, id.Replace('_',' '));
                var kind = (string?)data.Attribute("TruckType") == "SCOUT" ? "suv" : "truck";
                catalog.trucks.Add(new(id, name, kind, suspension, wheels.ToArray()));
            }
            catch (System.Xml.XmlException) { /* A custom truck may use an unsupported XML extension. */ }
        }
        return catalog;
    }
}
