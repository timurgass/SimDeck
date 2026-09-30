using System.Globalization;
using System.Xml.Linq;

namespace SimDeck.Core;

/// <summary>
/// One field's state at the moment the save was written. See <c>fields.xml</c>.
/// </summary>
/// <param name="Id">Field number as it appears on the in-game map.</param>
/// <param name="FruitType">Uppercase fruit-type name (WHEAT, POTATO, GRASS). "FALLOW" or empty when the field is fallow.</param>
/// <param name="PlannedFruit">What the game intends to grow next once the current crop is harvested. Often "FALLOW" in newer saves.</param>
/// <param name="GrowthState">1-based index into the crop's <c>foliageState</c> list — decode via <see cref="Fs25Crop.StageAt"/>.</param>
/// <param name="LastGrowthState">Previous growth state when the field was inspected. 0 when never set.</param>
/// <param name="WeedState">0..9. 0 is clean, ≥ 6 hurts yield sharply.</param>
/// <param name="StoneLevel">0..N. Affects tyre wear and cultivator damage.</param>
/// <param name="GroundType">SOWN / HARVEST_READY / GRASS / etc. Cross-checks with <paramref name="GrowthState"/>.</param>
/// <param name="SprayType">NONE / FERTILIZER / MANURE / LIQUIDFERTILIZER / …</param>
/// <param name="SprayLevel">0..3. Fertiliser application count.</param>
/// <param name="LimeLevel">0..3. Lime applications since the last plough. 0 stalls growth on lime-requiring crops.</param>
/// <param name="RollerLevel">0..1. Whether the field has been rolled since seeding.</param>
/// <param name="PlowLevel">0..1. Whether the field has been ploughed since the last harvest.</param>
/// <param name="StubbleShredLevel">0..1. Stubble was mulched.</param>
/// <param name="WaterLevel">0..3 for irrigation-enabled maps.</param>
public sealed record Fs25FieldState(
    int Id,
    string FruitType,
    string PlannedFruit,
    int GrowthState,
    int LastGrowthState,
    int WeedState,
    int StoneLevel,
    string GroundType,
    string SprayType,
    int SprayLevel,
    int LimeLevel,
    int RollerLevel,
    int PlowLevel,
    int StubbleShredLevel,
    int WaterLevel);

/// <summary>
/// One in-save farm. FS25 supports multiple farms.
/// </summary>
public sealed record Fs25FarmState(
    int FarmId,
    string Name,
    double Money,
    double Loan,
    double PlayTimeHours);

/// <summary>
/// The subset of <c>careerSavegame.xml</c> we care about.
/// </summary>
public sealed record Fs25CareerHeader(
    string SavegameName,
    string MapId,
    string MapTitle,
    DateOnly? SaveDate,
    string EconomicDifficulty,
    int PlannedDaysPerPeriod,
    bool LimeRequired,
    bool WeedsEnabled,
    bool PlowingRequired,
    IReadOnlyList<string> ActiveMods);

/// <summary>
/// The subset of <c>environment.xml</c> we care about.
/// </summary>
public sealed record Fs25EnvironmentState(
    int CurrentDay,
    int DaysPerPeriod,
    double DayTimeSeconds)
{
    /// <summary>
    /// The <see cref="Period"/> the save is in, derived from <paramref name="CurrentDay"/> and <paramref name="DaysPerPeriod"/>.
    /// Day 1 → period 1 (март). Day = <paramref name="DaysPerPeriod"/> + 1 → period 2. Wraps at 12.
    /// </summary>
    public Period Period => new Period(((CurrentDay - 1) / Math.Max(1, DaysPerPeriod)) % Period.Count + 1);

    /// <summary>Day within the current period, 1-based.</summary>
    public int DayInPeriod => (CurrentDay - 1) % Math.Max(1, DaysPerPeriod) + 1;
}

/// <summary>
/// Composed snapshot of the FS25 save at a moment in time. All numbers come straight from files
/// on disk — station prices and field-hectares are NOT here (see CLAUDE.md ловушка). Callers
/// should treat <see cref="Timestamp"/> as authoritative for freshness and <see cref="IsStale"/>
/// for whether the data should be shown as live.
/// </summary>
public sealed record Fs25Details(
    Fs25CareerHeader Header,
    Fs25EnvironmentState Environment,
    IReadOnlyList<Fs25FarmState> Farms,
    IReadOnlyList<Fs25FieldState> Fields,
    DateTime Timestamp,
    bool IsStale)
{
    public Fs25FarmState? PlayerFarm => Farms.FirstOrDefault();
    public Period Period => Environment.Period;
}

