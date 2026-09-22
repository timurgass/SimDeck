using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using SimDeck.App;
using SimDeck.Core;

static class ProfileTests
{
    public static async Task Run(Action<bool, string> check, string directory)
    {
        var full = GameProfiles.F1();
        GameProfiles.Validate(full);
        var full25 = GameProfiles.F125();
        GameProfiles.Validate(full25);
        var additional = AdditionalProfiles.All();
        foreach (var profile in additional) GameProfiles.Validate(profile);
        check(full.Actions.Count == 69 && full.Actions.Select(a => a.Page).Distinct().Count() == 3 && full.Actions.All(a => !a.Key.Contains("NumPad")), "F1 75-percent catalog validates all 69 actions across three sections");
        check(full25.Actions.SequenceEqual(full.Actions) && full25.TargetProcess == "F1_25", "F1 25 reuses the verified bindings with its own process target");
        check(additional.Select(p => p.Id).SequenceEqual(new[] { "acc", "ams2", "ets2", "snowrunner" }) && additional.All(p => p.Actions.Count >= 25 && p.Actions.Select(a => a.Page).Distinct().Count() >= 3), "ACC, AMS2, ETS2 and SnowRunner ship complete multi-page button-box profiles");
        check(additional.Select(p => p.TargetProcess).SequenceEqual(new[] { "AC2-Win64-Shipping", "AMS2AVX", "eurotrucks2", "SnowRunner" }), "Additional profiles target the actual Windows game processes");
        check(full.Actions.All(a => a.Id is not ("drs" or "ers") && a.Group != "Вождение"), "Removed driving and overtake buttons stay absent");
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "f1-preset.json")));
        var expectedActions = JsonSerializer.Deserialize<List<DeckAction>>(fixture.RootElement.GetProperty("controls"), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        check(full.Actions.SequenceEqual(expectedActions), "Windows profile matches shared Android protocol fixture");
        var migrationPath = Path.Combine(directory, "f1-75-migration"); Directory.CreateDirectory(migrationPath);
        var legacyF1 = new GameProfile("f1-24", "F1 24", "F1_24", [new("mfd", "Основное", "MFD", "", "NumPad0"), new("drs", "Основное", "DRS", "", "F"), new("custom-user", "Свои", "Своя", "", "F12")], 8);
        var originalSettings = new Settings { ActiveProfileId = "f1-24", Profiles = [legacyF1, new("beamng-default", "BeamNG", "BeamNG.drive.x64", [new("custom-beam", "Свои", "Своя", "", "F10")])], Devices = [new("tablet", new string('a', 64))] };
        File.WriteAllText(Path.Combine(migrationPath, "settings.json"), JsonSerializer.Serialize(originalSettings));
        var migrated = new SettingsStore(migrationPath);
        check(migrated.Value.ActiveProfile.Actions.Count == 70 && migrated.Value.ActiveProfile.Revision == 9 && migrated.Value.ActiveProfile.Actions.Single(a => a.Id == "mfd").Key == "B", "Existing F1 profile migrates to installed keyboard preset and retains custom action");
        check(migrated.Value.Profiles.Single(p => p.Id == "beamng-default").Actions.Single().Key == "F10" && migrated.Value.Devices.SequenceEqual(originalSettings.Devices), "F1 migration preserves BeamNG and pairing");
        check(Directory.GetFiles(migrationPath, "settings.before-f1-75-*.json").Length == 1, "Original settings backed up before F1 migration");
        var edited = migrated.Value.ActiveProfile with { Actions = migrated.Value.ActiveProfile.Actions.Where(a => a.Id != "radio").ToList() };
        migrated.Value.Profiles[0] = edited; migrated.Save();
        var twice = new SettingsStore(migrationPath);
        check(twice.Value.ActiveProfile.Actions.All(a => a.Id != "radio") && twice.Value.ActiveProfile.Revision == 9, "F1 migration is one-time and does not undo later edits");
        check(new ButtonRow(full.Actions.First()).ToAction() == full.Actions.First(), "Companion editor preserves subgroup metadata");
        long now = 1000;
        var parser = new F1TelemetryParser(() => now);
        check(parser.TryParse(Packet(6), out var t) && t!.Gear == 7 && t.SpeedMps == 75 && t.Rpm == 11500 && t.FuelFraction is null && t.ActionStates!["drs"], "F1 selects player car and converts km/h without waiting for status");
        check(parser.TryParse(Packet(7), out t) && t is null, "F1 status never refreshes old speed");
        parser.TryParse(Packet(6, 2), out t);
        check(t!.FuelFraction == .5 && t.MaxRpm == 15000 && t.MaxGear == 8 && t.ActionStates!["ers"] && t.ActionStates["pitLimiter"], "F1 merges fresh fuel, RPM limit, ERS and pit limiter");
        parser.TryParse(Packet(6, 1), out t);
        check(t is null, "F1 ignores out-of-order telemetry");
        now += 501; parser.TryParse(Packet(6, 3), out t);
        check(t!.FuelFraction is null && t.MaxRpm is null && !t.ActionStates!.ContainsKey("ers"), "F1 expires auxiliary data independently");
        check(!parser.TryParse(Packet(6)[..1351], out _), "F1 rejects truncated packet");
        var invalid = Packet(6, 4); invalid[27] = 22;
        check(!parser.TryParse(invalid, out _), "F1 rejects invalid player index");
        invalid = Packet(6, 4); BinaryPrimitives.WriteSingleLittleEndian(invalid.AsSpan(29 + 3 * 60 + 2), float.NaN);
        check(!parser.TryParse(invalid, out _), "F1 rejects NaN pedals");
        invalid = Packet(6, 4); BinaryPrimitives.WriteUInt16LittleEndian(invalid, 2025); invalid[2] = 25;
        check(new F1TelemetryParser().TryParse(invalid, out var f125) && f125?.Gear == 7, "F1 25 native telemetry layout is accepted");
        parser.TryParse(Packet(7, 4), out _); parser.TryParse(Packet(6, 1, 99), out t);
        check(t is not null && t.FuelFraction is null, "F1 new session clears prior car status");
        parser.TryParse(Packet(6, 5), out t);
        check(t is null, "F1 delayed old session cannot replace current session");
        invalid = Packet(6, 2, 99); invalid[29 + 3 * 60 + 15] = 255;
        parser.TryParse(invalid, out t);
        check(t!.GearDisplay == "R", "F1 signed reverse gear");

        var path = Path.Combine(directory, "profile-migration"); Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "settings.json"), """{"Keys":{"lights":"F10","horn":"H","ignition":"V","reset":"R"},"Devices":[],"TargetProcess":"BeamNG.drive.x64"}""");
        await using var host = new CompanionHost(path);
        var token = PairingGate.NewToken(); host.Store.Trust("test", token);
        check(host.Profile.Actions.Single(a => a.Id == "lights").Key == "F10" && host.Store.Value.Profiles.Count == 7, "Profile migration preserves existing user keys and adds all installed game profiles");
        check(host.Store.Value.Profiles.Select(p => p.Id).ToHashSet().SetEquals(GameProfiles.KnownIds), "Profile catalog migration adds ACC, AMS2, ETS2 and SnowRunner exactly once");
        var custom = new DeckAction("custom-test", "Мои кнопки", "CUSTOM", "Test", "Ctrl+F12", "hold");
        var oldRevision = host.Profile.Revision;
        host.SaveProfile(host.Profile with { Actions = [.. host.Profile.Actions.Where(a => a.Id != "camera"), custom] });
        check(host.Profile.Revision == oldRevision + 1 && host.Store.IsTrusted(token), "Profile save increments revision without revoking pairing");
        host.SelectProfile("f1-24");
        check(host.Backend.TargetProcess == "F1_24" && !host.Backend.Enabled && host.Telemetry.Read().Data is null, "Game switch changes process, disables input and clears telemetry");
        host.SetCompatibleInput(true);
        check(host.Backend.UseVirtualKey && host.Store.Value.UseVirtualKeyInput, "Compatible Virtual-Key input mode is applied and saved");
        host.SelectProfile("beamng-default");
        check(host.Profile.Actions.Contains(custom) && host.Profile.Actions.All(a => a.Id != "camera"), "Custom button and deletion survive profile switch");
        var reloaded = new SettingsStore(path);
        check(reloaded.Value.ActiveProfile.Actions.Contains(custom) && reloaded.IsTrusted(token) && reloaded.Value.ActiveProfile.Actions.All(a => a.Id != "camera") && reloaded.Value.UseVirtualKeyInput, "Custom edits, pairing and input compatibility mode survive restart");
        check(reloaded.Value.Profiles.Count == 7 && reloaded.Value.ProfileCatalogVersion == AdditionalProfiles.CatalogVersion, "Additional profile migration is idempotent across restart");
        try { GameProfiles.Validate(host.Profile with { Actions = [custom, custom] }); check(false, "Duplicate actions rejected"); } catch (ArgumentException) { check(true, "Duplicate actions rejected"); }
        try { GameProfiles.Validate(host.Profile with { Actions = [custom with { Id = "ignition", Gesture = "press" }] }); check(false, "Ignition safety preserved"); } catch (ArgumentException) { check(true, "Ignition safety preserved"); }
    }
    public static byte[] Packet(byte id, uint frame = 1, ulong session = 42)
    {
        var b = new byte[id == 6 ? 1352 : 1239];
        BinaryPrimitives.WriteUInt16LittleEndian(b, 2024); b[2] = 24; b[5] = 1; b[6] = id;
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(7), session);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(23), frame); b[27] = 3; b[28] = 255;
        var c = b.AsSpan(29 + 3 * (id == 6 ? 60 : 55));
        if (id == 6)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(c, 270); BinaryPrimitives.WriteSingleLittleEndian(c[2..], .75f);
            c[15] = 7; BinaryPrimitives.WriteUInt16LittleEndian(c[16..], 11500); c[18] = 1;
        }
        else
        {
            c[4] = 1; BinaryPrimitives.WriteSingleLittleEndian(c[5..], 50); BinaryPrimitives.WriteSingleLittleEndian(c[9..], 100);
            BinaryPrimitives.WriteUInt16LittleEndian(c[17..], 15000); c[21] = 8; c[41] = 3;
        }
        return b;
    }
}
