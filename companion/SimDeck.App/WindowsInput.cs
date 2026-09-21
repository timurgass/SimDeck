using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using SimDeck.Core;

namespace SimDeck.App;

public sealed class WindowsInput : IInputBackend
{
    public volatile bool Enabled;
    public volatile bool Demo;
    public volatile bool UseVirtualKey;
    public string TargetProcess = "BeamNG.drive.x64";
    public string InputModeName => UseVirtualKey ? "Virtual-Key" : "Scan Code";
    public string LastSendStatus { get; private set; } = "Команд ещё не было";
    public string ForegroundProcessName
    {
        get
        {
            var window = GetForegroundWindow();
            if (window == 0) return "нет активного окна";
            GetWindowThreadProcessId(window, out var pid);
            if (pid == 0) return "неизвестный процесс";
            try { return Process.GetProcessById((int)pid).ProcessName; }
            catch (ArgumentException) { return "процесс закрыт"; }
            catch (InvalidOperationException) { return "процесс недоступен"; }
        }
    }
    public bool CanInject
    {
        get
        {
            if (!Enabled || Demo) return false;
            return ForegroundProcessName.Equals(TargetProcess, StringComparison.OrdinalIgnoreCase);
        }
    }
    public bool Send(ushort code, bool down)
    {
        if (down && !CanInject) return false;
        var fields = DescribeInput(code, down, UseVirtualKey);
        if (UseVirtualKey && fields.VirtualKey == 0)
        {
            LastSendStatus = $"Virtual-Key: не удалось преобразовать scan code 0x{code:X4}";
            return false;
        }
        var keyboard = new KEYBDINPUT { virtualKey = fields.VirtualKey, scanCode = fields.ScanCode, flags = fields.Flags };
        var input = new INPUT { type = 1, data = new InputUnion { keyboard = keyboard } };
        var sent = SendInput(1, [input], Marshal.SizeOf<INPUT>()) == 1;
        LastSendStatus = sent
            ? $"{InputModeName}: Windows приняла {(down ? "нажатие" : "отпускание")}"
            : $"{InputModeName}: SendInput вернул ошибку {Marshal.GetLastWin32Error()}";
        return sent;
    }
    public static (ushort VirtualKey, ushort ScanCode, uint Flags) DescribeInput(ushort code, bool down, bool useVirtualKey)
    {
        const uint keyUp = 0x0002, extended = 0x0001, scanCode = 0x0008;
        var isExtended = (code & 0xFF00) != 0;
        return useVirtualKey
            ? ((ushort)MapVirtualKey(code, 3), 0, (down ? 0u : keyUp) | (isExtended ? extended : 0u))
            : (0, (ushort)(code & 0xFF), scanCode | (down ? 0u : keyUp) | (isExtended ? extended : 0u));
    }
    public static ushort ParseKey(string name)
    {
        if (!Enum.TryParse<Key>(name, true, out var key) || key == Key.None) throw new ArgumentException($"Неизвестная клавиша: {name}. Примеры: L, Space, F1, D1.");
        var code = MapVirtualKey((uint)KeyInterop.VirtualKeyFromKey(key), 4);
        if (code == 0) throw new ArgumentException($"Нет scan code: {name}");
        // Some keyboard layouts return the base scan code even with mapping mode 4.
        // Preserve physical navigation keys instead of injecting their numpad twins.
        if (key is Key.Up or Key.Down or Key.Left or Key.Right or Key.Insert or Key.Delete
            or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.RightCtrl
            or Key.RightAlt or Key.Divide or Key.NumLock or Key.PrintScreen
            or Key.LWin or Key.RWin or Key.Apps)
            code = (code & 0xFF) | 0xE000;
        return (ushort)code;
    }
    public static Binding ParseBinding(string action, string key, string gesture)
    {
        var parts = key.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 4 || parts.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Пример сочетания: Ctrl+Q, Alt+Shift+N.");
        var modifiers = parts[..^1].Select(p => p.ToLowerInvariant() switch
        {
            "ctrl" or "control" => ParseKey("LeftCtrl"),
            "alt" => ParseKey("LeftAlt"),
            "shift" => ParseKey("LeftShift"),
            _ => throw new ArgumentException("Перед последней клавишей разрешены только Ctrl, Alt, Shift.")
        }).Distinct().ToArray();
        var main = ParseKey(parts[^1]);
        if (modifiers.Contains(main)) throw new ArgumentException("Основная клавиша не должна повторять модификатор.");
        return new(action, main, gesture, modifiers);
    }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion data; }
    [StructLayout(LayoutKind.Explicit)] struct InputUnion
    { [FieldOffset(0)] public KEYBDINPUT keyboard; [FieldOffset(0)] public MOUSEINPUT mouse; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT
    { public ushort virtualKey, scanCode; public uint flags, time; public nuint extraInfo; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT
    { public int dx, dy; public uint mouseData, flags, time; public nuint extraInfo; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
}
