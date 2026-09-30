using System.Text.Json.Serialization;

namespace SimDeck.Core;

/// <summary>Four calendar seasons; index matches ((period-1)/3).</summary>
public enum Season { Spring, Summer, Autumn, Winter }

/// <summary>
/// One of the twelve FS25 growth periods. Period 1 is March — confirmed by the
/// inline comments GIANTS left in <c>data/maps/maps_fillTypes.xml</c>, where the price
/// factors are annotated <c>&lt;factor period="1"/&gt;&lt;!-- march --&gt;</c> through
/// <c>&lt;factor period="12"/&gt;&lt;!-- feruary --&gt;</c> (their typo, not ours).
/// </summary>
/// <remarks>
/// The number is 1-based to match the game files. Arrays in code are 0-based, so
/// always subtract one when indexing. A common pitfall is that:
/// «Период 1 в FS25 — март, не январь. Номер периода с единицы, массивы с нуля.»
/// </remarks>
public readonly record struct Period
{
    public const int Count = 12;

    private static readonly string[] GameNames =
    [
        "EARLY_SPRING", "MID_SPRING", "LATE_SPRING",
        "EARLY_SUMMER", "MID_SUMMER", "LATE_SUMMER",
        "EARLY_AUTUMN", "MID_AUTUMN", "LATE_AUTUMN",
        "EARLY_WINTER", "MID_WINTER", "LATE_WINTER",
    ];

    private static readonly string[] RussianMonths =
    [
        "март", "апрель", "май", "июнь", "июль", "август",
        "сентябрь", "октябрь", "ноябрь", "декабрь", "январь", "февраль",
    ];

    public Period(int number)
    {
        if (number < 1 || number > Count)
            throw new ArgumentOutOfRangeException(nameof(number), number, "Period must be 1..12.");
        Number = number;
    }

    public int Number { get; }

    public string GameName => GameNames[Number - 1];

    /// <summary>Russian month name in nominative case, for Весельчак's prose.</summary>
    public string RussianMonth => RussianMonths[Number - 1];

    public Season Season => (Season)((Number - 1) / 3);

    /// <summary>Real-world month number, 1 = January. Period 1 (EARLY_SPRING) is March.</summary>
    public int CalendarMonth => ((Number + 1) % Count) + 1;

    [JsonIgnore]
    public Period Next => Offset(1);

    public Period Offset(int periods)
    {
        var zero = ((Number - 1 + periods) % Count + Count) % Count;
        return new Period(zero + 1);
    }

    /// <summary>How many period steps forward from this period to <paramref name="target"/>. Zero when equal.</summary>
    public int StepsUntil(Period target) => ((target.Number - Number) % Count + Count) % Count;

    public static bool TryParse(string? gameName, out Period period)
    {
        var index = Array.IndexOf(GameNames, gameName?.Trim().ToUpperInvariant());
        period = index >= 0 ? new Period(index + 1) : default;
        return index >= 0;
    }

    public static Period Parse(string gameName) => TryParse(gameName, out var p)
        ? p
        : throw new FormatException($"Unknown FS25 period name '{gameName}'.");

    public static IEnumerable<Period> All => Enumerable.Range(1, Count).Select(n => new Period(n));

    public override string ToString() => $"{GameName} ({RussianMonth})";
}

public static class SeasonNames
{
    public static string InRussian(this Season season) => season switch
    {
        Season.Spring => "весна",
        Season.Summer => "лето",
        Season.Autumn => "осень",
        Season.Winter => "зима",
        _ => season.ToString(),
    };

    public static bool TryParse(string? name, out Season season)
    {
        switch (name?.Trim().ToUpperInvariant())
        {
            case "SPRING": season = Season.Spring; return true;
            case "SUMMER": season = Season.Summer; return true;
            case "AUTUMN": season = Season.Autumn; return true;
            case "WINTER": season = Season.Winter; return true;
            default: season = default; return false;
        }
    }
}
