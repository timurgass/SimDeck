using System.IO;
using System.Xml;
using SimDeck.App;
using SimDeck.Core;

static class Fs25SaveWatcherTests
{
    static readonly string MidSeasonSave = Path.Combine(AppContext.BaseDirectory, "fixtures", "fs25", "save-mid-season");

    public static void Run(Action<bool, string> check)
    {
        // ---- 1. Publishes on first poll ----

        var hub = new TelemetryHub();
        var fixed_ = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        var savedMtime = new DateTime(2026, 9, 30, 11, 59, 0, DateTimeKind.Utc);
        var mockDetails = Fs25SaveReader.Read(new Fs25SavegameDir(MidSeasonSave), fixed_) with { Timestamp = savedMtime, IsStale = false };

        var watcher = new Fs25SaveWatcher(
            hub, new Fs25SavegameDir(MidSeasonSave),
            retryDelays: [],
            clock: () => fixed_,
            isGameRunning: () => true,
            sleep: (_, __) => { },
            read: (_, _) => mockDetails);

        var poll1 = watcher.PollOnce();
        check(poll1.Published && !poll1.IsStale && poll1.Details is not null,
            "Fs25SaveWatcher publishes fresh details on first poll when game is running");

        var (data, _, _) = hub.Read();
        check(data?.Fs25 is not null && data.Fs25.PlayerFarm?.Money > 0,
            "TelemetryHub receives Telemetry with Fs25 field populated");

        // ---- 2. No re-publish without mtime change ----

        var poll2 = watcher.PollOnce();
        check(!poll2.Published && poll2.Details?.Timestamp == savedMtime,
            "Second poll with same mtime does not re-publish (idempotent)");

        var readGood = true;
        var errorHub = new TelemetryHub();
        var errorWatcher = new Fs25SaveWatcher(errorHub, new Fs25SavegameDir(MidSeasonSave),
            retryDelays: [], clock: () => fixed_, isGameRunning: () => true,
            read: (_, _) => readGood ? mockDetails : throw new IOException("save is being rewritten"));
        errorWatcher.PollOnce();
        readGood = false;
        var failedPoll = errorWatcher.PollOnce();
        check(failedPoll.Published && failedPoll.IsStale && errorHub.Read().Data?.Fs25?.IsStale == true,
            "A failed save read marks the last published FS25 snapshot stale");

        // ---- 3. Stale when game process missing ----

        var hub3 = new TelemetryHub();
        var watcher3 = new Fs25SaveWatcher(
            hub3, new Fs25SavegameDir(MidSeasonSave),
            retryDelays: [],
            clock: () => fixed_,
            isGameRunning: () => false, // game not running
            sleep: (_, __) => { },
            read: (_, _) => mockDetails);
        var poll3 = watcher3.PollOnce();
        check(poll3.IsStale && poll3.Details is not null && poll3.Details.IsStale,
            "IsStale flips on when FarmingSimulator2025Game process is absent");
        check(poll3.Published,
            "Stale-because-no-process still publishes (the phone needs to see the stale flag)");

        // ---- 4. Stale when mtime beyond FreshnessWindow ----

        var oldMtime = fixed_ - Fs25SaveReader.FreshnessWindow - TimeSpan.FromMinutes(5);
        var oldDetails = mockDetails with { Timestamp = oldMtime, IsStale = true };
        var hub4 = new TelemetryHub();
        var watcher4 = new Fs25SaveWatcher(
            hub4, new Fs25SavegameDir(MidSeasonSave),
            retryDelays: [],
            clock: () => fixed_,
            isGameRunning: () => true,
            sleep: (_, __) => { },
            read: (_, _) => oldDetails);
        var poll4 = watcher4.PollOnce();
        check(poll4.IsStale && poll4.Details?.IsStale == true,
            "Save older than 30 min is marked stale even with game running");

        // ---- 5. Retries on locked file ----

        var attemptsMade = 0;
        var sleeps = new List<TimeSpan>();
        var watcher5 = new Fs25SaveWatcher(
            new TelemetryHub(), new Fs25SavegameDir(MidSeasonSave),
            retryDelays: [TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1000)],
            clock: () => fixed_,
            isGameRunning: () => true,
            sleep: (t, _) => sleeps.Add(t),
            read: (_, _) =>
            {
                attemptsMade++;
                if (attemptsMade < 3) throw new IOException("save file is locked by FS25 write");
                return mockDetails;
            });
        var poll5 = watcher5.PollOnce();
        check(attemptsMade == 3 && poll5.Details is not null && poll5.Error is null,
            "Watcher retries after IOException and succeeds on 3rd attempt");
        check(sleeps.Count == 2 && sleeps[0] == TimeSpan.FromMilliseconds(200) && sleeps[1] == TimeSpan.FromMilliseconds(500),
            "Retry ladder waits 200 ms, then 500 ms between attempts");

        var partialAttempts = 0;
        var partialWatcher = new Fs25SaveWatcher(new TelemetryHub(), new Fs25SavegameDir(MidSeasonSave),
            retryDelays: [TimeSpan.FromMilliseconds(1)], clock: () => fixed_, isGameRunning: () => true,
            sleep: (_, _) => { }, read: (_, _) =>
            {
                if (++partialAttempts == 1) throw new XmlException("partially written save");
                return mockDetails;
            });
        check(partialWatcher.PollOnce().Published && partialAttempts == 2,
            "Watcher retries XML parse errors caused by a partially written save");

        // ---- 6. Gives up after all retries exhausted ----

        var attempts6 = 0;
        var watcher6 = new Fs25SaveWatcher(
            new TelemetryHub(), new Fs25SavegameDir(MidSeasonSave),
            retryDelays: [TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)],
            clock: () => fixed_,
            isGameRunning: () => true,
            sleep: (_, __) => { },
            read: (_, _) => { attempts6++; throw new IOException("never available"); });
        var poll6 = watcher6.PollOnce();
        check(attempts6 == 3 && poll6.Details is null && poll6.Error is IOException && poll6.IsStale,
            "After 1 + 2 retries fail with IOException, watcher gives up and reports stale");

        // ---- 7. Non-IO errors are not retried ----

        var attempts7 = 0;
        var watcher7 = new Fs25SaveWatcher(
            new TelemetryHub(), new Fs25SavegameDir(MidSeasonSave),
            retryDelays: [TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)],
            clock: () => fixed_,
            isGameRunning: () => true,
            sleep: (_, __) => { },
            read: (_, _) => { attempts7++; throw new InvalidDataException("bad XML"); });
        var poll7 = watcher7.PollOnce();
        check(attempts7 == 1 && poll7.Details is null && poll7.Error is InvalidDataException,
            "Non-IO exceptions (malformed XML) short-circuit — no retry ladder");

        // ---- 8. Telemetry.Fs25 field exists and threads through ----

        var t = new Telemetry(0, 0, 0, null, 0, 0, 0, Fs25: mockDetails);
        check(t.Fs25 is not null && t.Fs25.Period.Number == 8,
            "Telemetry record accepts Fs25Details as an optional named parameter (period 8 MID_AUTUMN)");
    }
}
