namespace SimDeck.Core;

/// <summary>Location of an installed Farming Simulator 25.</summary>
public sealed record Fs25GameInstall(string Root)
{
    public string DataDir => Path.Combine(Root, "data");
    public string MapsDir => Path.Combine(DataDir, "maps");
    public string FoliageDir => Path.Combine(DataDir, "foliage");
    public string VehiclesDir => Path.Combine(DataDir, "vehicles");
    public string StoreDir => Path.Combine(DataDir, "store");
    public string FruitTypesXml => Path.Combine(MapsDir, "maps_fruitTypes.xml");
    public string FillTypesXml => Path.Combine(MapsDir, "maps_fillTypes.xml");

    /// <summary>Contents of the VERSION file, e.g. "1.23.1.0". Used as the catalog cache key.</summary>
    public string Version
    {
        get
        {
            var file = Path.Combine(Root, "VERSION");
            return File.Exists(file) ? File.ReadAllText(file).Trim() : "unknown";
        }
    }

    public bool LooksValid => Directory.Exists(FoliageDir) && File.Exists(FruitTypesXml);
}

public sealed record Fs25SavegameDir(string Path)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string File(string fileName) => System.IO.Path.Combine(Path, fileName);
    public bool LooksValid => System.IO.File.Exists(File("careerSavegame.xml"));

    /// <summary>Newest write time across the six XML files used by the reader.</summary>
    public DateTime LastSaved => new[] { "careerSavegame.xml", "environment.xml", "farms.xml", "fields.xml", "placeables.xml", "vehicles.xml" }
        .Select(file => System.IO.File.GetLastWriteTimeUtc(File(file)))
        .Max();
}

/// <summary>
/// Locates the FS25 user data folder and the game installation.
/// </summary>
/// <remarks>
/// The user data folder must never be built by concatenating "Documents" literally:
/// Documents may be redirected into OneDrive or localised. Use the shell folder
/// API instead of constructing a path from a username.
/// </remarks>
public static class Fs25GamePaths
{
    /// <summary>Environment variable that overrides install probing for tests and CI.</summary>
    public const string InstallEnvVar = "SIMDECK_FS25_INSTALL";
    public const string UserDataEnvVar = "SIMDECK_FS25_USERDATA";

    private static readonly string[] InstallProbes =
    [
        @"C:\Program Files (x86)\Steam\steamapps\common\Farming Simulator 25",
        @"C:\Program Files\Farming Simulator 2025",
        @"C:\Program Files (x86)\Farming Simulator 2025",
    ];

    public static string? FindUserDataDir()
    {
        var overridePath = Environment.GetEnvironmentVariable(UserDataEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath) && Directory.Exists(overridePath)) return overridePath;
        var candidates = new List<string>();

        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(documents))
            candidates.Add(Path.Combine(documents, "My Games", "FarmingSimulator2025"));

        // OneDrive redirection sometimes leaves the shell folder pointing at the local copy
        // while the game writes to the synced one, so probe both explicitly.
        var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrEmpty(oneDrive))
        {
            candidates.Add(Path.Combine(oneDrive, "Documents", "My Games", "FarmingSimulator2025"));
            candidates.Add(Path.Combine(oneDrive, "Документы", "My Games", "FarmingSimulator2025"));
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
        {
            candidates.Add(Path.Combine(profile, "Documents", "My Games", "FarmingSimulator2025"));
            candidates.Add(Path.Combine(profile, "Документы", "My Games", "FarmingSimulator2025"));
        }

        return candidates.FirstOrDefault(Directory.Exists);
    }

    /// <summary>Savegame folders that contain a career, newest first.</summary>
    public static IReadOnlyList<Fs25SavegameDir> FindSavegames(string? userDataDir = null)
    {
        userDataDir ??= FindUserDataDir();
        if (userDataDir is null || !Directory.Exists(userDataDir))
            return [];

        return Directory.EnumerateDirectories(userDataDir, "savegame*")
            .Select(d => new Fs25SavegameDir(d))
            .Where(s => s.LooksValid)
            .OrderByDescending(s => s.LastSaved)
            .ToList();
    }

    public static string? FindModsDir(string? userDataDir = null)
    {
        userDataDir ??= FindUserDataDir();
        if (userDataDir is null) return null;
        var mods = Path.Combine(userDataDir, "mods");
        return Directory.Exists(mods) ? mods : null;
    }

    public static Fs25GameInstall? FindInstall(string? hint = null)
    {
        var envOverride = Environment.GetEnvironmentVariable(InstallEnvVar);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            var fromEnv = new Fs25GameInstall(envOverride);
            if (fromEnv.LooksValid) return fromEnv;
        }

        if (!string.IsNullOrWhiteSpace(hint))
        {
            var fromHint = new Fs25GameInstall(hint);
            if (fromHint.LooksValid) return fromHint;
        }

        return InstallProbes
            .Select(p => new Fs25GameInstall(p))
            .FirstOrDefault(i => i.LooksValid);
    }
}
