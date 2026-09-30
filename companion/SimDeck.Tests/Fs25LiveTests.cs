using System.Text;
using System.IO;
using SimDeck.App;
using SimDeck.Core;

static class Fs25LiveTests
{
    public static void Run(Action<bool, string> check)
    {
        var dir = Path.Combine(Path.GetTempPath(), "simdeck-fs25-live-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, Fs25LiveReader.FileName);
            File.WriteAllText(path, "<simdeckStatus version=\"1\" lowered=\"true\" turnedOn=\"false\" motor=\"true\"/>", Encoding.UTF8);
            var states = Fs25LiveReader.Read(dir, DateTime.UtcNow);
            check(states is not null && states["fs25Lower"] && !states["fs25TurnOn"] && states["fs25Motor"],
                "FS25 live bridge reads actual lowering, implement power and motor states");
            var hub = new TelemetryHub();
            hub.Reset("fs25");
            hub.Publish(new Telemetry(0, 0, 0, null, 0, 0, 0));
            hub.PublishFs25Live(states!);
            var snapshot = hub.Read();
            check(snapshot.Age < 500 && snapshot.Data?.ActionStates?["fs25Lower"] == true,
                "FS25 live states overlay save data without making it stale");
            hub.ClearFs25Live();
            check(hub.Read().Data?.ActionStates is null, "FS25 removes state indicators when live bridge disappears");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(-5));
            check(Fs25LiveReader.Read(dir, DateTime.UtcNow) is null, "FS25 never displays an old implement state as current");
            File.WriteAllText(path, "<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///secret\">]><simdeckStatus version=\"1\" lowered=\"true\"/>");
            check(Fs25LiveReader.Read(dir, DateTime.UtcNow) is null, "FS25 live bridge rejects XML entities");
        }
        finally { Directory.Delete(dir, true); }
    }
}
