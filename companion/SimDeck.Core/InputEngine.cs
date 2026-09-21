namespace SimDeck.Core;

public interface IInputBackend
{
    bool CanInject { get; }
    bool Send(ushort scanCode, bool down);
}

public sealed record Binding(string ActionId, ushort ScanCode, string Gesture = "press", ushort[]? Modifiers = null);
public sealed record InputResult(bool Success, string Code);

public sealed class InputEngine
{
    readonly IInputBackend backend;
    readonly Func<long> clock;
    readonly Dictionary<string, Held> held = new();
    readonly Dictionary<ushort, int> owners = new();
    readonly HashSet<string> commands = new();
    readonly HashSet<string> usedPresses = new();
    readonly Dictionary<string, Binding> bindings = new();
    readonly object gate = new();
    string? session;
    bool ignitionReady;
    long stateRevision;
    sealed record Held(ushort[] Keys, long ExpiresAt, bool Renewable, bool ArmsIgnition = false);

    public InputEngine(IInputBackend backend, Func<long>? clock = null)
    { this.backend = backend; this.clock = clock ?? (() => Environment.TickCount64); }
    public int HeldCount { get { lock (gate) return held.Count; } }
    public (bool IgnitionReady, long Revision) ControlState { get { lock (gate) return (ignitionReady, stateRevision); } }
    public string LastFault { get; private set; } = "";
    public void Configure(IEnumerable<Binding> settings)
    {
        lock (gate) { ReleaseAllLocked(); bindings.Clear(); foreach (var b in settings) bindings.Add(b.ActionId, b); }
    }
    public void BeginSession(string id)
    {
        lock (gate) { ReleaseAllLocked(); session = id; commands.Clear(); usedPresses.Clear(); }
    }
    public void EndSession(string id)
    {
        lock (gate) if (session == id) { ReleaseAllLocked(); session = null; }
    }
    public InputResult Invoke(string sessionId, string commandId, string actionId, string phase, string pressId)
    {
        lock (gate)
        {
            if (session != sessionId) return new(false, "wrong_session");
            if (commandId.Length is < 1 or > 80 || pressId.Length is < 1 or > 80) return new(false, "invalid_id");
            if (commands.Contains(commandId)) return new(true, "duplicate");
            // Bound memory without allowing an old non-idempotent command to execute again.
            if (commands.Count >= 50000) return new(false, "session_limit_reconnect");
            if (!bindings.TryGetValue(actionId, out var binding)) return new(false, "unknown_action");
            commands.Add(commandId);
            if (phase == "up") { Release(pressId); usedPresses.Add(pressId); return new(true, "released"); }
            if (phase != "down" && phase != "press") return new(false, "invalid_phase");
            if (LastFault.Length > 0) return new(false, "input_fault_restart_required");
            var allowed = binding.Gesture switch
            {
                "press" => phase == "press",
                "hold" => phase == "down",
                "tapThenHold" => phase is "press" or "down",
                _ => false
            };
            if (!allowed) return new(false, "invalid_gesture");
            if (!backend.CanInject) { ReleaseAllLocked(); return new(false, "game_not_focused_or_input_disabled"); }
            var isIgnition = binding.Gesture == "tapThenHold";
            if (isIgnition && phase == "down" && !ignitionReady) return new(false, "ignition_tap_required");
            if (isIgnition && held.Values.Any(x => x.Keys.Contains(binding.ScanCode))) return new(false, "ignition_busy");
            var keys = (binding.Modifiers ?? []).Append(binding.ScanCode).Distinct().ToArray();
            // Serialize chords so Ctrl/Alt/Shift cannot change another control's meaning.
            if (held.Count > 0 && (keys.Length > 1 || held.Values.Any(x => x.Keys.Length > 1))) return new(false, "input_busy");
            if (!usedPresses.Add(pressId)) return new(false, "closed_press");
            var acquired = new List<ushort>();
            foreach (var code in keys)
            {
                if (!owners.TryGetValue(code, out var count) && !backend.Send(code, true))
                {
                    held.Add(pressId, new(acquired.ToArray(), 0, false));
                    Release(pressId);
                    return new(false, "injection_failed");
                }
                owners[code] = count + 1;
                acquired.Add(code);
            }
            var resetsVehicle = actionId is "reset" or "recover" or "recoverAlt" or "recoverRoad" or "loadHome";
            if (isIgnition || resetsVehicle) SetIgnitionReady(false);
            if (resetsVehicle)
                foreach (var pending in held.Where(x => x.Value.ArmsIgnition).Select(x => x.Key).ToArray())
                    held[pending] = held[pending] with { ArmsIgnition = false };
            held.Add(pressId, new(keys, clock() + (phase == "press" ? 100 : 500), phase == "down", isIgnition && phase == "press"));
            return new(true, "injected");
        }
    }
    public void Renew(string sessionId, IEnumerable<string> pressIds)
    {
        lock (gate)
        {
            if (session != sessionId) return;
            var now = clock();
            foreach (var id in pressIds.Take(32))
                if (held.TryGetValue(id, out var h) && h.Renewable && h.ExpiresAt > now)
                    held[id] = h with { ExpiresAt = now + 500 };
        }
    }
    public void Tick()
    {
        lock (gate)
        {
            if (!backend.CanInject) { ReleaseAllLocked(); return; }
            var now = clock();
            foreach (var id in held.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToArray()) Release(id, completedPulse: true);
        }
    }
    public void ReleaseAll() { lock (gate) ReleaseAllLocked(); }
    void ReleaseAllLocked() { SetIgnitionReady(false); foreach (var id in held.Keys.ToArray()) Release(id); }
    void SetIgnitionReady(bool value) { if (ignitionReady != value) { ignitionReady = value; stateRevision++; } }
    void Release(string id, bool completedPulse = false)
    {
        if (!held.TryGetValue(id, out var h)) return;
        for (var index = h.Keys.Length - 1; index >= 0; index--)
        {
            var code = h.Keys[index];
            var count = owners[code];
            if (count == 1 && !backend.Send(code, false))
            {
                LastFault = "Не удалось отпустить клавишу. Ввод отключён; повторяем отпускание.";
                SetIgnitionReady(false);
                held[id] = h with { Keys = h.Keys[..(index + 1)], ExpiresAt = 0, Renewable = false, ArmsIgnition = false };
                return;
            }
            if (count == 1) owners.Remove(code); else owners[code] = count - 1;
        }
        held.Remove(id);
        if (h.ArmsIgnition && completedPulse) SetIgnitionReady(true);
    }
}
