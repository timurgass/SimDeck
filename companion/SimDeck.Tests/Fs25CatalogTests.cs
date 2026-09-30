using System.IO;
using SimDeck.Core;

static class Fs25CatalogTests
{
    // Fixtures live in SimDeck.Tests/fixtures/fs25/data-1.23.1.0/ and are copied to
    // AppContext.BaseDirectory during build via the .csproj Content include.
    static readonly string DataRoot = Path.Combine(AppContext.BaseDirectory, "fixtures", "fs25", "data-1.23.1.0");

    public static void Run(Action<bool, string> check)
    {
        // ---- Period and Season ----

        check(Period.Parse("EARLY_SPRING").Number == 1
            && Period.Parse("MID_WINTER").Number == 11,
            "FS25 period names map to numbers 1..12 starting at March");
        check(new Period(1).RussianMonth == "март" && new Period(11).RussianMonth == "январь",
            "Period 1 is март, period 11 is январь");
        check(new Period(1).Season == Season.Spring
            && new Period(6).Season == Season.Summer
            && new Period(9).Season == Season.Autumn
            && new Period(11).Season == Season.Winter,
            "Season groups map period ranges to spring/summer/autumn/winter");
        check(new Period(1).CalendarMonth == 3 && new Period(11).CalendarMonth == 1 && new Period(12).CalendarMonth == 2,
            "CalendarMonth maps period → 1..12 with 1 = January (period 11)");
        check(new Period(12).Next.Number == 1 && new Period(3).Offset(2).Number == 5
            && new Period(2).Offset(-3).Number == 11,
            "Period arithmetic wraps forward and backward across the year boundary");
        check(new Period(1).StepsUntil(new Period(1)) == 0
            && new Period(12).StepsUntil(new Period(1)) == 1
            && new Period(3).StepsUntil(new Period(1)) == 10,
            "StepsUntil counts periods forward around the year");
        check(Period.All.Count() == 12 && Period.All.Select(p => p.Number).SequenceEqual(Enumerable.Range(1, 12)),
            "Period.All lists all twelve periods in order");
        check(!Period.TryParse("bogus", out _) && !Period.TryParse(null, out _),
            "TryParse rejects unknown and null period names");
        try { _ = new Period(0); check(false, "Period 0 rejected"); }
        catch (ArgumentOutOfRangeException) { check(true, "Period 0 rejected"); }
        try { _ = new Period(13); check(false, "Period 13 rejected"); }
        catch (ArgumentOutOfRangeException) { check(true, "Period 13 rejected"); }
        check(SeasonNames.InRussian(Season.Autumn) == "осень"
            && SeasonNames.TryParse("winter", out var winter) && winter == Season.Winter,
            "Season Russian names and case-insensitive parsing");

        // ---- Linear RGB → sRGB ----

        var mid = new LinearRgba(0.5, 0.5, 0.5, 1).ToSrgb();
        check(Math.Abs(mid.R - 0.7353569) < 0.001 && mid.R == mid.G && mid.G == mid.B,
            "LinearRgba(0.5,0.5,0.5) → sRGB ≈ 0.735 across all channels");
        var (r0, g0, b0) = new LinearRgba(0, 0, 0, 1).ToSrgb();
        var (r1, g1, b1) = new LinearRgba(1, 1, 1, 1).ToSrgb();
        check(r0 == 0 && g0 == 0 && b0 == 0 && Math.Abs(r1 - 1) < 1e-9 && Math.Abs(g1 - 1) < 1e-9 && Math.Abs(b1 - 1) < 1e-9,
            "Linear → sRGB fixes the black and white endpoints");

        // The licensed game-data fixtures are intentionally not distributed with SimDeck.
        // Run the catalog integration checks only on a developer checkout that has them.
        if (!File.Exists(Path.Combine(DataRoot, "VERSION"))) return;

        // ---- Fixture wiring ----

        check(File.Exists(Path.Combine(DataRoot, "VERSION")), "Catalog fixtures ship the VERSION file");
        check(File.ReadAllText(Path.Combine(DataRoot, "VERSION")).Trim() == "1.23.1.0",
            "Catalog fixture is pinned to FS25 1.23.1.0");

        // ---- Fs25CropParser ----

        var wheat = LoadCrop("foliage/wheat/wheat.xml");
        check(wheat.Name == "WHEAT" && wheat.RawName == "wheat",
            "Crop Name is uppercase-normalised, RawName preserves XML casing");
        check(wheat.NeedsRolling && wheat.GrowthRequiresLime,
            "Wheat needs rolling and lime (1.23.1.0)");
        check(Math.Abs(wheat.HarvestLitersPerSqm - 0.89) < 1e-6
            && Math.Abs(wheat.SeedingLitersPerSqm!.Value - 0.0308) < 1e-6,
            "Wheat yield 0.89 L/m² and seed rate 0.0308 L/m² match 1.23.1.0");
        check(wheat.FoliageStates.Count == 10
            && wheat.FoliageStates[0] == "invisible"
            && wheat.FoliageStates[^1] == "harvested",
            "Wheat has 10 foliage states, invisible → harvested");
        check(wheat.PlantingPeriods.SequenceEqual(new[] { new Period(7), new Period(8) }),
            "Wheat is planted EARLY_AUTUMN + MID_AUTUMN — winter grain, not spring");
        check(wheat.StageAt(1) == "invisible" && wheat.StageAt(10) == "harvested"
            && wheat.StageAt(0) is null && wheat.StageAt(11) is null,
            "growthState is 1-based; StageAt returns null out of range");

        var canola = LoadCrop("foliage/canola/canola.xml");
        check(canola.FoliageStates.Count == 11,
            "Canola has 11 foliage states — different length from wheat, so growthState indexes per-crop");
        check(canola.PlantingPeriods.SequenceEqual(new[] { new Period(6), new Period(7) }),
            "Canola is planted LATE_SUMMER + EARLY_AUTUMN — not the same window as wheat");
        check(canola.StageAt(9) == "harvestReady",
            "Canola stage index 9 is harvestReady (per-crop resolution matters)");

        var sugar = LoadCrop("foliage/sugarbeet/sugarbeet.xml");
        check(sugar.Name == "SUGARBEET" && sugar.RawName == "sugarBeet",
            "sugarbeet folder ↔ sugarBeet fruitType name mismatch is preserved in the record");
        check(sugar.GrowthRequiresLime, "Sugar beet requires lime");
        check(sugar.PlantingPeriods.SequenceEqual(new[] { new Period(1), new Period(2) }),
            "Sugar beet planted EARLY_SPRING + MID_SPRING (spring crop)");

        var potato = LoadCrop("foliage/potato/potato.xml");
        check(!potato.NeedsRolling, "Potato does NOT need rolling — uses ridges instead");
        check(potato.PlantingPeriods.Contains(new Period(1)), "Potato is planted from EARLY_SPRING");
        check(Math.Abs(potato.SeedingLitersPerSqm!.Value - 0.3733) < 1e-4,
            "Potato uses 0.3733 L/m² of seed potatoes (very high vs grains)");

        // Different crops resolve the same growthState to different stage names.
        check(wheat.StageAt(4) != canola.StageAt(4) || wheat.FoliageStates[3] != canola.FoliageStates[3],
            "growthState 4 does NOT decode to the same stage across crops (per-crop catalog is required)");

        // ---- Fs25FillTypesParser ----

        var fills = LoadFills("maps/maps_fillTypes.xml");
        var wFill = fills.Get("WHEAT")!;
        check(Math.Abs(wFill.PricePerLiter - 0.337) < 1e-6,
            "WHEAT base price €0.337/L in 1.23.1.0");
        check(wFill.MonthlyFactors is { Count: 12 },
            "WHEAT has all 12 monthly price factors");
        check(Math.Abs(wFill.PriceAt(new Period(1)) - 0.337 * 1.00) < 1e-6
            && Math.Abs(wFill.PriceAt(new Period(11)) - 0.337 * 1.21) < 1e-6,
            "Wheat priced by period: March = base, January = base × 1.21");
        check(wFill.PriceAt(new Period(11)) > wFill.PriceAt(new Period(6)),
            "Wheat peaks in January over the August trough (grain market)");
        check(Math.Abs(wFill.MassPerLiter!.Value - 0.78) < 1e-6,
            "WHEAT mass 0.78 kg/L (used for €/tonne conversion)");
        check(fills.Get("wheat") is not null && fills.Get("Wheat") is not null,
            "Fill-type lookup is case-insensitive");
        check(fills.Get("does_not_exist") is null && fills.PriceAt("nowhere", new Period(1)) is null,
            "Unknown fill types return null instead of a fabricated price");
        var silage = fills.Get("SILAGE")!;
        check(silage.MonthlyFactors is null
            && Math.Abs(silage.PriceAt(new Period(3)) - 0.121) < 1e-6
            && Math.Abs(silage.PriceAt(new Period(9)) - 0.121) < 1e-6,
            "Fill types without <factors> return the base price for every period");
        var beetCut = fills.Get("SUGARBEET_CUT")!;
        check(Math.Abs(beetCut.PriceAt(new Period(10)) - 0.222 * 1.15) < 1e-6,
            "SUGARBEET_CUT peaks in December (period 10, factor 1.15)");

        // ---- Fs25FruitTypesParser ----

        var fruits = LoadFruits("maps/maps_fruitTypes.xml");
        check(fruits.FoliageFiles.Contains("foliage/wheat/wheat.xml")
            && fruits.FoliageFiles.Contains("foliage/canola/canola.xml"),
            "Fruit-types index references crop foliage files with $data stripped");
        check(fruits.CategoriesFor("WHEAT").Contains("SOWINGMACHINE")
            && fruits.CategoriesFor("WHEAT").Contains("GRAINHEADER"),
            "Wheat belongs to SOWINGMACHINE and GRAINHEADER categories");
        check(fruits.CategoriesFor("wheat").Count == fruits.CategoriesFor("WHEAT").Count,
            "CategoriesFor is case-insensitive on the crop name");
        check(fruits.CategoriesFor("SUGARBEET").SequenceEqual(new[] { "PLANTER" }),
            "Sugar beet's only category is PLANTER (no grain header)");
        check(fruits.CategoriesFor("POTATO").Count == 0,
            "Potato is not covered by the fixture's sowing categories (real game: only planter/harvester categories)");
        check(fruits.CropsIn("MOWER").Contains("GRASS")
            && fruits.CropsIn("MOWER").Contains("WHEAT"),
            "MOWER category lists grass and grains");
        check(fruits.CropsIn("nowhere").Count == 0,
            "Unknown category returns empty list, not exception");

        // ---- Fs25MachineCatalog ----

        var machines = Fs25MachineCatalog.LoadFrom(Path.Combine(DataRoot, "vehicles"));
        check(machines.Machines.Count == 3,
            $"Machine catalog loads 3 fixture vehicles (found {machines.Machines.Count}) and skips the sounds/ sub-XML");

        var waterTrailer = machines.Machines.Single(m => m.Name == "1600");
        check(waterTrailer.Type == "waterTrailer" && waterTrailer.Brand == "ABI"
            && waterTrailer.Category == "barrels" && waterTrailer.Price == 17000,
            "ABI 1600 water trailer parsed: type/brand/category/price");
        check(waterTrailer.RelativePath == "abi/waterTrailer1600/waterTrailer1600.xml",
            "Machine relative path uses forward slashes");
        check(waterTrailer.FullName == "ABI 1600", "FullName combines brand and model");
        check(waterTrailer.Functions.SequenceEqual(new[] { "$l10n_function_tank" }),
            "Functions list preserves the localisation tokens as-is");

        var spreader = machines.Machines.Single(m => m.Name == "ZA-TS 3200");
        check(spreader.Category == "fertilizerSpreaders" && spreader.Price == 25000
            && spreader.Type == "spreader" && spreader.Brand == "AMAZONE",
            "AMAZONE ZA-TS 3200 fertilizer spreader parsed correctly");
        check(spreader.ImagePath == "vehicles/amazone/zats3200/store_zats3200.png",
            "Image path has $data/ stripped");

        var cultivator = machines.Machines.Single(m => m.Name == "Cenio 4000 Super");
        check(cultivator.Category == "cultivators" && cultivator.Price == 24500
            && cultivator.Type == "cultivator",
            "AMAZONE Cenio 4000 Super cultivator parsed correctly");

        check(machines.ByCategory("cultivators").Count == 1,
            "ByCategory filters case-insensitively");
        check(machines.ByCategory("CULTIVATORS").Count == 1,
            "ByCategory is case-insensitive on input");
        check(machines.ByBrand("AMAZONE").Count == 2, "ByBrand matches every model");
        check(machines.ByType("spreader").Count == 1, "ByType filters by vehicle type");
        check(machines.Categories.SequenceEqual(new[] { "barrels", "cultivators", "fertilizerSpreaders" }),
            "Categories list is sorted, distinct, and case-preserving from the XML");
        check(Fs25MachineCatalog.LoadFrom(Path.Combine(DataRoot, "vehicles", "nowhere")).Machines.Count == 0,
            "LoadFrom returns empty catalog when the folder does not exist");

        // ---- Fs25GamePaths (optional integration test) ----

        var install = Fs25GamePaths.FindInstall();
        if (install is not null)
        {
            check(install.LooksValid && install.Version == "1.23.1.0",
                $"Real FS25 install detected at {install.Root} ({install.Version})");
            using (var real = File.OpenRead(install.FruitTypesXml))
            {
                var realCatalog = Fs25FruitTypesParser.Parse(real);
                check(realCatalog.FoliageFiles.Count >= 20,
                    $"Real 1.23.1.0 fruitTypes lists ≥ 20 crops (actual: {realCatalog.FoliageFiles.Count})");
            }
            var realMachines = Fs25MachineCatalog.LoadFrom(install.VehiclesDir);
            check(realMachines.Machines.Count >= 300,
                $"Real 1.23.1.0 vehicles folder contains ≥ 300 machines with storeData (actual: {realMachines.Machines.Count})");
            // FS25 stores combine harvesters under the "harvesters" store category (I initially
            // guessed "combines" — game reality won that fight, hence this fixed assertion).
            check(realMachines.ByCategory("harvesters").Count >= 10,
                $"Real 1.23.1.0 has ≥ 10 harvesters/combines (actual: {realMachines.ByCategory("harvesters").Count})");
            check(realMachines.ByCategory("tractorsL").Count + realMachines.ByCategory("tractorsM").Count
                + realMachines.ByCategory("tractorsS").Count >= 30,
                $"Real 1.23.1.0 has ≥ 30 tractors across S/M/L classes");
        }
        else
        {
            // Skipped rather than failed when FS25 is not installed locally.
            check(true, "Optional real-install integration test skipped (game not installed here)");
        }
    }

    static Fs25Crop LoadCrop(string relative)
    {
        using var s = File.OpenRead(Path.Combine(DataRoot, relative));
        return Fs25CropParser.Parse(s);
    }

    static Fs25FillTypeCatalog LoadFills(string relative)
    {
        using var s = File.OpenRead(Path.Combine(DataRoot, relative));
        return Fs25FillTypesParser.Parse(s);
    }

    static Fs25FruitTypeCatalog LoadFruits(string relative)
    {
        using var s = File.OpenRead(Path.Combine(DataRoot, relative));
        return Fs25FruitTypesParser.Parse(s);
    }
}
