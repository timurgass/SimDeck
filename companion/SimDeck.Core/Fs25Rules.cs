using System.Text.Json.Serialization;

namespace SimDeck.Core;

/// <summary>
/// Cross-references a live <see cref="Fs25Details"/> with an optional <see cref="Fs25Plan"/>
/// and produces a report the phone screens render as-is. Pure Core — no I/O, no clocks:
/// the App decides when to re-evaluate. See <c>docs/FS25-SEASON-PLAN.md §1</c>: the rules
/// engine answers "what is on the fields now"; the plan answers "where to go this season."
/// </summary>
public static class Fs25Rules
{
    /// <summary>Weeds level (0..9) at or above which an alert fires. FS25 stops fruit growth around 5–6.</summary>
    public const int WeedAlertLevel = 5;
    /// <summary>Weeds level considered a crisis — same colour as fires and floods.</summary>
    public const int WeedCrisisLevel = 8;

    public static Fs25AdvisorReport Evaluate(Fs25Details details, Fs25Plan? plan, Fs25CropCatalogView? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(details);
        var period = details.Period;
        var planApplies = plan is not null && plan.AppliesTo(period);
        var fieldsById = details.Fields.ToDictionary(f => f.Id);

        // Task evaluation.
        var evaluations = new List<Fs25TaskEvaluation>();
        if (plan is not null)
        {
            foreach (var task in plan.Tasks)
            {
                evaluations.Add(EvaluateTask(task, plan, period, fieldsById));
            }
        }

        // Auto-detected alerts (independent of plan).
        var alerts = new List<Fs25AdvisorAlert>();
        foreach (var field in details.Fields)
        {
            if (field.WeedState >= WeedCrisisLevel)
                alerts.Add(new(field.Id, Fs25AlertSeverity.Crisis, "weeds",
                    $"Поле {field.Id}: сорняки {field.WeedState}. Урожай под угрозой. Химия или прополка сейчас."));
            else if (field.WeedState >= WeedAlertLevel && !string.IsNullOrEmpty(field.FruitType))
                alerts.Add(new(field.Id, Fs25AlertSeverity.Warning, "weeds",
                    $"Поле {field.Id}: сорняки {field.WeedState}. Рост {field.FruitType} замедляется."));

            if (!string.IsNullOrEmpty(field.FruitType) && field.LimeLevel == 0)
                alerts.Add(new(field.Id, Fs25AlertSeverity.Warning, "lime",
                    $"Поле {field.Id}: известь на нуле. {field.FruitType} даст меньше или уйдёт."));

            if (field.GroundType == "HARVEST_READY")
                alerts.Add(new(field.Id, Fs25AlertSeverity.Info, "harvest",
                    $"Поле {field.Id}: {field.FruitType} готов к уборке. Не тяните дольше периода."));

            if (catalog is not null && !string.IsNullOrEmpty(field.PlannedFruit)
                && field.PlannedFruit != "FALLOW" && catalog.PlantingAllowed(field.PlannedFruit, period) is false)
                alerts.Add(new(field.Id, Fs25AlertSeverity.Warning, "off-season",
                    $"Поле {field.Id}: план посеять {field.PlannedFruit} — но в {period.RussianMonth} окно закрыто."));
        }

        // Farm-level alerts.
        if (details.PlayerFarm is { } farm)
        {
            if (farm.Loan > 0 && farm.Money < farm.Loan / 4)
                alerts.Add(new(null, Fs25AlertSeverity.Warning, "cash",
                    $"Наличных {farm.Money:N0} € против кредита {farm.Loan:N0} €. Проценты капают ежедневно."));
        }

        // Staleness ⇒ banner.
        Fs25AdvisorAlert? staleBanner = details.IsStale
            ? new(null, Fs25AlertSeverity.Info, "stale",
                "Данные сейва устарели — либо игра не запущена, либо давно не сохранялись.")
            : null;

        return new Fs25AdvisorReport(period, planApplies, evaluations, alerts, staleBanner, plan);
    }

