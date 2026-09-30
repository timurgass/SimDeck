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
        check(additional.Select(p => p.Id).SequenceEqual(new[] { "acc", "ams2", "ets2", "snowrunner", "fs25" }) && additional.All(p => p.Actions.Count >= 25 && p.Actions.Select(a => a.Page).Distinct().Count() >= 3), "ACC, AMS2, ETS2, SnowRunner and FS25 ship complete multi-page button-box profiles");
        check(additional.Select(p => p.TargetProcess).SequenceEqual(new[] { "AC2-Win64-Shipping", "AMS2AVX", "eurotrucks2", "SnowRunner", "FarmingSimulator2025Game" }), "Additional profiles target the actual Windows game processes");
        var acc = AdditionalProfiles.Acc();
        check(acc.Actions.Single(a => a.Id == "accIgnition").Key == "I" && acc.Actions.Single(a => a.Id == "accIgnitionOff").Key == "F2", "ACC exposes ignition on and a supported MFD-based ignition off control");

        // FS25 reads the player's own key map, so the token translation carries the profile.
        static string? Fs25Key(string token) => Fs25Bindings.TryTranslate(token, out var k) ? k : null;
        check(Fs25Key("KEY_v") == "V" && Fs25Key("KEY_3") == "D3" && Fs25Key("KEY_f1") == "F1"
            && Fs25Key("KEY_comma") == "OemComma" && Fs25Key("KEY_esc") == "Escape",
            "FS25 plain keys, digits and punctuation map to the names ParseKey accepts");
        check(Fs25Key("KEY_KP_1") == "NumPad1" && Fs25Key("KEY_KP_plus") == "Add" && Fs25Key("KEY_KP_minus") == "Subtract",
            "FS25 numpad bindings survive, unlike the numpad-free F1 preset");
        check(Fs25Key("KEY_lctrl KEY_b") == "Ctrl+B" && Fs25Key("KEY_lshift KEY_f") == "Shift+F"
            && Fs25Key("KEY_lshift KEY_lctrl KEY_k") == "Shift+Ctrl+K" && Fs25Key("KEY_rctrl KEY_q") == "Ctrl+Q",
            "FS25 modifier combinations become the Ctrl/Alt/Shift syntax, right-hand keys folding onto left");
        check(Fs25Key("KEY_lshift") is null && Fs25Key("MOUSE_BUTTON_LEFT") is null
            && Fs25Key("MOUSE_BUTTON_MIDDLE AXIS_X-") is null && Fs25Key("KEY_a KEY_b") is null && Fs25Key("") is null,
            "FS25 rejects what a deck cannot press: bare modifiers, mouse buttons, axes and two-key bindings");
        foreach (var key in Fs25Profile.Default().Actions.Select(a => a.Key))
            WindowsInput.ParseBinding("probe", key, "press");
        var fs25 = Fs25Profile.Default();
        check(fs25.Actions.Count == 53 && fs25.Actions.Select(a => a.Page).Distinct().Count() == 5
            && fs25.Actions.All(a => a.Id is not ("ignition" or "fs25Detach")) && fs25.Actions.Single(a => a.Id == "fs25Attach").Label == "ПРИЦЕПИТЬ / ОТЦЕПИТЬ"
            && fs25.TargetProcess == "FarmingSimulator2025Game",
            "FS25 ships one contextual hitch button and 53 parseable actions over five pages");
        var reboundSample = Fs25Bindings.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            """<inputBinding><actionBinding action="LOWER_IMPLEMENT"><binding device="KB_MOUSE_DEFAULT" input="KEY_k"/></actionBinding></inputBinding>""")));
        check(Fs25Profile.Build(reboundSample).Actions.Single(a => a.Id == "fs25Lower").Key == "K"
            && Fs25Profile.Build(reboundSample).Actions.Single(a => a.Id == "fs25Attach").Key == "Q",
            "FS25 profile follows a rebound key and keeps factory defaults for the rest");
        var fs25Custom = new GameProfile("fs25", "Farming Simulator 25", "FarmingSimulator2025Game",
            [new("fs25Lower", "Орудие", "ОПУСТИТЬ", "", "F11"), new("fs25Attach", "Орудие", "ПРИЦЕПИТЬ", "", "F10"), new("fs25Detach", "Орудие", "ОТЦЕПИТЬ", "", "Ctrl+Q"), new("fs25-mine", "Свои", "CUSTOM", "", "F12")], 1);
        var fs25Upgraded = Fs25Profile.Upgrade(fs25Custom, Fs25Bindings.Empty);
        check(fs25Upgraded.Actions.Any(a => a.Id == "fs25-mine") && fs25Upgraded.Actions.Count == 54
            && fs25Upgraded.Actions.All(a => a.Id != "fs25Detach")
            && fs25Upgraded.Actions.Single(a => a.Id == "fs25Lower").Key == "F11"
            && fs25Upgraded.Actions.Single(a => a.Id == "fs25Attach").Key == "F10"
            && fs25Upgraded.Revision == 2, "FS25 upgrade removes redundant detach and keeps custom actions and keys");
        var imported = Fs25Profile.ApplyPlayerBindings(fs25Custom, reboundSample);
        check(imported.Actions.Single(a => a.Id == "fs25Lower").Key == "K"
            && imported.Actions.Single(a => a.Id == "fs25-mine").Key == "F12"
            && imported.Actions.Count == fs25Custom.Actions.Count,
            "FS25 import changes mapped keys without deleting personal buttons");
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
        var accMigrationPath = Path.Combine(directory, "acc-preset-migration"); Directory.CreateDirectory(accMigrationPath);
        var oldAcc = new GameProfile("acc", "Assetto Corsa Competizione", "AC2-Win64-Shipping", [new("accIgnition", "Гонка", "ЗАЖИГАНИЕ", "", "Z"), new("acc-custom", "Свои", "CUSTOM", "", "F12")], 4);
        File.WriteAllText(Path.Combine(accMigrationPath, "settings.json"), JsonSerializer.Serialize(new Settings { ActiveProfileId = "acc", Profiles = [oldAcc], F1PresetVersion = 2, ProfileCatalogVersion = 1 }));
        var accMigrated = new SettingsStore(accMigrationPath);
        check(accMigrated.Value.ActiveProfile.Actions.Single(a => a.Id == "accIgnition").Key == "I" && accMigrated.Value.ActiveProfile.Actions.Single(a => a.Id == "accIgnitionOff").Key == "F2" && accMigrated.Value.ActiveProfile.Actions.Any(a => a.Id == "acc-custom"), "ACC preset migration fixes built-in bindings and preserves user-added buttons");
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
        check(host.Profile.Actions.Single(a => a.Id == "lights").Key == "F10" && host.Store.Value.Profiles.Count == 8, "Profile migration preserves existing user keys and adds all installed game profiles");
        check(host.Store.Value.Profiles.Select(p => p.Id).ToHashSet().SetEquals(GameProfiles.KnownIds), "Profile catalog migration adds ACC, AMS2, ETS2, SnowRunner and FS25 exactly once");
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
        check(reloaded.Value.Profiles.Count == 8 && reloaded.Value.ProfileCatalogVersion == AdditionalProfiles.CatalogVersion, "Additional profile migration is idempotent across restart");
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
