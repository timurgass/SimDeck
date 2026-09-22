namespace SimDeck.App;

public static class AdditionalProfiles
{
    public const int CatalogVersion = 1;

    public static IReadOnlyList<GameProfile> All() =>
    [
        Acc(), Automobilista2(), Ets2(), SnowRunner()
    ];

    public static GameProfile Acc() => new("acc", "Assetto Corsa Competizione", "AC2-Win64-Shipping",
    [
        A("accPitLimiter", "Гонка", "ПИТ-ЛИМИТЕР", "Pit Limiter", "P", group: "Машина"),
        A("accIgnition", "Гонка", "ЗАЖИГАНИЕ", "Ignition", "I", group: "Машина"),
        A("accStarter", "Гонка", "СТАРТЕР", "Starter · удерживайте", "S", "hold", "Машина"),
        A("accHeadlights", "Гонка", "ФАРЫ", "Headlights", "L", group: "Машина"),
        A("accFlash", "Гонка", "МИГНУТЬ", "Flasher · удерживайте", "H", "hold", "Машина"),
        A("accWipers", "Гонка", "ДВОРНИКИ", "Cycle Wipers", "W", group: "Машина"),
        A("accRainLight", "Гонка", "ДОЖДЕВОЙ ФОНАРЬ", "Rain Light", "R", group: "Машина"),
        A("accCamera", "Гонка", "КАМЕРА", "Cycle Camera", "C", group: "Обзор"),
        A("accLookLeft", "Гонка", "ВЗГЛЯД ВЛЕВО", "Look Left", "Q", "hold", "Обзор"),
        A("accLookRight", "Гонка", "ВЗГЛЯД ВПРАВО", "Look Right", "E", "hold", "Обзор"),
        A("accLookBack", "Гонка", "ВЗГЛЯД НАЗАД", "Look Back", "B", "hold", "Обзор"),
        A("accPause", "Гонка", "ПАУЗА", "Pause", "Escape", group: "Система"),

        A("accTcUp", "Электроника", "TC +", "Increase Traction Control", "Ctrl+T", group: "Помощники"),
        A("accTcDown", "Электроника", "TC −", "Decrease Traction Control", "Alt+T", group: "Помощники"),
        A("accTc2Up", "Электроника", "TC2 +", "Increase Traction Control 2", "Ctrl+D2", group: "Помощники"),
        A("accTc2Down", "Электроника", "TC2 −", "Decrease Traction Control 2", "Alt+D2", group: "Помощники"),
        A("accAbsUp", "Электроника", "ABS +", "Increase ABS", "Ctrl+A", group: "Помощники"),
        A("accAbsDown", "Электроника", "ABS −", "Decrease ABS", "Alt+A", group: "Помощники"),
        A("accMapUp", "Электроника", "КАРТА +", "Increase Engine Map", "Ctrl+M", group: "Двигатель"),
        A("accMapDown", "Электроника", "КАРТА −", "Decrease Engine Map", "Alt+M", group: "Двигатель"),
        A("accBiasUp", "Электроника", "БАЛАНС +", "Increase Brake Bias", "Ctrl+B", group: "Тормоза"),
        A("accBiasDown", "Электроника", "БАЛАНС −", "Decrease Brake Bias", "Alt+B", group: "Тормоза"),

        A("accMfdUp", "MFD", "ВВЕРХ", "MFD Up", "Up", group: "Навигация"),
        A("accMfdDown", "MFD", "ВНИЗ", "MFD Down", "Down", group: "Навигация"),
        A("accMfdLeft", "MFD", "ВЛЕВО", "MFD Left", "Left", group: "Навигация"),
        A("accMfdRight", "MFD", "ВПРАВО", "MFD Right", "Right", group: "Навигация"),
        A("accMfdSelect", "MFD", "ВЫБРАТЬ", "MFD Select", "Return", group: "Навигация"),
        A("accMfdBack", "MFD", "НАЗАД", "MFD Back", "Back", group: "Навигация"),
        A("accMfdCycle", "MFD", "СЛЕДУЮЩАЯ СТРАНИЦА", "Cycle MFD", "Insert", group: "Страницы"),
        A("accDashUp", "MFD", "ДИСПЛЕЙ +", "Display Page Up", "PageUp", group: "Страницы"),
        A("accDashDown", "MFD", "ДИСПЛЕЙ −", "Display Page Down", "PageDown", group: "Страницы"),
        A("accRequestPit", "MFD", "ЗАПРОС ПИТ-СТОПА", "Request Pit Stop", "F1", group: "Пит-стоп")
    ], 1);

