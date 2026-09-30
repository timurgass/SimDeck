using System.IO;
using System.Text;
using SimDeck.Core;

static class Fs25RulesTests
{
    static Fs25Details MakeDetails(Period period, params Fs25FieldState[] fields)
    {
        var header = new Fs25CareerHeader(
            "test", "MapEU", "Zielonka",
            new DateOnly(2026, 1, 1), "NORMAL", 2, false, true, false, []);
        // Compute currentDay from period so PeriodProperty matches.
        var currentDay = (period.Number - 1) * 2 + 1;
        var env = new Fs25EnvironmentState(currentDay, 2, 0);
        var farms = new[] { new Fs25FarmState(1, "TestFarm", 500_000, 200_000, 10) };
        return new Fs25Details(header, env, farms, fields, DateTime.UtcNow, IsStale: false);
    }

    static Fs25FieldState Field(int id, string fruit, int weeds = 0, int lime = 2, int spray = 2,
        string groundType = "SOWN", int growth = 3, int plow = 1, string plannedFruit = "FALLOW")
        => new(id, fruit, plannedFruit, growth, 0, weeds, 0, groundType, "NONE", spray, lime, 0, plow, 0, 0);

    static Fs25Plan LoadPlan(string json)
    {
        using var s = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return Fs25PlanLoader.Load(s);
    }

