using System.IO;
using SimDeck.Core;

static class Fs25SaveTests
{
    static readonly string CleanSave = Path.Combine(AppContext.BaseDirectory, "fixtures", "fs25", "save-clean");
    static readonly string MidSeasonSave = Path.Combine(AppContext.BaseDirectory, "fixtures", "fs25", "save-mid-season");

    public static void Run(Action<bool, string> check)
    {
        // ---- Clean save (day 1) ----

        Fs25CareerHeader header;
        using (var s = File.OpenRead(Path.Combine(CleanSave, "careerSavegame.xml")))
            header = Fs25SaveParser.ParseCareer(s);
        check(header.SavegameName == "test-clean" && header.MapId == "MapEU"
            && header.MapTitle == "Zielonka",
            "Career header pulls savegame name and map metadata");
        check(header.SaveDate == new DateOnly(2026, 1, 1),
            "Career header parses saveDate as DateOnly (ISO format)");
        check(header.EconomicDifficulty == "HARD" && header.PlannedDaysPerPeriod == 2,
            "Career header exposes economic difficulty and daysPerPeriod for the advisor");
        check(header.LimeRequired && header.WeedsEnabled && !header.PlowingRequired,
            "Career header parses the three гайку-flags — lime, weeds, plow");
        check(header.ActiveMods.Count == 0, "Clean save has no active mods");

        Fs25EnvironmentState env;
        using (var s = File.OpenRead(Path.Combine(CleanSave, "environment.xml")))
            env = Fs25SaveParser.ParseEnvironment(s);
        check(env.CurrentDay == 1 && env.DaysPerPeriod == 2,
            "Environment pulls currentDay and daysPerPeriod");
        check(env.Period.Number == 1 && env.Period.RussianMonth == "март",
            "Day 1, daysPerPeriod 2 → period 1 (март)");
        check(env.DayInPeriod == 1,
            "Day 1 is day-in-period 1");

        IReadOnlyList<Fs25FarmState> farms;
        using (var s = File.OpenRead(Path.Combine(CleanSave, "farms.xml")))
            farms = Fs25SaveParser.ParseFarms(s);
        check(farms.Count == 1, "Clean save has exactly one farm");
        check(farms[0].FarmId == 1 && farms[0].Name == "TestFarm"
            && Math.Abs(farms[0].Money - 1_000_000) < 0.001
            && Math.Abs(farms[0].Loan - 250_000) < 0.001,
            "Farm parses id/name/money/loan from XML attributes");
        check(farms[0].PlayTimeHours == 0, "Clean save has zero playtime");

        IReadOnlyList<Fs25FieldState> fields;
        using (var s = File.OpenRead(Path.Combine(CleanSave, "fields.xml")))
            fields = Fs25SaveParser.ParseFields(s);
        check(fields.Count == 2, "Clean save has two fallow fields");
        check(fields.All(f => f.FruitType == "" && f.GrowthState == 0 && f.WeedState == 0),
            "Fallow fields have no fruit, no growth, no weeds");

        // ---- Mid-season save (day 15, period 8) ----

        Fs25Details details;
        using (var _ = new StreamReader(Path.Combine(MidSeasonSave, "careerSavegame.xml"))) { /* ensure files exist */ }
        details = Fs25SaveReader.Read(new Fs25SavegameDir(MidSeasonSave));

        check(details.Header.SavegameName == "test-mid-season",
            "SaveReader combines all four files into one Fs25Details");
        check(details.Environment.CurrentDay == 15 && details.Environment.DaysPerPeriod == 2,
            "SaveReader picks up environment state");
        check(details.Period.Number == 8 && details.Period.GameName == "MID_AUTUMN",
            "Day 15 with daysPerPeriod=2 → period 8 (MID_AUTUMN, октябрь)");
        check(details.Environment.DayInPeriod == 1,
            "Day 15 is day 1 of period 8 (15 = 7 × 2 + 1)");
        var farm = details.PlayerFarm!;
        check(farm.FarmId == 1 && Math.Abs(farm.Money - 856_000) < 0.001,
            "Mid-season money 856 000 € after 32 hours");
        check(Math.Abs(farm.Loan - 220_000) < 0.001,
            "Loan reduced from 250к to 220к in the fixture");
        check(details.Header.ActiveMods.SequenceEqual(new[] { "pdlc_strawHarvestPack", "pdlc_vredoPack" }),
            "Active mods list is preserved in order");

        // Field-level: wheat harvest-ready with weeds, potato in crisis, sugar beet OK, fallow field
        check(details.Fields.Count == 4, "Mid-season save has four fields");
        var wheat = details.Fields.Single(f => f.Id == 1);
        check(wheat.FruitType == "WHEAT" && wheat.GrowthState == 8 && wheat.GroundType == "HARVEST_READY"
            && wheat.WeedState == 6,
            "Wheat field is HARVEST_READY at growthState 8 with weeds needing attention");
        var potato = details.Fields.Single(f => f.Id == 2);
        check(potato.FruitType == "POTATO" && potato.WeedState == 9 && potato.LimeLevel == 0
            && potato.SprayLevel == 1,
            "Potato field is in crisis: weeds 9, lime 0 — advisor SHOULD flag this");
        var beet = details.Fields.Single(f => f.Id == 3);
        check(beet.FruitType == "SUGARBEET" && beet.WeedState == 1 && beet.LimeLevel == 2,
            "Sugar beet field is healthy: low weeds, lime OK");
        var fallow = details.Fields.Single(f => f.Id == 4);
        check(fallow.FruitType == "" && fallow.PlowLevel == 1,
            "Fallow field has no fruit but plow marker survives");

        // ---- Cross-catalog: decode growthState via crop catalog ----

        var dataRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", "fs25", "data-1.23.1.0");
        if (File.Exists(Path.Combine(dataRoot, "foliage", "wheat", "wheat.xml")) &&
            File.Exists(Path.Combine(dataRoot, "foliage", "potato", "potato.xml")))
        {
        Fs25Crop wheatCrop, potatoCrop;
        using (var s = File.OpenRead(Path.Combine(dataRoot, "foliage", "wheat", "wheat.xml")))
            wheatCrop = Fs25CropParser.Parse(s);
        using (var s = File.OpenRead(Path.Combine(dataRoot, "foliage", "potato", "potato.xml")))
            potatoCrop = Fs25CropParser.Parse(s);

        // Field 1: wheat with growthState 8 → wheatCrop.StageAt(8) is "harvestReady".
        check(wheatCrop.StageAt(wheat.GrowthState) == "harvestReady",
            "Field 1: wheat growthState 8 decodes to 'harvestReady' — ready to combine");
        // Field 2: potato with growthState 5 → potatoCrop.StageAt(5) is "greenBig" (still growing).
        // Potato's harvestReady sits at index 6, not 8 — different foliage lengths per crop matter.
        check(potatoCrop.StageAt(potato.GrowthState) == "greenBig",
            "Field 2: potato growthState 5 is still 'greenBig' — the crisis is weeds, not maturity");
        // Same numeric growthState → different stage names across crops.
        check(wheatCrop.StageAt(5) != potatoCrop.StageAt(5),
            $"growthState 5 decodes differently: wheat='{wheatCrop.StageAt(5)}', potato='{potatoCrop.StageAt(5)}'");
        // Where is potato's harvestReady in its own list?
        check(potatoCrop.FoliageStates[5] == "harvestReady" && wheatCrop.FoliageStates[7] == "harvestReady",
            "Potato harvestReady is growthState 6 (index 5), wheat is growthState 8 (index 7) — per-crop resolution required");
        }

        // ---- Staleness ----

        var freshTs = details.Timestamp; // whatever the fixture files were last written
        var stalePoint = freshTs + Fs25SaveReader.FreshnessWindow + TimeSpan.FromMinutes(1);
        var reloaded = Fs25SaveReader.Read(new Fs25SavegameDir(MidSeasonSave), nowUtc: stalePoint);
        check(reloaded.IsStale,
            "Save reader marks details stale when now is beyond FreshnessWindow after file mtime");
        var freshPoint = freshTs + TimeSpan.FromMinutes(1);
        var freshReloaded = Fs25SaveReader.Read(new Fs25SavegameDir(MidSeasonSave), nowUtc: freshPoint);
        check(!freshReloaded.IsStale,
            "Save reader keeps details live when nowUtc is inside FreshnessWindow");

        // ---- Real player save (optional integration test) ----

        var userDir = Fs25GamePaths.FindUserDataDir();
        if (userDir is not null)
        {
            var saves = Fs25GamePaths.FindSavegames(userDir);
            if (saves.Count > 0)
            {
                var real = Fs25SaveReader.Read(saves[0]);
                // No assertions on a player's private save values.
                // Just: parseable, period is in range, has at least one farm.
                check(real.PlayerFarm is not null && real.Period.Number >= 1 && real.Period.Number <= 12,
                    $"Real save {saves[0].Name} parses: farm {real.PlayerFarm?.Name ?? "?"}, {real.Fields.Count} fields, period {real.Period.GameName}");
            }
            else
            {
                check(true, "Optional real-save test skipped: no savegame in user folder");
            }
        }
        else
        {
            check(true, "Optional real-save test skipped: no FS25 user data folder");
        }
    }
}