    public static GameProfile Automobilista2() => new("ams2", "Automobilista 2", "AMS2AVX",
    [
        A("amsPitLimiter", "Гонка", "ПИТ-ЛИМИТЕР", "Pit Limiter", "P", group: "Машина"),
        A("amsIgnition", "Гонка", "ЗАЖИГАНИЕ", "Ignition", "I", group: "Машина"),
        A("amsStarter", "Гонка", "СТАРТЕР", "Starter · удерживайте", "S", "hold", "Машина"),
        A("amsHeadlights", "Гонка", "ФАРЫ", "Headlights", "L", group: "Машина"),
        A("amsFlash", "Гонка", "МИГНУТЬ", "Headlight Flash", "H", "hold", "Машина"),
        A("amsWipers", "Гонка", "ДВОРНИКИ", "Cycle Wipers", "W", group: "Машина"),
        A("amsCamera", "Гонка", "КАМЕРА", "Cycle Camera", "C", group: "Обзор"),
        A("amsLookLeft", "Гонка", "ВЗГЛЯД ВЛЕВО", "Look Left", "Q", "hold", "Обзор"),
        A("amsLookRight", "Гонка", "ВЗГЛЯД ВПРАВО", "Look Right", "E", "hold", "Обзор"),
        A("amsLookBack", "Гонка", "ВЗГЛЯД НАЗАД", "Look Back", "B", "hold", "Обзор"),
        A("amsReset", "Гонка", "ВОССТАНОВИТЬ", "Reset Car", "F10", group: "Система"),
        A("amsPause", "Гонка", "ПАУЗА", "Pause", "Escape", group: "Система"),

        A("amsTcUp", "Электроника", "TC +", "Increase Traction Control", "Ctrl+T", group: "Помощники"),
        A("amsTcDown", "Электроника", "TC −", "Decrease Traction Control", "Alt+T", group: "Помощники"),
        A("amsAbsUp", "Электроника", "ABS +", "Increase ABS", "Ctrl+A", group: "Помощники"),
        A("amsAbsDown", "Электроника", "ABS −", "Decrease ABS", "Alt+A", group: "Помощники"),
        A("amsBiasUp", "Электроника", "БАЛАНС +", "Increase Brake Bias", "Ctrl+B", group: "Тормоза"),
        A("amsBiasDown", "Электроника", "БАЛАНС −", "Decrease Brake Bias", "Alt+B", group: "Тормоза"),
        A("amsMapUp", "Электроника", "КАРТА +", "Increase Engine Map", "Ctrl+M", group: "Двигатель"),
        A("amsMapDown", "Электроника", "КАРТА −", "Decrease Engine Map", "Alt+M", group: "Двигатель"),
        A("amsBoostUp", "Электроника", "БУСТ +", "Increase Boost", "Ctrl+U", group: "Двигатель"),
        A("amsBoostDown", "Электроника", "БУСТ −", "Decrease Boost", "Alt+U", group: "Двигатель"),

        A("amsIcmUp", "Пит и HUD", "ВВЕРХ", "ICM Up", "Up", group: "Навигация"),
        A("amsIcmDown", "Пит и HUD", "ВНИЗ", "ICM Down", "Down", group: "Навигация"),
        A("amsIcmLeft", "Пит и HUD", "ВЛЕВО", "ICM Left", "Left", group: "Навигация"),
        A("amsIcmRight", "Пит и HUD", "ВПРАВО", "ICM Right", "Right", group: "Навигация"),
        A("amsIcmSelect", "Пит и HUD", "ВЫБРАТЬ", "ICM Select", "Return", group: "Навигация"),
        A("amsIcmBack", "Пит и HUD", "НАЗАД", "ICM Back", "Back", group: "Навигация"),
        A("amsIcmCycle", "Пит и HUD", "ОТКРЫТЬ ICM", "Cycle ICM", "Insert", group: "Панель"),
        A("amsHud", "Пит и HUD", "HUD", "Cycle HUD", "PageUp", group: "Панель"),
        A("amsRequestPit", "Пит и HUD", "ЗАПРОС ПИТ-СТОПА", "Request Pit Stop", "F1", group: "Пит-стоп")
    ], 1);

