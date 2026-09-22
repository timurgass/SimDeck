using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace SimDeck.App;

public sealed record TrustedDevice(string Name, string TokenHash);
public sealed class Settings
{
    public string TargetProcess { get; set; } = "BeamNG.drive.x64";
    public int Port { get; set; } = 9443;
    public int UdpPort { get; set; } = 4444;
    public string CertificateThumbprint { get; set; } = "";
    public bool UseVirtualKeyInput { get; set; }
    public Dictionary<string, string> Keys { get; set; } = BeamNgProfile.Actions.ToDictionary(x => x.Id, x => x.Key);
    public List<TrustedDevice> Devices { get; set; } = [];
    public string ActiveProfileId { get; set; } = "beamng-default";
    public List<GameProfile> Profiles { get; set; } = [];
    public int F1PresetVersion { get; set; }
    public int ProfileCatalogVersion { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public GameProfile ActiveProfile => Profiles.Single(p => p.Id == ActiveProfileId);
}

public sealed class SettingsStore
{
    readonly string path;
    readonly object gate = new();
    public Settings Value { get; }
    public SettingsStore(string directory)
    {
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, "settings.json");
        Value = File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new() : new();
        if (Value.Port is < 1024 or > 65535 || Value.UdpPort is < 1024 or > 65535) throw new InvalidDataException("Недопустимый порт в settings.json");
        if (Value.Profiles.Count == 0)
        {
            BeamNgProfile.AddMissingKeys(Value.Keys);
            Value.Profiles.Add(new("beamng-default", "BeamNG.drive", Value.TargetProcess,
                BeamNgProfile.Actions.Select(a => a with { Key = Value.Keys[a.Id] }).ToList()));
            Value.Profiles.Add(GameProfiles.F1());
            Value.Profiles.Add(GameProfiles.F125());
            Value.F1PresetVersion = GameProfiles.F1PresetVersion;
            Save();
        }
        if (Value.F1PresetVersion < 1)
        {
            var index = Value.Profiles.FindIndex(p => p.Id == "f1-24");
            if (index >= 0)
            {
                var upgraded = GameProfiles.UpgradeF1(Value.Profiles[index]);
                GameProfiles.Validate(upgraded);
                if (File.Exists(path)) File.Copy(path, Path.Combine(directory, "settings.before-f1-75-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json"));
                Value.Profiles[index] = upgraded;
            }
            Value.F1PresetVersion = 1;
            Save();
        }
        if (Value.F1PresetVersion < 2)
        {
            if (Value.Profiles.All(p => p.Id != "f1-25")) Value.Profiles.Add(GameProfiles.F125());
            Value.F1PresetVersion = 2;
            Save();
        }
        if (Value.ProfileCatalogVersion < AdditionalProfiles.CatalogVersion)
        {
            foreach (var profile in AdditionalProfiles.All())
                if (Value.Profiles.All(existing => existing.Id != profile.Id)) Value.Profiles.Add(profile);
            Value.ProfileCatalogVersion = AdditionalProfiles.CatalogVersion;
            Save();
        }
        foreach (var p in Value.Profiles) GameProfiles.Validate(p);
        _ = Value.ActiveProfile;
    }
    public void Save()
    {
        lock (gate)
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
    }
    public bool IsTrusted(string token)
    {
        if (token.Length != 64) return false;
        var hash = Core.PairingGate.Hash(token);
        lock (gate) return Value.Devices.Any(x => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(x.TokenHash), Convert.FromHexString(hash)));
    }
    public void Trust(string name, string token)
    {
        lock (gate) { Value.Devices.Add(new(name, Core.PairingGate.Hash(token))); Save(); }
    }
    public void RevokeAll() { lock (gate) { Value.Devices.Clear(); Save(); } }
    public X509Certificate2 Certificate()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        if (Value.CertificateThumbprint.Length > 0)
        {
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, Value.CertificateThumbprint, false);
            foreach (var c in found) if (c.HasPrivateKey && c.NotAfter > DateTime.UtcNow.AddDays(1)) return c;
        }
        using var rsa = RSA.Create(3072);
        var req = new CertificateRequest("CN=SimDeck Companion", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        using var issued = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
        var cert = X509CertificateLoader.LoadPkcs12(issued.Export(X509ContentType.Pfx), null,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
        store.Add(cert);
        Value.CertificateThumbprint = cert.Thumbprint;
        Save();
        return cert;
    }
}
