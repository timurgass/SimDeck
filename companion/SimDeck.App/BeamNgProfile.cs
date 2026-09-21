namespace SimDeck.App;

public sealed record DeckAction(string Id, string Page, string Label, string Description, string Key, string Gesture = "press", string Group = "");
public static class BeamNgProfile
{
    public const int Revision = 3;
    // Verified against BeamNG 0.39.4 settings/inputmaps/keyboard.json.
    public static readonly DeckAction[] Actions =
    [
        new("lights", "Основное", "СВЕТ", "Переключить фары", "N"),
        new("horn", "Основное", "СИГНАЛ", "Удерживайте", "H", "hold"),
        new("ignition", "Основное", "ЗАЖИГАНИЕ", "Нажать, затем держать", "V", "tapThenHold"),
        new("reset", "Основное", "СБРОС", "Восстановить машину", "R"),
        new("esc", "Системы", "ESC / TCS", "Следующий режим", "Ctrl+Q"),
        new("gearbox", "Системы", "КОРОБКА", "Аркада / реализм", "Q"),
        new("fourWheelDrive", "Системы", "2WD / 4WD", "Переключить привод", "Alt+S"),
        new("differentials", "Системы", "БЛОКИРОВКИ", "Дифференциалы", "Alt+D"),
        new("range", "Системы", "ПОНИЖАЮЩАЯ", "Высокий / низкий ряд", "Alt+A"),
        new("leftSignal", "Свет", "ЛЕВЫЙ", "Поворотник", "OemComma"),
        new("rightSignal", "Свет", "ПРАВЫЙ", "Поворотник", "OemPeriod"),
        new("hazards", "Свет", "АВАРИЙКА", "Включить / выключить", "OemQuestion"),
        new("fogLights", "Свет", "ПРОТИВОТУМАНКИ", "Переключить", "Alt+N"),
        new("lightbar", "Свет", "СПЕЦСИГНАЛЫ", "Маячки / сирена", "Shift+N"),
        new("lightbarMode", "Свет", "РЕЖИМ МАЯЧКОВ", "Следующий режим", "Alt+Shift+N"),
        new("camera", "Основное", "КАМЕРА", "Следующий вид", "C"),
        new("couplers", "Основное", "СЦЕПКА", "Соединить / отсоединить", "L"),
        new("recover", "Возврат", "ВЕРНУТЬ", "Удерживайте: перемотка", "Insert", "hold"),
        new("recoverAlt", "Возврат", "ВЕРНУТЬ · ВАР. 2", "Удерживайте: другой вариант", "Ctrl+Insert", "hold"),
        new("recoverRoad", "Возврат", "НА ДОРОГУ", "Последняя дорога · Alt+T", "Alt+T"),
        new("loadHome", "Возврат", "К ПОЗИЦИИ", "Вернуть в сохранённое · Home", "Home"),
        new("saveHome", "Возврат", "СОХРАНИТЬ", "Текущую позицию · Ctrl+Home", "Ctrl+Home")
    ];
    public static bool AddMissingKeys(Dictionary<string, string> keys)
    {
        var changed = false;
        foreach (var retired in new[] { "shiftUp", "shiftDown", "parkingBrake", "handbrakeHold" }) changed |= keys.Remove(retired);
        foreach (var action in Actions) changed |= keys.TryAdd(action.Id, action.Key);
        return changed;
    }
}
