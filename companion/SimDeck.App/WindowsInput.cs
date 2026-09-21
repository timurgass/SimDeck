using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using SimDeck.Core;

namespace SimDeck.App;

public sealed class WindowsInput : IInputBackend
{
    public volatile bool Enabled;
    public volatile bool Demo;
    public string TargetProcess = "BeamNG.drive.x64";
    public bool CanInject
    {
        get
        {
            if (!Enabled || Demo) return false;
            GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
            try { return Process.GetProcessById((int)pid).ProcessName.Equals(TargetProcess, StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
    }
    public bool Send(ushort code, bool down)
    {
        if (down && !CanInject) return false;
        var input = new INPUT { type = 1, data = new InputUnion { keyboard = new KEYBDINPUT {
            scanCode = (ushort)(code & 0xFF), flags = 0x0008u | (down ? 0u : 0x0002u) | ((code & 0xFF00) != 0 ? 0x0001u : 0u)
        } } };
        return SendInput(1, [input], Marshal.SizeOf<INPUT>()) == 1;
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
