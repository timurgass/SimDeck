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
            var vehicle = new VehicleInfo("combine", "MF 8570", "combine", [], []);
            hub.PublishFs25Live(states!, vehicle, 2000);
            var delayed = hub.Read();
            check(!hub.HasFs25Live && delayed.Age >= 2000 && delayed.Data?.Vehicle == vehicle,
                "Expired FS25 stream retains its last machine and its real stale age");
            hub.Publish(new Telemetry(0, 0, 0, null, 0, 0, 0));
            check(hub.Read().Age >= 2000 && hub.Read().Data?.ActionStates?["fs25Lower"] == true,
                "A fresh save report neither revives nor erases a stopped live stream");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMilliseconds(-700));
            var aged = Fs25LiveReader.ReadSnapshot(dir, DateTime.UtcNow);
            check(aged?.AgeMs is >= 650 and < 1200,
                "FS25 reader carries the file production age instead of resetting it on every poll");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(-5));
            check(Fs25LiveReader.Read(dir, DateTime.UtcNow) is null, "FS25 never displays an old implement state as current");
            File.WriteAllText(path, "<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///secret\">]><simdeckStatus version=\"1\" lowered=\"true\"/>");
            check(Fs25LiveReader.Read(dir, DateTime.UtcNow) is null, "FS25 live bridge rejects XML entities");
        }
        finally { Directory.Delete(dir, true); }
    }
}
