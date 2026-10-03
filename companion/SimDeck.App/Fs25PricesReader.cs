using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using SimDeck.Core;

namespace SimDeck.App;

public sealed record Fs25PricesSnapshot(Fs25Prices Data, DateTime WrittenAtUtc);
public static class Fs25PricesReader
{
    public const string FileName = "simdeckPrices.xml";
    public static Fs25PricesSnapshot? Read(string directory, DateTime nowUtc, DateTime? unchangedSince = null)
    {
        try {
            var file = new FileInfo(Path.Combine(directory, FileName));
            if (!file.Exists || file.Length is <= 0 or > 1048576 || file.LastWriteTimeUtc == unchangedSince) return null;
            var written = file.LastWriteTimeUtc;
            var age = (long)(nowUtc - written).TotalMilliseconds;
            if (age < -2000 || age > 30000) return null;
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1048576 });
            var root = XDocument.Load(reader).Root;
            if (root?.Name != "simdeckPrices" || (int?)root.Attribute("version") != 1) return null;
            var offers = new List<Fs25PriceOffer>();
            foreach (var row in root.Elements("offer")) {
                if (offers.Count >= 2048) return null;
                string Text(string key) => ((string?)row.Attribute(key) ?? "").Trim();
                var crop=Text("crop"); var name=Text("cropName"); var station=Text("station"); var formatted=Text("formatted");
                if (crop.Length is < 1 or > 160 || name.Length > 160 || station.Length is < 1 or > 160 || formatted.Length > 160 ||
                    !double.TryParse(Text("pricePer1000"), NumberStyles.Float, CultureInfo.InvariantCulture, out var price) || !double.IsFinite(price) || price is <= 0 or >= 100000000) continue;
                offers.Add(new(crop, name, station, price, formatted));
            }
            return new(new(offers, Math.Max(0, age)), written);
        } catch (Exception ex) when(ex is IOException or XmlException or UnauthorizedAccessException or InvalidDataException or FormatException) { return null; }
    }
}