/// <summary>
/// Static parsers for FS25 savegame XML files. Pure Core: no file I/O beyond what the caller
/// hands in via <see cref="Stream"/>. Follows the <c>DEVELOPING-GAMES.md</c> contract:
/// «Парсинг в SimDeck.Core, чтение файла/сокета/SHM — в адаптере SimDeck.App.»
/// </summary>
public static class Fs25SaveParser
{
    public static Fs25CareerHeader ParseCareer(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var root = doc.Root ?? throw new InvalidDataException("careerSavegame has no root element.");
        var settings = root.Element("settings");
        DateOnly? date = null;
        if (DateOnly.TryParseExact((string?)settings?.Element("saveDate") ?? "", "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) date = d;
        var mods = root.Elements("mod")
            .Select(m => ((string?)m.Attribute("modName") ?? "").Trim())
            .Where(s => s.Length > 0)
            .ToList();

        return new Fs25CareerHeader(
            SavegameName: ((string?)settings?.Element("savegameName") ?? "").Trim(),
            MapId: ((string?)settings?.Element("mapId") ?? "").Trim(),
            MapTitle: ((string?)settings?.Element("mapTitle") ?? "").Trim(),
            SaveDate: date,
            EconomicDifficulty: ((string?)settings?.Element("economicDifficulty") ?? "").Trim(),
            PlannedDaysPerPeriod: (int?)settings?.Element("plannedDaysPerPeriod") ?? 0,
            LimeRequired: (bool?)settings?.Element("limeRequired") ?? false,
            WeedsEnabled: (bool?)settings?.Element("weedsEnabled") ?? false,
            PlowingRequired: (bool?)settings?.Element("plowingRequiredEnabled") ?? false,
            ActiveMods: mods);
    }

    public static Fs25EnvironmentState ParseEnvironment(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var root = doc.Root ?? throw new InvalidDataException("environment has no root element.");
        return new Fs25EnvironmentState(
            CurrentDay: (int?)root.Element("currentDay") ?? 1,
            DaysPerPeriod: Math.Max(1, (int?)root.Element("daysPerPeriod") ?? 1),
            DayTimeSeconds: (double?)root.Element("dayTime") ?? 0);
    }

    public static IReadOnlyList<Fs25FarmState> ParseFarms(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var root = doc.Root ?? throw new InvalidDataException("farms has no root element.");
        return root.Elements("farm").Select(f => new Fs25FarmState(
            FarmId: (int?)f.Attribute("farmId") ?? 0,
            Name: ((string?)f.Attribute("name") ?? "").Trim(),
            Money: (double?)f.Attribute("money") ?? 0,
            Loan: (double?)f.Attribute("loan") ?? 0,
            PlayTimeHours: (double?)f.Element("statistics")?.Element("playTime") ?? 0)).ToList();
    }

    public static IReadOnlyList<Fs25FieldState> ParseFields(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var root = doc.Root ?? throw new InvalidDataException("fields has no root element.");
        return root.Elements("field").Select(f => new Fs25FieldState(
            Id: (int?)f.Attribute("id") ?? 0,
            FruitType: ((string?)f.Attribute("fruitType") ?? "").Trim().ToUpperInvariant(),
            PlannedFruit: ((string?)f.Attribute("plannedFruit") ?? "").Trim().ToUpperInvariant(),
            GrowthState: (int?)f.Attribute("growthState") ?? 0,
            LastGrowthState: (int?)f.Attribute("lastGrowthState") ?? 0,
            WeedState: (int?)f.Attribute("weedState") ?? 0,
            StoneLevel: (int?)f.Attribute("stoneLevel") ?? 0,
            GroundType: ((string?)f.Attribute("groundType") ?? "").Trim().ToUpperInvariant(),
            SprayType: ((string?)f.Attribute("sprayType") ?? "").Trim().ToUpperInvariant(),
            SprayLevel: (int?)f.Attribute("sprayLevel") ?? 0,
            LimeLevel: (int?)f.Attribute("limeLevel") ?? 0,
            RollerLevel: (int?)f.Attribute("rollerLevel") ?? 0,
            PlowLevel: (int?)f.Attribute("plowLevel") ?? 0,
            StubbleShredLevel: (int?)f.Attribute("stubbleShredLevel") ?? 0,
            WaterLevel: (int?)f.Attribute("waterLevel") ?? 0)).ToList();
    }
}

/// <summary>
/// Composes a full <see cref="Fs25Details"/> from a savegame directory. Pure Core — the App
/// adapter is responsible for FileSystemWatcher, retries on lock, and <see cref="TelemetryHub"/>
/// publishing. See <c>docs/PLAN.md</c> pt 2.
/// </summary>
public static class Fs25SaveReader
{
    /// <summary>How old the save can be before <see cref="Fs25Details.IsStale"/> flips on.</summary>
    public static readonly TimeSpan FreshnessWindow = TimeSpan.FromMinutes(30);

    public static Fs25Details Read(Fs25SavegameDir dir, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        Fs25CareerHeader header;
        using (var s = File.OpenRead(dir.File("careerSavegame.xml"))) header = Fs25SaveParser.ParseCareer(s);
        Fs25EnvironmentState env;
        using (var s = File.OpenRead(dir.File("environment.xml"))) env = Fs25SaveParser.ParseEnvironment(s);
        IReadOnlyList<Fs25FarmState> farms;
        using (var s = File.OpenRead(dir.File("farms.xml"))) farms = Fs25SaveParser.ParseFarms(s);
        IReadOnlyList<Fs25FieldState> fields;
        using (var s = File.OpenRead(dir.File("fields.xml"))) fields = Fs25SaveParser.ParseFields(s);

        var timestamp = dir.LastSaved;
        var stale = now - timestamp > FreshnessWindow;
        return new Fs25Details(header, env, farms, fields, timestamp, stale);
    }
}
