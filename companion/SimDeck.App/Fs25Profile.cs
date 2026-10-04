using SimDeck.Core;

namespace SimDeck.App;

/// <summary>
/// Farming Simulator 25 deck profile.
/// </summary>
/// <remarks>
/// Unlike the F1 profile, nothing is written into the game: FS25 publishes the player's
/// own bindings in "My Games/FarmingSimulator2025/inputBinding.xml", so the deck reads
/// them and follows. The keys spelled out below are only the factory defaults, used when
/// an action is unbound or the file cannot be read.
/// <para>
/// FS25 deliberately reuses one key across contexts — V lowers an implement but detaches
/// a yarder cable, B has five meanings depending on what you are sitting in. The deck
/// sends the key; the game decides what it means. Labels therefore name the arable-farming
/// meaning and the descriptions say so, rather than pretending a button is single-purpose.
/// </para>
/// </remarks>
public static class Fs25Profile
{
    public const int Revision = 4;

    /// <summary>Process name without ".exe", from the game's own FarmingSimulator2025.xml cmdline.</summary>
    public const string TargetProcess = "FarmingSimulator2025Game";

    /// <summary>One deck button: what it means in FS25, and the key to fall back on.</summary>
    sealed record Spec(
        string Id, string GameAction, string Page, string Label, string Description,
        string FallbackKey, string Gesture = "press", string Group = "");

    public static GameProfile Default() => Build(Fs25Bindings.Empty);

    public static GameProfile Build(Fs25Bindings bindings) =>
        new("fs25", "Farming Simulator 25", TargetProcess,
            [.. Specs.Select(s => new DeckAction(s.Id, s.Page, s.Label, s.Description,
                bindings.KeyOr(s.GameAction, s.FallbackKey), s.Gesture, s.Group))],
            Revision);

    /// <summary>FS25 action names the profile expects, for the Companion diagnostics panel.</summary>
    public static IReadOnlyList<string> RequiredGameActions() => [.. Specs.Select(s => s.GameAction)];

    /// <summary>Buttons whose key the player has rebound away from the FS25 default.</summary>
    public static IReadOnlyList<(string Label, string Default, string Actual)> Rebound(Fs25Bindings bindings) =>
    [
        .. Specs.Select(s => (s.Label, s.FallbackKey, Actual: bindings.Key(s.GameAction)))
                .Where(x => x.Actual is not null && x.Actual != x.FallbackKey)
                .Select(x => (x.Label, x.FallbackKey, x.Actual!))
    ];

    /// <summary>Apply only keyboard actions found in the player's file. Keep personal edits and buttons.</summary>
    public static GameProfile ApplyPlayerBindings(GameProfile existing, Fs25Bindings bindings)
    {
        if (existing.Id != "fs25") throw new ArgumentException("Откройте профиль Farming Simulator 25.");
        var actionsById = Specs.ToDictionary(s => s.Id, StringComparer.Ordinal);
        return existing with
        {
            Actions = existing.Actions.Select(action =>
                actionsById.TryGetValue(action.Id, out var spec) && bindings.Key(spec.GameAction) is { } key
                    ? action with { Key = key }
                    : action).ToList()
        };
    }

    /// <summary>Add the ground-unload control without resetting any existing pages or custom bindings.</summary>
    public static GameProfile UpgradeUnloading(GameProfile existing, Fs25Bindings bindings)
    {
        var shipped=Build(bindings).Actions;
        var unload=shipped.Single(a=>a.Id=="fs25Unload");
        var updated=existing.Actions.Select(a=>a.Id==unload.Id
            ? a with { Label=unload.Label, Description=unload.Description, Key=bindings.Key("TOGGLE_TIPSTATE") ?? a.Key }
            : a).ToList();
        if(updated.All(a=>a.Id!="fs25UnloadHere")) updated.Add(shipped.Single(a=>a.Id=="fs25UnloadHere"));
        return existing with { Actions=updated, Revision=checked(existing.Revision+1) };
    }

    public static GameProfile UpgradeNavigation(GameProfile existing)
    {
        if(existing.Actions.Any(a=>a.Id=="fs25Back")) return existing;
        var back=Default().Actions.Single(a=>a.Id=="fs25Back");
        return existing with { Actions=[..existing.Actions,back], Revision=Math.Max(Revision,existing.Revision+1) };
    }