    public static GameProfile Ets2() => new("ets2", "Euro Truck Simulator 2", "eurotrucks2",
    [
        A("etsEngine", "Вождение", "ДВИГАТЕЛЬ", "Start / Stop Engine", "E", group: "Грузовик"),
        A("etsParkingBrake", "Вождение", "РУЧНИК", "Parking Brake", "Space", group: "Грузовик"),
        A("etsEngineBrake", "Вождение", "МОТОРНЫЙ ТОРМОЗ", "Engine Brake", "B", group: "Грузовик"),
        A("etsRetarderUp", "Вождение", "РЕТАРДЕР +", "Retarder Increase", "Ctrl+R", group: "Грузовик"),
        A("etsRetarderDown", "Вождение", "РЕТАРДЕР −", "Retarder Decrease", "Alt+R", group: "Грузовик"),
        A("etsDifferential", "Вождение", "БЛОКИРОВКА", "Differential Lock", "V", group: "Грузовик"),
        A("etsAttachTrailer", "Вождение", "ПРИЦЕП", "Attach / Detach Trailer", "T", group: "Грузовик"),
        A("etsLiftAxle", "Вождение", "ПОДЪЁМНАЯ ОСЬ", "Lift Truck Axle", "U", group: "Грузовик"),
        A("etsLiftTrailerAxle", "Вождение", "ОСЬ ПРИЦЕПА", "Lift Trailer Axle", "J", group: "Грузовик"),
        A("etsHorn", "Вождение", "СИГНАЛ", "Horn · удерживайте", "H", "hold", "Сигналы"),
        A("etsAirHorn", "Вождение", "ПНЕВМОСИГНАЛ", "Air Horn · удерживайте", "N", "hold", "Сигналы"),

        A("etsLights", "Свет", "ФАРЫ", "Light Modes", "L", group: "Свет"),
        A("etsHighBeam", "Свет", "ДАЛЬНИЙ", "High Beam", "K", group: "Свет"),
        A("etsBeacon", "Свет", "МАЯЧКИ", "Beacon", "O", group: "Свет"),
        A("etsHazards", "Свет", "АВАРИЙКА", "Hazard Warning", "F", group: "Свет"),
        A("etsLeftSignal", "Свет", "ЛЕВЫЙ", "Left Turn Indicator", "OemComma", group: "Поворотники"),
        A("etsRightSignal", "Свет", "ПРАВЫЙ", "Right Turn Indicator", "OemPeriod", group: "Поворотники"),
        A("etsWipers", "Свет", "ДВОРНИКИ +", "Wipers Increase", "P", group: "Дворники"),
        A("etsWipersDown", "Свет", "ДВОРНИКИ −", "Wipers Decrease", "Shift+P", group: "Дворники"),

        A("etsCruise", "Круиз", "КРУИЗ", "Cruise Control", "C", group: "Круиз-контроль"),
        A("etsCruiseResume", "Круиз", "ВОЗОБНОВИТЬ", "Cruise Resume", "Ctrl+C", group: "Круиз-контроль"),
        A("etsCruiseUp", "Круиз", "СКОРОСТЬ +", "Cruise Increase", "OemPlus", group: "Круиз-контроль"),
        A("etsCruiseDown", "Круиз", "СКОРОСТЬ −", "Cruise Decrease", "OemMinus", group: "Круиз-контроль"),

        A("etsMap", "Интерфейс", "КАРТА", "World Map", "M", group: "Навигация"),
        A("etsRouteAdvisor", "Интерфейс", "МАРШРУТНЫЙ СОВЕТНИК", "Route Advisor", "F3", group: "Навигация"),
        A("etsMirrors", "Интерфейс", "ЗЕРКАЛА", "Virtual Mirrors", "F2", group: "Кабина"),
        A("etsDashboard", "Интерфейс", "ДИСПЛЕЙ", "Dashboard Display", "D", group: "Кабина"),
        A("etsCamera", "Интерфейс", "КАМЕРА", "Next Camera", "D1", group: "Кабина"),
        A("etsQuickSave", "Интерфейс", "БЫСТРОЕ СОХРАНЕНИЕ", "Quick Save", "F5", group: "Система"),
        A("etsQuickLoad", "Интерфейс", "БЫСТРАЯ ЗАГРУЗКА", "Quick Load", "F9", group: "Система"),
        A("etsPause", "Интерфейс", "ПАУЗА", "Pause", "Escape", group: "Система")
    ], 1);

