namespace SimDeck.App;

public sealed record GameProfile(string Id, string Name, string TargetProcess, List<DeckAction> Actions, int Revision = 3);

public static class GameProfiles
{
    public const int MaxActions = 96;
    public const int F1PresetVersion = 2;
    public static readonly HashSet<string> KnownIds =
    ["beamng-default", "f1-24", "f1-25", "acc", "ams2", "ets2", "snowrunner"];
    public static GameProfile F1()
    {
        using var stream = typeof(GameProfiles).Assembly.GetManifestResourceStream("SimDeck.F1Preset.json")!;
        var actions = System.Text.Json.JsonSerializer.Deserialize<List<DeckAction>>(stream,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        return new("f1-24", "F1 24", "F1_24", actions, 4);
    }
    public static GameProfile F125()
    {
        var source = F1();
        return source with { Id = "f1-25", Name = "F1 25", TargetProcess = "F1_25" };
    }
    public static GameProfile UpgradeF1(GameProfile old)
    {
        var current = F1();
        var retired = new HashSet<string> { "drs", "ers" };
        var builtIn = current.Actions.Select(a => a.Id).ToHashSet();
        var custom = old.Actions.Where(a => !builtIn.Contains(a.Id) && !retired.Contains(a.Id));
        return current with { Actions = [.. current.Actions, .. custom], Revision = checked(old.Revision + 1) };
    }

    public static void Validate(GameProfile p)
    {
        if (!KnownIds.Contains(p.Id)) throw new ArgumentException("Неизвестный профиль.");
        if (string.IsNullOrWhiteSpace(p.TargetProcess) || p.TargetProcess.IndexOfAny(['/', '\\']) >= 0) throw new ArgumentException("Введите имя процесса без пути.");
        if (p.Actions.Count is < 1 or > MaxActions) throw new ArgumentException("В профиле должно быть от 1 до 96 кнопок.");
        if (p.Actions.Select(a => a.Id).Distinct().Count() != p.Actions.Count) throw new ArgumentException("Повторяющиеся идентификаторы кнопок.");
        foreach (var a in p.Actions)
        {
            if (string.IsNullOrWhiteSpace(a.Id) || string.IsNullOrWhiteSpace(a.Label) || a.Label.Length > 36 || string.IsNullOrWhiteSpace(a.Page) || a.Page.Length > 24 || a.Group.Length > 24 || a.Description.Length > 100)
                throw new ArgumentException("Заполните название (до 36 символов) и страницу (до 24); описание — до 100 символов.");
            if (a.Gesture is not ("press" or "hold" or "tapThenHold") || (a.Gesture == "tapThenHold" && a.Id != "ignition") || (a.Id == "ignition" && a.Gesture != "tapThenHold"))
                throw new ArgumentException("Для зажигания используется только отдельное нажатие, затем удержание.");
            WindowsInput.ParseBinding(a.Id, a.Key, a.Gesture);
        }
    }

    public static string Help(GameProfile profile) => profile.Id switch
    {
        "f1-24" or "f1-25" => $"{profile.Name} · SimDeck 75: выберите изменённый Keyboard Preset 2. MFD — B, лимитер — P. 69 кнопок в трёх разделах; без нампада.",
        "beamng-default" => "BeamNG: мод SimDeck → 127.0.0.1:4444. Подсветка берётся из машины. Для новых клавиш, которых нет в телеметрии, отображается только физическое нажатие.",
        "acc" => "ACC: Shared Memory подключается автоматически после выхода на трассу. Установите готовый пресет; выключение зажигания выполняется штатно через Electronics MFD.",
        "ams2" => "Automobilista 2: назначьте клавиши SimDeck в Controls. Для будущей телеметрии включите Shared Memory → Project CARS 2.",
        "ets2" => "ETS2: назначьте те же клавиши в Keys & Buttons. Этот профиль пока работает как button box без телеметрии.",
        "snowrunner" => "SnowRunner: сверьте назначения в Settings → Controls. Этот профиль пока работает как button box без телеметрии.",
        _ => profile.Name
    };
}
