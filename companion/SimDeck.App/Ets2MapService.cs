using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SimDeck.Core;

namespace SimDeck.App;

public sealed class Ets2MapService(string dataDirectory) : IDisposable
{
    readonly object gate=new();
    readonly CancellationTokenSource stop=new();
    Ets2MapIndex? index;
    string revision="";
    long checkedAt;
    bool busy;
    public string Status { get; private set; } = "Запустите ETS2: карта подготовится из файлов игры.";
    public void Ensure()
    {
        lock(gate)
        {
            if(busy || Environment.TickCount64-checkedAt<30_000) return;
            busy=true; checkedAt=Environment.TickCount64;
        }
        _=Task.Run(Prepare);
    }
    async Task Prepare()
    {
        try
        {
            var game=FindGame();
            if(game is null) return;
            var files=Directory.GetFiles(game,"*.scs").OrderBy(Path.GetFileName,StringComparer.Ordinal).Select(p=>new FileInfo(p));
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("vector-v1\n"+string.Join('\n',files.Select(f=>$"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"))))).ToLowerInvariant();
            lock(gate) { if(index is not null && revision==hash) return; index=null; }
            var cache=Path.Combine(dataDirectory,"ets2-map","roads-"+hash+".json");
            if(!File.Exists(cache))
            {
                var version=FileVersionInfo.GetVersionInfo(Path.Combine(game,"bin","win_x64","eurotrucks2.exe"));
                if(version.FileMajorPart!=1 || version.FileMinorPart is <59 or >61)
                { Status="Для этой версии ETS2 чтение карты ещё не проверено."; return; }
                Status="Подготовка дорог из ETS2…";
                Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
                var executable=Path.Combine(AppContext.BaseDirectory,"SimDeck.exe");
                if(!File.Exists(executable)) { Status="Подготовьте карту в Companion.";return; }
                using var process=new Process { StartInfo=new() { FileName=executable,UseShellExecute=false,CreateNoWindow=true } };
                process.StartInfo.ArgumentList.Add("--ets2-map-build");
                process.StartInfo.ArgumentList.Add(game);process.StartInfo.ArgumentList.Add(cache);
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromMinutes(2));
                process.Start();
                try { await process.WaitForExitAsync(timeout.Token); }
                catch { if(!process.HasExited)process.Kill(true);throw; }
                if(process.ExitCode!=0 || !File.Exists(cache)) throw new InvalidDataException("Не удалось прочитать карту установленной ETS2.");
            }
            if(new FileInfo(cache).Length>100_000_000) throw new InvalidDataException("Файл карты слишком большой.");
            await using var stream=File.OpenRead(cache);
            var geometry=await JsonSerializer.DeserializeAsync<Ets2RoadMap>(stream,new JsonSerializerOptions(JsonSerializerDefaults.Web),stop.Token)
                ?? throw new InvalidDataException("Пустая карта.");
            var loaded=new Ets2MapIndex(geometry);
            lock(gate) { index=loaded;revision=hash; }
            Status=$"Карта готова · {geometry.Roads.Length:N0} участков · {geometry.Cities.Length} городов";
        }
        catch(OperationCanceledException) { Status="Подготовка карты прервана."; }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException or System.ComponentModel.Win32Exception)
        { Status="Не удалось подготовить карту ETS2. Проверьте файлы игры и перезапустите Companion."; }
        finally { lock(gate) busy=false; }
    }
    static string? FindGame()
    {
        foreach(var process in Process.GetProcessesByName("eurotrucks2"))
        {
            using(process) try
            {
                var file=process.MainModule?.FileName;
                var root=file is null ? null:Directory.GetParent(file)?.Parent?.Parent?.FullName;
                if(root is not null && File.Exists(Path.Combine(root,"def.scs")) && File.Exists(Path.Combine(root,"base_map.scs"))) return root;
            }
            catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException) { }
        }
        return null;
    }
    public IResult Response(HttpContext context)
    {
        Ensure();
        if(!double.TryParse(context.Request.Query["x"],NumberStyles.Float,CultureInfo.InvariantCulture,out var x) ||
           !double.TryParse(context.Request.Query["z"],NumberStyles.Float,CultureInfo.InvariantCulture,out var z) ||
           !double.TryParse(context.Request.Query["span"],NumberStyles.Float,CultureInfo.InvariantCulture,out var span)) return Results.BadRequest();
        lock(gate)
        {
            if(index is null) return Results.Json(new { error=Status },statusCode:503);
            try { return Results.Json(index.Slice(revision,x,z,span)); }
            catch(ArgumentException) { return Results.BadRequest(); }
        }
    }
    public void Dispose() { stop.Cancel(); }
}