    public static GameProfile SnowRunner() => new("snowrunner", "SnowRunner", "SnowRunner",
    [
        A("snowEngine", "Вождение", "ДВИГАТЕЛЬ", "Start / Stop Engine", "F6", group: "Машина"),
        A("snowParkingBrake", "Вождение", "РУЧНИК", "Parking Brake", "Space", group: "Машина"),
        A("snowAwd", "Вождение", "ПОЛНЫЙ ПРИВОД", "All-Wheel Drive", "E", group: "Трансмиссия"),
        A("snowDifferential", "Вождение", "БЛОКИРОВКА", "Differential Lock", "Q", group: "Трансмиссия"),
        A("snowGearbox", "Вождение", "КОРОБКА", "Gearbox Selector · удерживайте", "LeftShift", "hold", "Трансмиссия"),
        A("snowHorn", "Вождение", "СИГНАЛ", "Horn · удерживайте", "H", "hold", "Машина"),
        A("snowHeadlights", "Вождение", "ФАРЫ", "Headlights", "L", group: "Машина"),
        A("snowCamera", "Вождение", "КАМЕРА", "Change Camera", "C", group: "Обзор"),
        A("snowCockpit", "Вождение", "КАБИНА", "Cockpit Camera", "D1", group: "Обзор"),
        A("snowPause", "Вождение", "ПАУЗА", "Pause", "Escape", group: "Система"),

        A("snowFunctions", "Функции", "ФУНКЦИИ", "Open Functions", "V", group: "Меню машины"),
        A("snowQuickWinch", "Функции", "БЫСТРАЯ ЛЕБЁДКА", "Quick Winch", "F", group: "Лебёдка"),
        A("snowWinch", "Функции", "ЛЕБЁДКА", "Attach Winch · удерживайте", "G", "hold", "Лебёдка"),
        A("snowReleaseWinch", "Функции", "ОТПУСТИТЬ ЛЕБЁДКУ", "Release Winch", "R", group: "Лебёдка"),
        A("snowCrane", "Функции", "КРАН", "Crane Mode", "D2", group: "Оборудование"),
        A("snowAnchors", "Функции", "ОПОРЫ", "Deploy Anchors", "D3", group: "Оборудование"),
        A("snowPackCargo", "Функции", "ЗАКРЕПИТЬ ГРУЗ", "Pack / Unpack Cargo", "D4", group: "Груз"),
        A("snowTrailer", "Функции", "ПРИЦЕП", "Attach Trailer", "T", group: "Прицеп"),
        A("snowDetachTrailer", "Функции", "ОТЦЕПИТЬ", "Detach Trailer", "Ctrl+T", group: "Прицеп"),
        A("snowChangeTruck", "Функции", "СМЕНИТЬ МАШИНУ", "Change Truck", "D5", group: "Машина"),

        A("snowMap", "Навигация", "КАРТА", "Open Map", "M", group: "Карта"),
        A("snowTasks", "Навигация", "ЗАДАЧИ", "Tasks", "Tab", group: "Карта"),
        A("snowGarage", "Навигация", "ГАРАЖ", "Garage", "F4", group: "Сервис"),
        A("snowRecover", "Навигация", "ЭВАКУАЦИЯ", "Recover", "Back", group: "Сервис"),
        A("snowRefuel", "Навигация", "ЗАПРАВКА", "Refuel", "Ctrl+F", group: "Сервис"),
        A("snowRepair", "Навигация", "РЕМОНТ", "Repair", "Ctrl+R", group: "Сервис")
    ], 1);

    static DeckAction A(string id, string page, string label, string description, string key,
        string gesture = "press", string group = "") => new(id, page, label, description, key, gesture, group);
}