    public static void Run(Action<bool, string> check)
    {
        // ---- Alert: weeds crisis fires independent of any plan ----

        var d1 = MakeDetails(new Period(7), Field(1, "POTATO", weeds: 9, lime: 0));
        var r1 = Fs25Rules.Evaluate(d1, plan: null);
        check(r1.Alerts.Any(a => a.Field == 1 && a.Severity == Fs25AlertSeverity.Crisis && a.Category == "weeds"),
            "Weeds ≥ 8 fires a crisis alert (no plan needed)");
        check(r1.Alerts.Any(a => a.Field == 1 && a.Category == "lime"),
            "Lime = 0 with growing crop fires an alert");
        check(!r1.PlanApplies && r1.Tasks.Count == 0,
            "No plan ⇒ PlanApplies=false and no task evaluations");

        // ---- Alert: HARVEST_READY info alert ----

        var d2 = MakeDetails(new Period(6), Field(1, "WHEAT", weeds: 1, lime: 2, groundType: "HARVEST_READY", growth: 8));
        var r2 = Fs25Rules.Evaluate(d2, plan: null);
        check(r2.Alerts.Any(a => a.Category == "harvest" && a.Severity == Fs25AlertSeverity.Info),
            "HARVEST_READY produces an info alert");

        // ---- Plan out of season ----

        var autumnPlan = LoadPlan("""
            {
              "planVersion": 1, "name": "Осень",
              "forPeriods": [7, 8, 9],
              "tasks": [{ "id":"t1", "title":"Убрать пшеницу с поля 1", "why":"—", "priority":1, "field":1 }]
            }
            """);
        var dSpring = MakeDetails(new Period(3), Field(1, "WHEAT"));
        var rSpring = Fs25Rules.Evaluate(dSpring, autumnPlan);
        check(!rSpring.PlanApplies,
            "Autumn plan does not apply in period 3 (май)");
        check(rSpring.Tasks[0].Status == Fs25TaskStatus.OutOfSeason,
            "Task from off-season plan is marked OutOfSeason");

        // ---- Plan in-season: task active vs done ----

        var dAutumnActive = MakeDetails(new Period(7),
            Field(1, "WHEAT", weeds: 1, lime: 2, groundType: "HARVEST_READY", growth: 8));
        var rActive = Fs25Rules.Evaluate(dAutumnActive, autumnPlan);
        check(rActive.PlanApplies && rActive.Tasks[0].Status == Fs25TaskStatus.Active,
            "Wheat HARVEST_READY: 'убрать' task is Active until field goes CUT");

        var dAutumnDone = MakeDetails(new Period(7),
            Field(1, fruit: "", weeds: 0, lime: 2, groundType: "CUT", growth: 0));
        var rDone = Fs25Rules.Evaluate(dAutumnDone, autumnPlan);
        check(rDone.Tasks[0].Status == Fs25TaskStatus.Done,
            "'убрать' task flips to Done when GroundType == CUT");

        // ---- Missing field ----

        var missingPlan = LoadPlan("""
            {
              "planVersion": 1, "name": "x", "forPeriods": [7],
              "tasks": [{ "id":"m", "title":"Что-то на поле 99", "why":"—", "priority":1, "field":99 }]
            }
            """);
        var rMissing = Fs25Rules.Evaluate(MakeDetails(new Period(7), Field(1, "WHEAT")), missingPlan);
        check(rMissing.Tasks[0].Status == Fs25TaskStatus.FieldMissing,
            "Task referencing a field not in the save is marked FieldMissing");

        // ---- Off-season planting alert (needs catalog) ----

        // Fake catalog: wheat planting allowed in periods 7,8 only.
        var catalog = new FakeCatalog();
        catalog.AllowedPeriods["WHEAT"] = new HashSet<int> { 7, 8 };
        var dOffSeason = MakeDetails(new Period(3), Field(1, fruit: "", plannedFruit: "WHEAT"));
        var rOff = Fs25Rules.Evaluate(dOffSeason, plan: null, catalog: catalog);
        check(rOff.Alerts.Any(a => a.Category == "off-season" && a.Field == 1),
            "Planned WHEAT in period 3 (май) fires an off-season alert (winter grain window is 7–8)");

        // ---- Cash-vs-loan alert ----

        var poorHeader = new Fs25CareerHeader("t", "MapEU", "Z", new DateOnly(2026,1,1), "HARD", 2, false, true, false, []);
        var poorEnv = new Fs25EnvironmentState(1, 2, 0);
        var poorFarms = new[] { new Fs25FarmState(1, "Broke", 40_000, 250_000, 20) };
        var dPoor = new Fs25Details(poorHeader, poorEnv, poorFarms, new Fs25FieldState[0], DateTime.UtcNow, false);
        var rPoor = Fs25Rules.Evaluate(dPoor, plan: null);
        check(rPoor.Alerts.Any(a => a.Category == "cash"),
            "Cash below 25% of loan raises a cash alert");

        // ---- Staleness banner ----

        var dStale = MakeDetails(new Period(7), Field(1, "WHEAT")) with { IsStale = true };
        var rStale = Fs25Rules.Evaluate(dStale, plan: null);
        check(rStale.StaleBanner is not null && rStale.StaleBanner.Category == "stale",
            "IsStale details produce a top-of-screen staleness banner");

        // ---- Real crop catalog integration: wheat isn't a spring grain ----

        var dataRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", "fs25", "data-1.23.1.0");
        if (!File.Exists(Path.Combine(dataRoot, "foliage", "wheat", "wheat.xml"))) return;
        using var wheatStream = File.OpenRead(Path.Combine(dataRoot, "foliage", "wheat", "wheat.xml"));
        var wheatCrop = Fs25CropParser.Parse(wheatStream);
        var realCatalog = new Fs25CropListCatalogView(new[] { wheatCrop });
        var dRealSpring = MakeDetails(new Period(3), Field(1, fruit: "", plannedFruit: "wheat"));
        var rRealSpring = Fs25Rules.Evaluate(dRealSpring, plan: null, catalog: realCatalog);
        check(rRealSpring.Alerts.Any(a => a.Category == "off-season"),
            "Real wheat catalog: planning WHEAT in май (period 3) triggers off-season alert — озимая");
        var dAutumn7 = MakeDetails(new Period(7), Field(1, fruit: "", plannedFruit: "wheat"));
        var rAutumn = Fs25Rules.Evaluate(dAutumn7, plan: null, catalog: realCatalog);
        check(!rAutumn.Alerts.Any(a => a.Category == "off-season" && a.Field == 1),
            "Wheat in период 7 (сентябрь) is inside its planting window — no off-season alert");
    }

    sealed class FakeCatalog : Fs25CropCatalogView
    {
        public Dictionary<string, HashSet<int>> AllowedPeriods { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool? PlantingAllowed(string cropName, Period period) =>
            AllowedPeriods.TryGetValue(cropName, out var set) ? set.Contains(period.Number) : null;
    }
}
