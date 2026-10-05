using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SimDeck.Core;

namespace SimDeck.App;

public sealed class Ets2MapService(string dataDirectory, string profileId = "ets2") : IDisposable
{
    public string ProfileId { get; } = profileId is "ets2" or "ats" ? profileId : throw new ArgumentException("Unknown SCS game", nameof(profileId));
    public string CacheDirectory => Path.Combine(dataDirectory, ProfileId + "-map");
    string GameName => ProfileId == "ats" ? "ATS" : "ETS2";
    string ProcessName => ProfileId == "ats" ? "amtrucks" : "eurotrucks2";
    readonly object gate=new();
    readonly CancellationTokenSource stop=new();
    TruckLandscapeService? landscape;
    public IResult LandscapeResponse(HttpContext c) {TruckLandscapeService service;lock(gate)service=landscape??=new(FindGame,CacheDirectory,ProfileId);return service.Response(c);}
    Ets2MapIndex? index;
    TruckRouting? routing;
    readonly SemaphoreSlim routeGate=new(2,2);
    string revision="";
    long checkedAt;
    bool busy;
    readonly TruckGameGpsReader gpsReader=new();
    readonly SemaphoreSlim gpsGate=new(1,1);
    string gpsRevision="";
    TruckRoutePlan? gpsPlan;
    public Func<(bool Available,string Status,TruckGpsNode[] Nodes)>? GpsCapture { private get; set; }
    public string Status { get; private set; } = $"Запустите {(profileId == "ats" ? "ATS" : "ETS2")}: карта подготовится из файлов игры.";
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
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("navigator-v7\n"+string.Join('\n',files.Select(f=>$"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"))))).ToLowerInvariant();
            lock(gate) { if(index is not null && revision==hash) return; }
            var cache=Path.Combine(CacheDirectory,"roads-"+hash+".json");
            if(!File.Exists(cache))
            {
                var version=FileVersionInfo.GetVersionInfo(Path.Combine(game,"bin","win_x64",ProcessName+".exe"));
                if(version.FileMajorPart!=1 || version.FileMinorPart is <59 or >61)
                { Status=$"Для этой версии {GameName} чтение карты ещё не проверено."; return; }
                Status=$"Подготовка дорог из {GameName}…";
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
                if(process.ExitCode!=0 || !File.Exists(cache)) throw new InvalidDataException($"Не удалось прочитать карту установленной {GameName}.");
            }
            if(new FileInfo(cache).Length>300_000_000) throw new InvalidDataException("Файл карты слишком большой.");
            await using var stream=File.OpenRead(cache);
            var geometry=await JsonSerializer.DeserializeAsync<Ets2RoadMap>(stream,new JsonSerializerOptions(JsonSerializerDefaults.Web),stop.Token)
                ?? throw new InvalidDataException("Пустая карта.");
            var loaded=new Ets2MapIndex(geometry);
            var router=new TruckRouting(geometry);
            lock(gate) { index=loaded;routing=router;revision=hash; }
            Status=$"Карта готова · {geometry.Roads.Length:N0} участков · {geometry.Cities.Length} городов · {geometry.Pois?.Length??0} объектов";
        }
        catch(OperationCanceledException) { Status="Подготовка карты прервана."; }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException or System.ComponentModel.Win32Exception)
        { Status=$"Не удалось подготовить карту {GameName}. Проверьте файлы игры и перезапустите Companion."; }
        finally { lock(gate) busy=false; }
    }
    string? FindGame()
    {
        foreach(var process in Process.GetProcessesByName(ProcessName))
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
        if(context.Request.Query.ContainsKey("profile") && context.Request.Query["profile"]!=ProfileId)
            return Results.Conflict(new {error="Профиль игры изменился"});
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
    public void Dispose() { stop.Cancel();landscape?.Dispose();gpsReader.Dispose(); }
    public async Task<IResult> GameGpsResponse(HttpContext c)
    {
        if(c.Request.Query["profile"]!=ProfileId)return Results.Conflict(new{error="Профиль игры изменился"});
        Ensure();TruckRouting? router;string mapRevision;lock(gate){router=routing;mapRevision=revision;}
        if(router is null)return Results.Json(new{available=false,pending=true,message=Status});
        if(!await gpsGate.WaitAsync(TimeSpan.FromSeconds(1),c.RequestAborted))return Results.Json(new{available=false,pending=true,message="Чтение GPS занято"});
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted,stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(10));
            return await Task.Run<IResult>(()=>
            {
                var capture=GpsCapture?.Invoke();
                TruckGpsNode[] nodes;bool available;string message;
                if(capture.HasValue){(available,message,nodes)=capture.Value;}
                else{available=gpsReader.TryRead(ProfileId,out nodes);message=gpsReader.Status;}
                if(!available)return Results.Json(new{available=false,pending=false,message});
                if(nodes.Length<2){gpsRevision="";gpsPlan=null;return Results.Json(new{available=true,empty=true,message});}
                var signature=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(mapRevision+"\n"+string.Join('\n',nodes.Select(n=>$"{n.Uid}:{n.X.ToString("R",CultureInfo.InvariantCulture)}:{n.Z.ToString("R",CultureInfo.InvariantCulture)}")))));
                if(gpsRevision!=signature||gpsPlan is null){gpsPlan=router.GameRoute(nodes,timeout.Token) with{Revision=signature};gpsRevision=signature;}
                return Results.Json(new{available=true,empty=false,revision=signature,plan=gpsPlan,message="Маршрут из GPS игры"});
            },timeout.Token);
        }
        catch(Exception e) when(e is InvalidOperationException or ArgumentException){return Results.Json(new{available=false,pending=false,message=e.Message});}
        catch(OperationCanceledException){return Results.Json(new{available=false,pending=true,message="Чтение GPS прервано"});}
        finally{gpsGate.Release();}
    }
    public IResult PlacesResponse(HttpContext context)
    {
        if(context.Request.Query["profile"]!=ProfileId)return Results.Conflict(new {error="Профиль игры изменился"});
        Ensure();TruckRouting? router;lock(gate)router=routing;
        if(router is null)return Results.Json(new{error=Status},statusCode:503);
        double? Number(string name)=>double.TryParse(context.Request.Query[name],NumberStyles.Float,CultureInfo.InvariantCulture,out var v)&&double.IsFinite(v)&&Math.Abs(v)<=1_000_000?v:null;
        try{return Results.Json(new{places=router.Search(context.Request.Query["q"].ToString(),Number("x"),Number("z"),context.Request.Query["kind"].ToString() is {Length:>0} k?k:null),revision});}
        catch(ArgumentException){return Results.BadRequest();}
    }
    public async Task<IResult> RouteResponse(HttpContext context)
    {
        if(context.Request.Query["profile"]!=ProfileId)return Results.Conflict(new{error="Профиль игры изменился"});
        Ensure();TruckRouting? router;string currentRevision;lock(gate){router=routing;currentRevision=revision;}
        if(router is null)return Results.Json(new{error=Status},statusCode:503);
        if(!await routeGate.WaitAsync(TimeSpan.FromSeconds(1),context.RequestAborted))return Results.Json(new{error="Построение пути занято, повторите запрос"},statusCode:429);
        try {
            var r=await JsonSerializer.DeserializeAsync<TruckRouteRequest>(context.Request.Body,new JsonSerializerOptions(JsonSerializerDefaults.Web),context.RequestAborted);
            if(r is null)return Results.BadRequest();
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted,stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(12));
            var plan=await Task.Run(()=>router.Route(r,timeout.Token),timeout.Token);
            return Results.Json(plan with {Revision=currentRevision});
        }
        catch(Exception e) when(e is JsonException or ArgumentException or InvalidOperationException){return Results.Json(new{error=e is InvalidOperationException?e.Message:"Некорректный запрос маршрута"},statusCode:400);}
        catch(OperationCanceledException){return Results.Json(new{error="Построение маршрута прервано"},statusCode:408);}
        finally{routeGate.Release();}
    }
}
