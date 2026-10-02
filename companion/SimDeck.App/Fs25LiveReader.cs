using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using SimDeck.Core;

namespace SimDeck.App;

public sealed record Fs25LiveSnapshot(IReadOnlyDictionary<string, bool> States, VehicleInfo? Vehicle);

/// <summary>Read-only bridge for the optional FS25_SimDeckStatus in-game mod.</summary>
public static class Fs25LiveReader
{
    public const string FileName = "simdeckStatus.xml";
    static readonly (string Attribute, string Action)[] Mappings =
    [
        ("lowered", "fs25Lower"),
        ("turnedOn", "fs25TurnOn"),
        ("motor", "fs25Motor"),
    ];

    public static IReadOnlyDictionary<string, bool>? Read(string userDataDir, DateTime nowUtc) => ReadSnapshot(userDataDir, nowUtc)?.States;
    public static Fs25LiveSnapshot? ReadSnapshot(string userDataDir, DateTime nowUtc)
    {
        var path = Path.Combine(userDataDir, FileName);
        if (!File.Exists(path) || nowUtc - File.GetLastWriteTimeUtc(path) > TimeSpan.FromMilliseconds(1200)) return null;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (file.Length > 65536) return null;
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            var root = XElement.Load(reader);
            var version = (string?)root.Attribute("version");
            if (root.Name != "simdeckStatus" || version is not ("1" or "2")) return null;
            var states = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var (attribute, action) in Mappings)
                if (bool.TryParse((string?)root.Attribute(attribute), out var value)) states[action] = value;
            if (version == "1") return states.Count == 0 ? null : new(states, null);
            if (Bool(root, "controlled") == false) return new(new Dictionary<string, bool>(), new("", "", "unknown", [], [], false));
            var machines = root.Elements("vehicle").Take(33).ToArray();
            if (machines.Length is < 1 or > 32 || Bool(root, "controlled") != true) return null;
            var first = machines[0];
            var rootId = Str(first,"instance");
            rootId = "vehicle:" + (rootId.Length == 0 ? Str(first,"model") : rootId);
            var seen = new HashSet<string> { Str(first, "id") };
            if (seen.Contains("")) return null;
            var attachments = new List<VehicleAttachment>();
            foreach (var machine in machines.Skip(1)) {
                var id = Str(machine, "id"); var parent = Str(machine, "parentId");
                if (id.Length == 0 || !seen.Contains(parent) || !seen.Add(id)) return null;
                attachments.Add(new(id, parent == Str(first,"id") ? rootId : parent, Str(machine, "name"), VehicleKinds.Resolve(Str(machine, "kind")), Wheels(machine),
                    Bool(machine, "lowered"), Bool(machine, "turnedOn"), Number(machine, "fold", 1) is >= 0 and var fold ? fold : null));
            }
            return new(states, new(rootId, Str(first, "name"),
                VehicleKinds.Resolve(Str(first, "kind")), Wheels(first), attachments));
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }
    static string Str(XElement e, string name) => VehicleKinds.Clean((string?)e.Attribute(name));
    static bool? Bool(XElement e, string name) => bool.TryParse((string?)e.Attribute(name), out var b) ? b : null;
    static double? Number(XElement e, string name, double max) => double.TryParse((string?)e.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
        && double.IsFinite(d) && Math.Abs(d) <= max ? d : null;
    static IReadOnlyList<VehicleWheel> Wheels(XElement e) {
        var nodes = e.Elements("wheel").Take(33).ToArray();
        if (nodes.Length > 32) throw new InvalidDataException("Too many wheels");
        return nodes.Select(w => new VehicleWheel(Number(w,"x",100) ?? throw new InvalidDataException("Invalid wheel X"),
            Number(w,"z",100) ?? throw new InvalidDataException("Invalid wheel Z"))).ToArray();
    }
}