    static readonly Spec[] Specs =
    [
        // ---- Орудие: основной рабочий цикл в поле ----
        new("fs25Lower", "LOWER_IMPLEMENT", "Орудие", "ОПУСТИТЬ / ПОДНЯТЬ",
            "Опускает и поднимает текущее орудие", "V", Group: "Работа"),
        new("fs25LowerAll", "LOWER_ALL_IMPLEMENTS", "Орудие", "ОПУСТИТЬ / ПОДНЯТЬ ВСЁ",
            "Все прицепленные орудия сразу", "Ctrl+V", Group: "Работа"),
        new("fs25TurnOn", "IMPLEMENT_EXTRA", "Орудие", "ВКЛ / ВЫКЛ АГРЕГАТ",
            "Включает или выключает работу выбранного агрегата; на комбайне — молотилку", "B", Group: "Работа"),
        new("fs25TurnOnAll", "TURN_ON_ALL_IMPLEMENTS", "Орудие", "ВКЛ / ВЫКЛ ВСЁ",
            "Все орудия сразу", "Ctrl+B", Group: "Работа"),
        new("fs25Fold", "FOLD_ALL_IMPLEMENTS", "Орудие", "СЛОЖИТЬ / РАЗЛОЖИТЬ",
            "Транспортное и рабочее положение", "Ctrl+X", Group: "Работа"),
        new("fs25WorkWidth", "VARIABLE_WORK_WIDTH_TOGGLE", "Орудие", "ШИРИНА ЗАХВАТА",
            "Только на орудиях с изменяемой шириной", "Ctrl+Z", Group: "Работа"),
        new("fs25WorkMode", "TOGGLE_WORKMODE", "Орудие", "РЕЖИМ РАБОТЫ",
            "Переключает режим у орудий, где он есть", "Ctrl+Y", Group: "Работа"),
        new("fs25Attach", "ATTACH", "Орудие", "ПРИЦЕПИТЬ / ОТЦЕПИТЬ",
            "Q выполняет доступное действие сцепки; отдельная Ctrl+Q нужна лишь в особых случаях", "Q", Group: "Сцепка"),
        new("fs25NextImplement", "SWITCH_IMPLEMENT", "Орудие", "СЛЕДУЮЩЕЕ ОРУДИЕ",
            "Передаёт управление другому орудию в сцепке", "G", Group: "Сцепка"),
        new("fs25PrevImplement", "SWITCH_IMPLEMENT_BACK", "Орудие", "ПРЕДЫДУЩЕЕ ОРУДИЕ",
            "Обратный порядок", "Shift+G", Group: "Сцепка"),
        new("fs25Extra2", "IMPLEMENT_EXTRA2", "Орудие", "ДОП. ФУНКЦИЯ 2",
            "Назначение зависит от орудия", "X", Group: "Дополнительно"),
        new("fs25Extra3", "IMPLEMENT_EXTRA3", "Орудие", "ДОП. ФУНКЦИЯ 3",
            "Назначение зависит от орудия", "Y", Group: "Дополнительно"),
        new("fs25Extra4", "IMPLEMENT_EXTRA4", "Орудие", "ДОП. ФУНКЦИЯ 4",
            "Назначение зависит от орудия", "Z", Group: "Дополнительно"),

        // ---- Посев и уборка ----
        new("fs25Seeds", "TOGGLE_SEEDS", "Посев", "ВЫБРАТЬ КУЛЬТУРУ",
            "Перебирает культуры, доступные сеялке", "Y", Group: "Сеялка"),
        new("fs25SeedsBack", "TOGGLE_SEEDS_BACK", "Посев", "КУЛЬТУРА НАЗАД",
            "Обратный порядок", "Shift+Y", Group: "Сеялка"),
        new("fs25DoubleSpray", "DOUBLED_SPRAY_AMOUNT", "Посев", "ДВОЙНАЯ НОРМА",
            "Удвоенный расход удобрения за проход", "OemComma", Group: "Внесение"),
        new("fs25Pipe", "TOGGLE_PIPE", "Посев", "ТРУБА ВЫГРУЗКИ",
            "Выдвигает трубу комбайна", "O", Group: "Уборка"),
        new("fs25Unload", "TOGGLE_TIPSTATE", "Посев", "РАЗГРУЗИТЬ В ТОЧКЕ",
            "Запускает/останавливает выгрузку прицепа в зоне приёма; нужна непустая ёмкость", "I", Group: "Уборка"),
        new("fs25UnloadHere", "TOGGLE_TIPSTATE_GROUND", "Посев", "РАЗГРУЗИТЬ ЗДЕСЬ",
            "Выгрузка на землю в разрешённом месте; нужна непустая ёмкость и выбранный прицеп", "Ctrl+I", Group: "Уборка"),
        new("fs25TipSide", "TOGGLE_TIPSIDE", "Посев", "СТОРОНА СВАЛКИ",
            "Выбирает сторону разгрузки прицепа", "U", Group: "Уборка"),
        new("fs25Cover", "TOGGLE_COVER", "Посев", "ТЕНТ / КРЫШКА",
            "Открывает и закрывает кузов", "N", Group: "Уборка"),
        new("fs25Chopper", "TOGGLE_CHOPPER", "Посев", "ИЗМЕЛЬЧИТЕЛЬ",
            "Солому в измельчение или в валок", "OemComma", Group: "Уборка"),

        // ---- Трактор ----
        new("fs25Motor", "TOGGLE_MOTOR_STATE", "Трактор", "ДВИГАТЕЛЬ",
            "Пуск и остановка двигателя", "Return", Group: "Движение"),
        new("fs25Direction", "DIRECTION_CHANGE", "Трактор", "СМЕНА НАПРАВЛЕНИЯ",
            "Вперёд или назад", "Space", Group: "Движение"),
        new("fs25Cruise", "TOGGLE_CRUISE_CONTROL", "Трактор", "КРУИЗ-КОНТРОЛЬ",
            "Держит скорость на гоне", "D3", Group: "Движение"),
        new("fs25GearUp", "SHIFT_GEAR_UP", "Трактор", "ПЕРЕДАЧА +",
            "Только в ручной коробке", "Add", Group: "Коробка"),
        new("fs25GearDown", "SHIFT_GEAR_DOWN", "Трактор", "ПЕРЕДАЧА −",
            "Только в ручной коробке", "Subtract", Group: "Коробка"),
        new("fs25GroupUp", "SHIFT_GROUP_UP", "Трактор", "ДИАПАЗОН +",
            "Повышающий ряд", "NumPad9", Group: "Коробка"),
        new("fs25GroupDown", "SHIFT_GROUP_DOWN", "Трактор", "ДИАПАЗОН −",
            "Понижающий ряд", "NumPad7", Group: "Коробка"),
        new("fs25Lights", "TOGGLE_LIGHTS", "Трактор", "СВЕТ",
            "Ближний свет", "F", Group: "Свет"),
        new("fs25HighBeam", "TOGGLE_HIGH_BEAM_LIGHT", "Трактор", "ДАЛЬНИЙ",
            "Дальний свет", "Ctrl+F", Group: "Свет"),
        new("fs25WorkLightFront", "TOGGLE_WORK_LIGHT_FRONT", "Трактор", "РАБОЧИЙ ПЕРЕД",
            "Передние рабочие фары", "NumPad5", Group: "Свет"),
        new("fs25WorkLightBack", "TOGGLE_WORK_LIGHT_BACK", "Трактор", "РАБОЧИЙ ЗАД",
            "Задние рабочие фары", "NumPad6", Group: "Свет"),
        new("fs25Beacon", "TOGGLE_BEACON_LIGHTS", "Трактор", "МАЯЧОК",
            "Проблесковый маячок для дороги", "Home", Group: "Свет"),
        new("fs25TurnLeft", "TOGGLE_TURNLIGHT_LEFT", "Трактор", "ПОВОРОТ ВЛЕВО",
            "Левый указатель", "NumPad1", Group: "Сигналы"),
        new("fs25TurnRight", "TOGGLE_TURNLIGHT_RIGHT", "Трактор", "ПОВОРОТ ВПРАВО",
            "Правый указатель", "NumPad3", Group: "Сигналы"),
        new("fs25Hazard", "TOGGLE_TURNLIGHT_HAZARD", "Трактор", "АВАРИЙКА",
            "Обе стороны сразу", "NumPad2", Group: "Сигналы"),
        new("fs25Horn", "HONK", "Трактор", "СИГНАЛ",
            "Звучит, пока держите палец", "D0", Gesture: "hold", Group: "Сигналы"),
        new("fs25Camera", "CAMERA_SWITCH", "Трактор", "КАМЕРА",
            "Следующая камера", "C", Group: "Обзор"),
        new("fs25Axle", "LIFTABLE_AXLE", "Трактор", "ПОДЪЁМНАЯ ОСЬ",
            "Только там, где ось есть", "Z", Group: "Обзор"),

        // ---- Помощник и машины ----
        new("fs25Helper", "TOGGLE_AI", "Помощник", "НАНЯТЬ / УВОЛИТЬ",
            "Сажает помощника за текущую машину", "H", Group: "Помощник"),
        new("fs25NextVehicle", "SWITCH_VEHICLE", "Помощник", "СЛЕДУЮЩАЯ МАШИНА",
            "Переключает управление", "Tab", Group: "Машины"),
        new("fs25PrevVehicle", "SWITCH_VEHICLE_BACK", "Помощник", "ПРЕДЫДУЩАЯ МАШИНА",
            "Обратный порядок", "Shift+Tab", Group: "Машины"),
        new("fs25Enter", "ENTER", "Помощник", "ВОЙТИ / ВЫЙТИ",
            "Садиться и выходить из машины", "E", Group: "Машины"),
        new("fs25Seat", "SWITCH_SEAT", "Помощник", "СМЕНИТЬ МЕСТО",
            "Только в машинах с несколькими местами", "Ctrl+G", Group: "Машины"),

        // ---- Хозяйство ----
        new("fs25Menu", "MENU", "Хозяйство", "МЕНЮ",
            "Главное меню игры", "Escape", Group: "Экраны"),
        new("fs25Back", "MENU_BACK", "Хозяйство", "НАЗАД / ЗАКРЫТЬ МЕНЮ",
            "Возврат из игрового меню или карты; Escape", "Escape", Group: "Экраны"),
        new("fs25Store", "TOGGLE_STORE", "Хозяйство", "МАГАЗИН",
            "Открывает магазин техники", "P", Group: "Экраны"),
        new("fs25Map", "TOGGLE_MAP_SIZE", "Хозяйство", "КАРТА",
            "Меняет размер карты", "D9", Group: "Экраны"),
        new("fs25Construction", "TOGGLE_CONSTRUCTION", "Хозяйство", "СТРОИТЕЛЬСТВО",
            "Режим постройки", "Shift+P", Group: "Экраны"),
        new("fs25Help", "TOGGLE_HELP_TEXT", "Хозяйство", "ПОДСКАЗКИ",
            "Показывает игровые подсказки", "F1", Group: "Экраны"),
        new("fs25Pause", "PAUSE", "Хозяйство", "ПАУЗА",
            "Останавливает время", "Pause", Group: "Время"),
        new("fs25TimeUp", "INCREASE_TIMESCALE", "Хозяйство", "ВРЕМЯ +",
            "Ускоряет игровое время", "D8", Group: "Время"),
        new("fs25TimeDown", "DECREASE_TIMESCALE", "Хозяйство", "ВРЕМЯ −",
            "Замедляет игровое время", "D7", Group: "Время"),
        new("fs25Radio", "RADIO_TOGGLE", "Хозяйство", "РАДИО",
            "Включает и выключает радио", "D5", Group: "Время"),
    ];

    /// <summary>
    /// Keeps the player's extra buttons when the shipped profile gains new ones,
    /// the same way BeamNG and ACC migrate.
    /// </summary>
    public static GameProfile Upgrade(GameProfile existing, Fs25Bindings bindings)
    {
        var current = Build(bindings);
        var factoryIds = current.Actions.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var custom = existing.Actions.Where(a => !factoryIds.Contains(a.Id) && a.Id != "fs25Detach").ToList();
        var oldById = existing.Actions.ToDictionary(a => a.Id, StringComparer.Ordinal);
        return current with
        {
            Actions = [.. current.Actions.Select(a => oldById.TryGetValue(a.Id, out var old)
                ? a with { Key = bindings.Key(Specs.Single(s => s.Id == a.Id).GameAction) ?? old.Key }
                : a), .. custom],
            Revision = Math.Max(current.Revision, existing.Revision + 1),
        };
    }
}
