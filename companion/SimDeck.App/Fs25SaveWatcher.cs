using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using SimDeck.Core;

namespace SimDeck.App;

/// <summary>
/// Watches the FS25 savegame directory and republishes <see cref="Fs25Details"/> through
/// <see cref="TelemetryHub"/> whenever the save is rewritten by the game.
/// FS25 writes the save non-atomically — the reader retries at 200/500/1000 ms on file locks.
/// Staleness combines the mtime window from <see cref="Fs25SaveReader"/> with a process check:
/// no <c>FarmingSimulator2025Game</c> process ⇒ save is stale even if mtime is fresh.
/// </summary>
public sealed class Fs25SaveWatcher : IDisposable
{
    /// <summary>Retry schedule for locked-save reads. 200/500/1000 ms per DEVELOPING-GAMES.md.</summary>
    public static readonly IReadOnlyList<TimeSpan> DefaultRetryDelays = new[]
    {
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1000),
    };

    /// <summary>Name of the game process that indicates the user is actually playing.</summary>
    public const string GameProcessName = "FarmingSimulator2025Game";

    /// <summary>How often the watcher polls the save directory. Save writes are rare.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    readonly TelemetryHub hub;
    readonly Fs25SavegameDir dir;
    readonly IReadOnlyList<TimeSpan> retryDelays;
    readonly Func<DateTime> clock;
    readonly Func<bool> isGameRunning;
    readonly Action<TimeSpan, CancellationToken> sleep;
    readonly Func<Fs25SavegameDir, DateTime, Fs25Details> read;
    readonly Func<Fs25Plan?> plan;
    readonly Func<Fs25CropCatalogView?> catalog;
    readonly Action<Telemetry> publish;

    CancellationTokenSource? cts;
    Task? loop;
    DateTime lastPublishedMtime;
    bool? lastPublishedStale;
    Fs25Plan? lastPublishedPlan;
    Fs25Details? lastGoodDetails;

    public Fs25SaveWatcher(
        TelemetryHub hub,
        Fs25SavegameDir dir,
        IReadOnlyList<TimeSpan>? retryDelays = null,
        Func<DateTime>? clock = null,
        Func<bool>? isGameRunning = null,
        Action<TimeSpan, CancellationToken>? sleep = null,
        Func<Fs25SavegameDir, DateTime, Fs25Details>? read = null,
        Func<Fs25Plan?>? plan = null,
        Func<Fs25CropCatalogView?>? catalog = null,
        Action<Telemetry>? publish = null)
    {
        this.hub = hub;
        this.dir = dir;
        this.retryDelays = retryDelays ?? DefaultRetryDelays;
        this.clock = clock ?? (() => DateTime.UtcNow);
        this.isGameRunning = isGameRunning ?? DefaultGameRunningCheck;
        this.sleep = sleep ?? ((t, ct) => { try { Task.Delay(t, ct).Wait(ct); } catch (OperationCanceledException) { } });
        this.read = read ?? ((d, n) => Fs25SaveReader.Read(d, n));
        this.plan = plan ?? (() => null);
        this.catalog = catalog ?? (() => null);
        this.publish = publish ?? hub.Publish;
    }

    public void Start()
    {
        if (cts is not null) return;
        cts = new CancellationTokenSource();
        loop = Task.Run(() => RunLoop(cts.Token));
    }

    public void Stop()
    {
        cts?.Cancel();
        try { loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        cts?.Dispose(); cts = null; loop = null;
    }

    public void Dispose() => Stop();

    void RunLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { PollOnce(); } catch { /* swallow — the next poll will retry */ }
            sleep(PollInterval, ct);
        }
    }

    /// <summary>
    /// Runs one poll: read the save (with retries on IOException), decide staleness,
    /// and publish to the hub if the file has changed since the last publish.
    /// Public so tests can drive the watcher deterministically without a timer.
    /// Returns the outcome for tests to assert on.
    /// </summary>
    public Fs25WatcherPoll PollOnce()
    {
        var now = clock();
        Exception? lastError = null;
        Fs25Details? details = null;

        // Retry ladder for non-atomic writes.
        var attempts = 1 + retryDelays.Count;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                details = read(dir, now);
                lastError = null;
                break;
            }
            catch (Exception ex) when (ex is IOException or XmlException)
            {
                lastError = ex;
                if (attempt < retryDelays.Count)
                {
                    sleep(retryDelays[attempt], CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                // Programming/data errors are not transient save-write races.
                lastError = ex;
                break;
            }
        }

        if (details is null)
        {
            if (lastGoodDetails is null || lastPublishedStale == true)
                return new Fs25WatcherPoll(Published: false, IsStale: true, Details: lastGoodDetails, Error: lastError);
            var staleDetails = lastGoodDetails with { IsStale = true };
            publish(new Telemetry(0, 0, 0, null, 0, 0, 0, Fs25: staleDetails,
                Fs25Advisor: Fs25Rules.Evaluate(staleDetails, plan(), catalog())));
            lastPublishedStale = true;
            return new Fs25WatcherPoll(Published: true, IsStale: true, Details: staleDetails, Error: lastError);
        }

        // Combine the mtime staleness (already set by the reader) with the process check.
        var gameRunning = SafeIsGameRunning();
        var isStale = details.IsStale || !gameRunning;
        var effective = isStale && !details.IsStale
            ? details with { IsStale = true }
            : details;
        lastGoodDetails = details;

        var currentPlan = plan();
        var changed = effective.Timestamp != lastPublishedMtime || effective.IsStale != lastPublishedStale
            || !ReferenceEquals(currentPlan, lastPublishedPlan);
        if (changed)
        {
            publish(new Telemetry(
                SpeedMps: 0, Rpm: 0, Gear: 0, FuelFraction: null,
                Throttle: 0, Brake: 0, Clutch: 0,
                Fs25: effective,
                Fs25Advisor: Fs25Rules.Evaluate(effective, currentPlan, catalog())));
            lastPublishedMtime = effective.Timestamp;
            lastPublishedStale = effective.IsStale;
            lastPublishedPlan = currentPlan;
        }

        return new Fs25WatcherPoll(Published: changed, IsStale: isStale, Details: effective, Error: null);
    }

    bool SafeIsGameRunning()
    {
        try { return isGameRunning(); }
        catch { return false; }
    }

    static bool DefaultGameRunningCheck()
    {
        try { return Process.GetProcessesByName(GameProcessName).Length > 0; }
        catch { return false; }
    }
}

/// <summary>Outcome of a single <see cref="Fs25SaveWatcher.PollOnce"/> call.</summary>
public sealed record Fs25WatcherPoll(bool Published, bool IsStale, Fs25Details? Details, Exception? Error);
