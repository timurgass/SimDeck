using System.IO;
using System.Xml;

namespace SimDeck.App;

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

    public static IReadOnlyDictionary<string, bool>? Read(string userDataDir, DateTime nowUtc)
    {
        var path = Path.Combine(userDataDir, FileName);
        if (!File.Exists(path) || nowUtc - File.GetLastWriteTimeUtc(path) > TimeSpan.FromMilliseconds(1200)) return null;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (file.Length > 4096) return null;
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4096 });
            reader.MoveToContent();
            if (reader.Name != "simdeckStatus" || reader.GetAttribute("version") != "1") return null;
            var states = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var (attribute, action) in Mappings)
                if (bool.TryParse(reader.GetAttribute(attribute), out var value)) states[action] = value;
            return states.Count == 0 ? null : states;
        }
        catch (Exception ex) when (ex is IOException or XmlException or UnauthorizedAccessException) { return null; }
    }
}
