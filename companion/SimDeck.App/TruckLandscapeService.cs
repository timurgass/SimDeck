using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SimDeck.Core;

internal sealed class TruckLandscapeService(Func<string?> findGame,string cacheDirectory,string profile):IDisposable
{
    readonly object gate=new();readonly CancellationTokenSource stop=new();
    readonly List<TruckLandscape> loaded=[];
    bool busy;long retryAt;string status="Подготовка поверхности игры…";string fingerprint="";
    public IResult Response(HttpContext c)
    {
        if(c.Request.Query["profile"]!=profile)return Results.Conflict(new{error="Профиль игры изменился"});
        double Number(string key)=>double.TryParse(c.Request.Query[key],NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?value:double.NaN;
        var x=Number("x");var z=Number("z");var span=Number("span");
        var style=c.Request.Query["style"].ToString();if(style=="")style="terrain";
        if(style is not ("terrain" or "satellite"))return Results.BadRequest();
        if(!double.IsFinite(x)||!double.IsFinite(z)||Math.Abs(x)>1_000_000||Math.Abs(z)>1_000_000||!double.IsFinite(span)||span is <800 or >16000)return Results.BadRequest();
        lock(gate) {
            var layer=loaded.FirstOrDefault(l=>l.Style==style&&l.Covers(x,z,span));
            // Existing clients request the default terrain style and only know
            // format 1. Keep that response compatible; satellite is opt-in.
            if(layer is not null)return Results.Json(layer with{Version=style=="terrain"?1:2});
            if(!busy&&Environment.TickCount64>retryAt) {busy=true;_=Task.Run(()=>Prepare(x,z,span,style));}
            // HTTP 202 is intentional: road controls remain usable while the
            // isolated extractor works. Clients keep their previous background.
            return Results.Json(new{pending=true,message=status},statusCode:202);
        }
    }
    async Task Prepare(double x,double z,double span,string style)
    {
        var prepared=false;
        try {
            var game=findGame();if(game is null){status="Запустите игру для подготовки поверхности";return;}
            var files=Directory.GetFiles(game,"*.scs").Select(p=>new FileInfo(p)).OrderBy(f=>f.Name,StringComparer.Ordinal);
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("landscape-v2\n"+string.Join('\n',files.Select(f=>$"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"))))).ToLowerInvariant();
            lock(gate){if(hash!=fingerprint){loaded.Clear();fingerprint=hash;}}
            var cx=Math.Round(x/2000)*2000;var cz=Math.Round(z/2000)*2000;var cover=Math.Min(24000,Math.Max(8000,Math.Ceiling((span+2400)/2000)*2000));
            Directory.CreateDirectory(cacheDirectory);
            var cache=Path.Combine(cacheDirectory,$"surface-{hash}-{style}-{cx.ToString("0",CultureInfo.InvariantCulture)}-{cz.ToString("0",CultureInfo.InvariantCulture)}-{cover:0}.json");
            if(!File.Exists(cache)) {
                var directory=Path.Combine(AppContext.BaseDirectory,"scs-landscape");var dll=Path.Combine(directory,"SimDeck.ScsLandscape.dll");
                if(!File.Exists(dll))throw new FileNotFoundException("Landscape extractor missing");
                var dotnet=Path.Combine(AppContext.BaseDirectory,"dotnet.exe");
                var external=Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")??"","dotnet.exe");
                if(!File.Exists(dotnet))dotnet=File.Exists(external)?external:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe");
                // The tool is self-contained in release builds. Development can
                // use the configured runtime without requiring a global SDK.
                var exe=Path.Combine(directory,"SimDeck.ScsLandscape.exe");
                using var process=new Process{StartInfo=new(){FileName=File.Exists(Path.Combine(directory,"hostfxr.dll"))?exe:dotnet,UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true}};
                if(process.StartInfo.FileName==dotnet)process.StartInfo.ArgumentList.Add(dll);
                foreach(var arg in new[]{game,cx.ToString(CultureInfo.InvariantCulture),cz.ToString(CultureInfo.InvariantCulture),cover.ToString(CultureInfo.InvariantCulture),cache,style})process.StartInfo.ArgumentList.Add(arg);
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromMinutes(2));
                status="Читаю леса и покрытия из игры…";process.Start();
                var stderr=process.StandardError.ReadToEndAsync(timeout.Token);var stdout=process.StandardOutput.ReadToEndAsync(timeout.Token);
                try{await process.WaitForExitAsync(timeout.Token);await Task.WhenAll(stderr,stdout);}catch{if(!process.HasExited)process.Kill(true);throw;}
                if(process.ExitCode!=0||!File.Exists(cache))throw new InvalidDataException("Surface import failed");
            }
            if(new FileInfo(cache).Length>8_000_000)throw new InvalidDataException("Surface cache too large");
            await using var stream=File.OpenRead(cache);var layer=await JsonSerializer.DeserializeAsync<TruckLandscape>(stream,new JsonSerializerOptions(JsonSerializerDefaults.Web),stop.Token);
            if(layer?.IsValid()!=true||layer.Style!=style)throw new InvalidDataException("Invalid surface cache");
            lock(gate){loaded.Insert(0,layer);if(loaded.Count>4)loaded.RemoveAt(4);}
            prepared=true;
            // Bound persistent cache; game-derived files stay on this PC.
            foreach(var old in Directory.GetFiles(cacheDirectory,"surface-*.json").Select(p=>new FileInfo(p)).OrderByDescending(f=>f.LastWriteTimeUtc).Skip(16))try{old.Delete();}catch(IOException){}
            status="Леса и покрытия из файлов игры";
        }
        catch(OperationCanceledException){status="Подготовка поверхности прервана";}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException or System.ComponentModel.Win32Exception){status="Поверхность пока недоступна; дороги и управление работают";}
        finally{lock(gate){busy=false;retryAt=Environment.TickCount64+(prepared?1000:10000);}}
    }
    public void Dispose()=>stop.Cancel();
}
