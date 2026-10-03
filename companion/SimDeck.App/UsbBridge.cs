using System.Diagnostics;
using System.IO;

namespace SimDeck.App;

public sealed record UsbDevice(string Serial, string State);

/// <summary>Prepares a reverse tunnel; pairing and choosing the PC stay on Android.</summary>
public static class UsbBridge
{
    public static IReadOnlyList<UsbDevice> ParseDevices(string output) => output.Split('\n')
        .Select(line => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .Where(parts => parts.Length >= 2 && parts[1] is "device" or "unauthorized" or "offline")
        // A network-debugging endpoint is not the USB cable chosen in this UI.
        .Where(parts => !parts[0].Contains(':'))
        .Select(parts => new UsbDevice(parts[0], parts[1])).ToArray();

    public static string? FindAdb()
    {
        var candidates = new List<string> { Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe") };
        foreach (var name in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } sdk) candidates.Add(Path.Combine(sdk, "platform-tools", "adb.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe"));
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => p.Length > 0).Select(p => Path.Combine(p.Trim('"'), "adb.exe")));
        return candidates.FirstOrDefault(File.Exists);
    }

    public static async Task<string> PrepareAsync(int port)
    {
        if (port is < 1024 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var adb = FindAdb();
        if (adb is null) return "Не найден ADB. Установите Android Platform Tools и добавьте их в PATH; затем снова нажмите USB.";
        var devices = ParseDevices(await RunAsync(adb, ["devices", "-l"]));
        if (devices.Count == 0) return "Подключите Android кабелем с передачей данных и включите «Отладка по USB». Затем снова нажмите USB.";
        if (devices.Count > 1) return "Обнаружено несколько Android-устройств. Оставьте подключённым один планшет и снова нажмите USB.";
        if (devices[0].State == "unauthorized") return "Подтвердите разрешение отладки на планшете и снова нажмите USB.";
        if (devices[0].State != "device") return "Android пока не готов. Переподключите USB-кабель и повторите.";
        string[] prefix = ["-s", devices[0].Serial, "reverse"];
        await RunAsync(adb, [.. prefix, $"tcp:{port}", $"tcp:{port}"]);
        var tunnels = await RunAsync(adb, [.. prefix, "--list"]);
        if (!tunnels.Split('\n').Any(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) is { Length: >= 3 } parts && parts[^2] == $"tcp:{port}" && parts[^1] == $"tcp:{port}"))
            throw new IOException("USB-туннель не подтверждён ADB. Повторите подключение.");
        return "USB готов. На планшете выберите сохранённый ПК → USB. При первом подключении сначала выполните сопряжение по Wi-Fi. Для iPhone используйте Safari по Wi-Fi.";
    }

    static async Task<string> RunAsync(string executable, string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Не удалось запустить ADB.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(); throw new TimeoutException("ADB не ответил за 10 секунд. Переподключите кабель и повторите."); }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0) throw new IOException("ADB не подготовил USB. Проверьте кабель, драйвер и разрешение отладки на планшете.");
        return output;
    }
}
