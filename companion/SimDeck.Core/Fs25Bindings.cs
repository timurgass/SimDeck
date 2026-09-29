namespace SimDeck.Core;

/// <summary>
/// Reads the player's own Farming Simulator 25 key bindings so the deck follows the
/// game instead of the game being rewritten to follow the deck.
/// </summary>
/// <remarks>
/// Source file is "My Games/FarmingSimulator2025/inputBinding.xml", written by the game
/// itself. Verified against FS25 1.23.1.0: 280 actions, 220 of them bound to a plain key.
/// <para>
/// FS25 stores an action as a space-separated token list, e.g. <c>KEY_lctrl KEY_b</c>.
/// SimDeck's <c>WindowsInput.ParseBinding</c> wants <c>Ctrl+B</c>: modifiers first,
/// exactly one real key last. Translating between the two is all this class does.
/// </para>
/// </remarks>
public sealed class Fs25Bindings
{
    /// <summary>Action name as FS25 spells it, e.g. LOWER_IMPLEMENT, mapped to "V" or "Ctrl+B".</summary>
    readonly Dictionary<string, string> keys;

    Fs25Bindings(Dictionary<string, string> keys) => this.keys = keys;

    public int Count => keys.Count;

    public static Fs25Bindings Empty { get; } = new(new(StringComparer.Ordinal));

    /// <summary>The player's key for <paramref name="action"/>, or null when unbound or unusable.</summary>
    public string? Key(string action) => keys.TryGetValue(action, out var key) ? key : null;

    /// <summary>The player's key, falling back to the FS25 factory default we shipped with the profile.</summary>
    public string KeyOr(string action, string fallback) => Key(action) ?? fallback;

    public bool IsBound(string action) => keys.ContainsKey(action);

    public static Fs25Bindings Load(string inputBindingXmlPath)
    {
        if (!File.Exists(inputBindingXmlPath)) return Empty;
        using var stream = File.OpenRead(inputBindingXmlPath);
        return Read(stream);
    }

    public static Fs25Bindings Read(Stream xml)
    {
        var document = System.Xml.Linq.XDocument.Load(xml);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var node in document.Descendants("actionBinding"))
        {
            var action = (string?)node.Attribute("action");
            if (string.IsNullOrWhiteSpace(action) || result.ContainsKey(action)) continue;

            // Several <binding> entries per action are normal (keyboard, mouse, gamepad).
            // Take the first keyboard-only one; the rest cannot be injected as a key press.
            foreach (var binding in node.Elements("binding"))
            {
                if ((string?)binding.Attribute("device") is not "KB_MOUSE_DEFAULT") continue;
                if (TryTranslate((string?)binding.Attribute("input"), out var key))
                {
                    result[action] = key;
                    break;
                }
            }
        }

        return new Fs25Bindings(result);
    }

    /// <summary>
    /// "KEY_lctrl KEY_b" to "Ctrl+B". Returns false for anything the deck cannot press:
    /// mouse buttons, axes, and bare modifiers such as the clutch bound to KEY_lshift.
    /// </summary>
    public static bool TryTranslate(string? fs25Input, out string key)
    {
        key = "";
        if (string.IsNullOrWhiteSpace(fs25Input)) return false;

        var tokens = fs25Input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length is 0 or > 4) return false;

        var modifiers = new List<string>();
        string? main = null;

        foreach (var token in tokens)
        {
            if (!token.StartsWith("KEY_", StringComparison.Ordinal)) return false; // MOUSE_*, AXIS_*
            var name = token[4..];

            var modifier = name switch
            {
                "lshift" or "rshift" => "Shift",
                "lctrl" or "rctrl" => "Ctrl",
                "lalt" or "ralt" => "Alt",
                _ => null,
            };

            if (modifier is not null)
            {
                if (!modifiers.Contains(modifier)) modifiers.Add(modifier);
                continue;
            }

            if (main is not null) return false; // two real keys is not a deck binding
            if (!WpfKeyNames.TryGetValue(name, out main)) return false;
        }

        if (main is null) return false; // modifier-only, e.g. the clutch on KEY_lshift

        // SimDeck expects Ctrl/Alt/Shift in front of exactly one key.
        key = modifiers.Count == 0 ? main : string.Join('+', modifiers.Append(main));
        return true;
    }

    /// <summary>
    /// FS25 key token to the name <c>WindowsInput.ParseKey</c> accepts, which is a
    /// <c>System.Windows.Input.Key</c> enum member. Digits are D0-D9 and punctuation is Oem*.
    /// </summary>
    static readonly Dictionary<string, string> WpfKeyNames = BuildKeyNames();

    static Dictionary<string, string> BuildKeyNames()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["space"] = "Space",
            ["return"] = "Return",
            ["enter"] = "Return",
            ["esc"] = "Escape",
            ["escape"] = "Escape",
            ["tab"] = "Tab",
            ["backspace"] = "Back",
            ["delete"] = "Delete",
            ["insert"] = "Insert",
            ["home"] = "Home",
            ["end"] = "End",
            ["pageup"] = "PageUp",
            ["pagedown"] = "PageDown",
            ["pause"] = "Pause",
            ["print"] = "PrintScreen",
            ["capslock"] = "CapsLock",
            ["numlock"] = "NumLock",
            ["scrolllock"] = "Scroll",
            ["left"] = "Left",
            ["right"] = "Right",
            ["up"] = "Up",
            ["down"] = "Down",
            ["comma"] = "OemComma",
            ["period"] = "OemPeriod",
            ["minus"] = "OemMinus",
            ["equals"] = "OemPlus",
            ["semicolon"] = "OemSemicolon",
            ["slash"] = "OemQuestion",
            ["backslash"] = "OemPipe",
            ["lbracket"] = "OemOpenBrackets",
            ["rbracket"] = "OemCloseBrackets",
            ["apostrophe"] = "OemQuotes",
            ["grave"] = "OemTilde",
            // The numpad matters for FS25: turn signals, work lights and the range
            // groups all live there by default, unlike the numpad-free F1 preset.
            ["KP_plus"] = "Add",
            ["KP_minus"] = "Subtract",
            ["KP_multiply"] = "Multiply",
            ["KP_divide"] = "Divide",
            ["KP_period"] = "Decimal",
            ["KP_enter"] = "Return",
        };

        for (var c = 'a'; c <= 'z'; c++) map[c.ToString()] = char.ToUpperInvariant(c).ToString();
        for (var d = 0; d <= 9; d++) map[d.ToString()] = $"D{d}";
        for (var d = 0; d <= 9; d++) map[$"KP_{d}"] = $"NumPad{d}";
        for (var f = 1; f <= 12; f++) map[$"f{f}"] = $"F{f}";

        return map;
    }
}
