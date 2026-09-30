using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimDeck.Core;

/// <summary>
/// Season plan authored offline by the user (via an AI chat) and loaded from a local
/// <c>.json</c> file. See <c>docs/FS25-SEASON-PLAN.md</c>. The plan cannot see live save
/// state — it only carries the season's line and Весельчак's letters; the <see cref="Fs25Rules"/>
/// engine crosses it with <see cref="Fs25Details"/> to produce actual advice.
/// </summary>
public sealed record Fs25Plan(
    int PlanVersion,
    string Name,
    IReadOnlyList<int> ForPeriods,
    IReadOnlyList<Fs25PlanTask> Tasks,
    IReadOnlyList<Fs25PlanDont> Dont,
    IReadOnlyList<Fs25PlanLetter> Letters)
{
    public bool AppliesTo(Period period) => ForPeriods.Contains(period.Number);
}

public sealed record Fs25PlanTask(
    string Id, string Title, string Why, string? Kit,
    int Priority, int? Field, string? DeckPage);

public sealed record Fs25PlanDont(string Title, string Why);

public sealed record Fs25PlanLetter(
    string Id, string Date, string Subject, IReadOnlyList<string> Body, string Sign);

/// <summary>Structured parse error with the exact user-friendly line for the UI.</summary>
public sealed class Fs25PlanException : Exception
{
    public Fs25PlanException(string message) : base(message) { }
    public Fs25PlanException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Loads a plan from a JSON stream and validates it against the 0.1 schema. Errors are
/// user-facing (Russian) because the failure surface is the FS25 screen, not developer logs.
/// The loader is strict: on any error it throws — the App keeps the previous plan alive
/// (see <c>FS25-SEASON-PLAN.md §8</c>).
/// </summary>
public static class Fs25PlanLoader
{
    public const int SupportedVersion = 1;

    public static readonly IReadOnlySet<string> AllowedDeckPages =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Орудие", "Посев", "Трактор", "Погрузчик", "Помощник", "Хозяйство",
        };

    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        NumberHandling = JsonNumberHandling.Strict,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Fs25Plan Load(Stream stream)
    {
        Fs25PlanDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<Fs25PlanDto>(stream, Options);
        }
        catch (JsonException ex)
        {
            throw new Fs25PlanException(FriendlyJsonError(ex), ex);
        }
        if (dto is null) throw new Fs25PlanException("Файл пустой или не содержит JSON.");
        return Validate(dto);
    }

    public static Fs25Plan LoadFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Load(stream);
        }
        catch (IOException ex)
        {
            throw new Fs25PlanException($"Не удалось прочитать файл: {ex.Message}", ex);
        }
    }

    static Fs25Plan Validate(Fs25PlanDto dto)
    {
        if (dto.PlanVersion != SupportedVersion)
            throw new Fs25PlanException(
                $"Версия плана {dto.PlanVersion} не поддерживается. Ожидается {SupportedVersion}.");
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new Fs25PlanException("В плане отсутствует поле «name».");
        if (dto.ForPeriods is null || dto.ForPeriods.Count == 0)
            throw new Fs25PlanException("В плане отсутствует «forPeriods» или он пустой.");
        foreach (var p in dto.ForPeriods)
            if (p is < 1 or > 12)
                throw new Fs25PlanException($"forPeriods: {p} вне диапазона 1..12.");
        if (dto.Tasks is null || dto.Tasks.Count == 0)
            throw new Fs25PlanException("В плане отсутствует «tasks» или он пустой.");

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var tasks = new List<Fs25PlanTask>(dto.Tasks.Count);
        foreach (var t in dto.Tasks)
        {
            if (string.IsNullOrWhiteSpace(t.Id))
                throw new Fs25PlanException("У задачи отсутствует «id».");
            if (!seenIds.Add(t.Id))
                throw new Fs25PlanException($"Повторяющийся id задачи: «{t.Id}».");
            if (string.IsNullOrWhiteSpace(t.Title))
                throw new Fs25PlanException($"Задача «{t.Id}»: пустой «title».");
            if (string.IsNullOrWhiteSpace(t.Why))
                throw new Fs25PlanException($"Задача «{t.Id}»: пустой «why».");
            if (t.Priority is < 1 or > 3)
                throw new Fs25PlanException(
                    $"Задача «{t.Id}»: priority {t.Priority}. Разрешено 1, 2 или 3 (0 ставит приложение само).");
            if (t.DeckPage is not null && !AllowedDeckPages.Contains(t.DeckPage))
                throw new Fs25PlanException(
                    $"Задача «{t.Id}»: deckPage «{t.DeckPage}» не из списка (Орудие/Посев/Трактор/Погрузчик/Помощник/Хозяйство).");
            tasks.Add(new(t.Id, t.Title.Trim(), t.Why.Trim(), t.Kit?.Trim(),
                t.Priority, t.Field, t.DeckPage));
        }

        var donts = (dto.Dont ?? [])
            .Select((d, i) =>
            {
                if (string.IsNullOrWhiteSpace(d.Title))
                    throw new Fs25PlanException($"dont[{i}]: пустой «title».");
                if (string.IsNullOrWhiteSpace(d.Why))
                    throw new Fs25PlanException($"dont[{i}]: пустой «why».");
                return new Fs25PlanDont(d.Title.Trim(), d.Why.Trim());
            }).ToList();

        var letterIds = new HashSet<string>(StringComparer.Ordinal);
        var letters = (dto.Letters ?? [])
            .Select((l, i) =>
            {
                if (string.IsNullOrWhiteSpace(l.Id))
                    throw new Fs25PlanException($"letters[{i}]: пустой «id».");
                if (!letterIds.Add(l.Id))
                    throw new Fs25PlanException($"Повторяющийся id письма: «{l.Id}».");
                if (l.Body is null || l.Body.Count == 0)
                    throw new Fs25PlanException($"Письмо «{l.Id}»: пустой «body».");
                return new Fs25PlanLetter(l.Id, l.Date ?? "", l.Subject ?? "",
                    l.Body.Select(b => b ?? "").ToList(), l.Sign ?? "");
            }).ToList();

        return new Fs25Plan(dto.PlanVersion, dto.Name.Trim(),
            dto.ForPeriods.Distinct().OrderBy(x => x).ToList(),
            tasks, donts, letters);
    }

    static string FriendlyJsonError(JsonException ex)
    {
        var loc = ex.LineNumber is { } line
            ? $" (строка {line + 1}, позиция {ex.BytePositionInLine + 1})"
            : "";
        return $"JSON не разобрать{loc}: {ex.Message}. Часто причина — обёртка ```json``` " +
               "вокруг файла или запятая перед закрывающей скобкой.";
    }

    // DTO layer keeps the record contract stable even if authors add extra keys.
    sealed class Fs25PlanDto
    {
        public int PlanVersion { get; set; }
        public string Name { get; set; } = "";
        public List<int>? ForPeriods { get; set; }
        public List<TaskDto>? Tasks { get; set; }
        public List<DontDto>? Dont { get; set; }
        public List<LetterDto>? Letters { get; set; }
    }
    sealed class TaskDto
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Why { get; set; } = "";
        public string? Kit { get; set; }
        public int Priority { get; set; }
        public int? Field { get; set; }
        public string? DeckPage { get; set; }
    }
    sealed class DontDto { public string Title { get; set; } = ""; public string Why { get; set; } = ""; }
    sealed class LetterDto
    {
        public string Id { get; set; } = "";
        public string Date { get; set; } = "";
        public string Subject { get; set; } = "";
        public List<string>? Body { get; set; }
        public string Sign { get; set; } = "";
    }
}