    static Fs25TaskEvaluation EvaluateTask(Fs25PlanTask task, Fs25Plan plan, Period period,
        IReadOnlyDictionary<int, Fs25FieldState> fields)
    {
        if (!plan.AppliesTo(period))
            return new(task, Fs25TaskStatus.OutOfSeason,
                $"План на периоды {string.Join(", ", plan.ForPeriods)}, сейчас {period.Number}.");

        if (task.Field is null)
            return new(task, Fs25TaskStatus.Active, null); // No field ⇒ can't infer state.

        if (!fields.TryGetValue(task.Field.Value, out var field))
            return new(task, Fs25TaskStatus.FieldMissing,
                $"Поле {task.Field} не найдено в сейве.");

        var title = task.Title.ToLowerInvariant();
        // Cheap heuristics — plan authors know what verb they used, this is best-effort.
        if (ContainsAny(title, "убрать", "harvest", "сжать") && field.GroundType == "CUT")
            return new(task, Fs25TaskStatus.Done, $"Поле {field.Id}: сжато.");
        if (ContainsAny(title, "убрать", "harvest") && string.IsNullOrEmpty(field.FruitType) && field.GroundType != "HARVEST_READY")
            return new(task, Fs25TaskStatus.Done, $"Поле {field.Id}: пусто, уборка завершена.");
        if (ContainsAny(title, "вспахать", "plow", "плуг") && field.PlowLevel > 0 && field.GroundType != "HARVEST_READY")
            return new(task, Fs25TaskStatus.Done, $"Поле {field.Id}: вспахано.");
        if (ContainsAny(title, "посе", "seed", "sow") && !string.IsNullOrEmpty(field.FruitType))
            return new(task, Fs25TaskStatus.Done, $"Поле {field.Id}: посеяно ({field.FruitType}).");
        if (ContainsAny(title, "извест", "lime") && field.LimeLevel >= 2)
            return new(task, Fs25TaskStatus.Done, $"Поле {field.Id}: известь {field.LimeLevel}/3.");
        if (ContainsAny(title, "удобр", "fertil") && field.SprayLevel >= 2)
            return new(task, Fs25TaskStatus.Done, $"Поле {field.Id}: удобрено {field.SprayLevel}/3.");

        return new(task, Fs25TaskStatus.Active,
            $"Поле {field.Id}: {(string.IsNullOrEmpty(field.FruitType) ? "пусто" : field.FruitType)}, "
            + $"сорняки {field.WeedState}, известь {field.LimeLevel}.");
    }

    static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles) if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

/// <summary>What the phone screen renders: current period, plan status, per-task state, live alerts.</summary>
public sealed record Fs25AdvisorReport(
    Period CurrentPeriod,
    bool PlanApplies,
    IReadOnlyList<Fs25TaskEvaluation> Tasks,
    IReadOnlyList<Fs25AdvisorAlert> Alerts,
    Fs25AdvisorAlert? StaleBanner,
    [property: JsonIgnore] Fs25Plan? Plan)
{
    public string? PlanName => Plan?.Name;
    [JsonIgnore]
    public IReadOnlyList<Fs25TaskEvaluation> ActiveTasks => Tasks.Where(t => t.Status == Fs25TaskStatus.Active).ToList();
    [JsonIgnore]
    public IReadOnlyList<Fs25TaskEvaluation> DoneTasks => Tasks.Where(t => t.Status == Fs25TaskStatus.Done).ToList();
    [JsonIgnore]
    public IReadOnlyList<Fs25AdvisorAlert> AlertsBySeverity =>
        Alerts.OrderByDescending(a => (int)a.Severity).ToList();
}

public enum Fs25TaskStatus
{
    Active,        // Not yet done, in-season.
    Done,          // Field state satisfies the task (heuristic).
    OutOfSeason,   // Current period is outside plan.forPeriods.
    FieldMissing,  // Task references a field that isn't in the save.
}

public enum Fs25AlertSeverity { Info = 0, Warning = 1, Crisis = 2 }

public sealed record Fs25TaskEvaluation(Fs25PlanTask Task, Fs25TaskStatus Status, string? Detail);

public sealed record Fs25AdvisorAlert(int? Field, Fs25AlertSeverity Severity, string Category, string Message);

/// <summary>
/// Minimal read-only view of the crop catalog the rules engine needs. Keeps
/// <see cref="Fs25Rules"/> testable without wiring the full parser.
/// </summary>
public interface Fs25CropCatalogView
{
    /// <summary>Returns null if the crop is unknown; true/false if the crop knows this period.</summary>
    bool? PlantingAllowed(string cropName, Period period);
}

/// <summary>Adapter that turns a list of <see cref="Fs25Crop"/> into a <see cref="Fs25CropCatalogView"/>.</summary>
public sealed class Fs25CropListCatalogView(IReadOnlyList<Fs25Crop> crops) : Fs25CropCatalogView
{
    readonly Dictionary<string, Fs25Crop> byName =
        crops.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

    public bool? PlantingAllowed(string cropName, Period period)
    {
        if (!byName.TryGetValue(cropName, out var crop)) return null;
        foreach (var p in crop.PlantingPeriods)
            if (p.Number == period.Number) return true;
        return false;
    }
}
