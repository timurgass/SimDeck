using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using SimDeck.App;

static class AppSmoke
{
    public static async Task Run(string executable, string stateDirectory)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
        start.ArgumentList.Add("--data-dir"); start.ArgumentList.Add(stateDirectory); start.ArgumentList.Add("--demo");
        using var process = Process.Start(start) ?? throw new Exception("Cannot start Companion");
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var settingsPath = Path.Combine(stateDirectory, "settings.json");
            while (!File.Exists(settingsPath)) { if (process.HasExited) throw new Exception("Companion exited at startup"); await Task.Delay(100, timeout.Token); }
            var settings = JsonSerializer.Deserialize<Settings>(await File.ReadAllTextAsync(settingsPath, timeout.Token))!;
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false, ServerCertificateCustomValidationCallback = (_, c, _, _) => c?.Thumbprint == settings.CertificateThumbprint });
            while (true)
            {
                try
                {
                    var response = await http.GetAsync($"https://127.0.0.1:{settings.Port}/health", timeout.Token);
                    response.EnsureSuccessStatusCode(); break;
                }
                catch (HttpRequestException) { await Task.Delay(100, timeout.Token); }
            }
            process.Refresh();
            if (process.HasExited) throw new Exception("Companion exited unexpectedly");
            Console.WriteLine("PASS Published self-contained Companion starts and serves pinned HTTPS");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await process.WaitForExitAsync(exitTimeout.Token); }
                catch (OperationCanceledException) { process.Kill(); throw new Exception("Companion did not close normally"); }
                Console.WriteLine("PASS Companion closes normally");
            }
        }
    }
}
