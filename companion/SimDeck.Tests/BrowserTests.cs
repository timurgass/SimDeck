using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using SimDeck.App;

static class BrowserTests
{
    public static async Task Run(CompanionHost host, Action<bool,string> check)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        await host.StartBrowserAsync(port, true);
        var origin = $"http://127.0.0.1:{port}";
        var cookies = new CookieContainer();
        using var http = new HttpClient(new HttpClientHandler { UseProxy=false, CookieContainer=cookies });
        check((await http.GetStringAsync(origin)).Contains("IPHONE / SAFARI"), "Browser serves bundled UI without internet dependencies");
        var design = await http.GetStringAsync(origin + "/profile-design.css");
        check(design.Contains("data-profile=\"fs25\"") && design.Contains("data-profile=\"f1-25\""), "Browser serves game-specific mobile styles");
        check((await http.GetStringAsync(origin + "/dashboard.js")).Contains("mountProfileDashboard"), "Browser serves functional profile dashboards");
        check((await http.GetStringAsync(origin + "/vehicle.js")).Contains("updateVehiclePanel"), "Browser bundles automatic vehicle renderer without external assets");
        foreach (var image in new[] { "farm.png", "road.png", "equipment.png", "gt.png", "harvest.png" }) {
            using var response = await http.GetAsync(origin + "/vehicles/" + image);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            check(response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "image/png" && bytes.Length > 10000 && bytes.Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}), "Browser serves binary sprite " + image + " without text corruption");
        }
        check((await http.GetAsync(origin + "/vehicles/private.png")).StatusCode == HttpStatusCode.NotFound, "Sprite endpoint exposes only bundled image names");
        check((await http.GetStringAsync(origin + "/dashboard.css")).Contains("beamDial"), "Browser serves dashboard geometry without external assets");
        check((await http.GetStringAsync(origin + "/ets2-icons.js")).Contains("etsHighBeam"), "Browser serves ETS2 dashboard icons to Safari");
        var fs25Icons = await http.GetStringAsync(origin + "/fs25-icons.js");
        check(fs25Icons.Contains("fs25Attach") && fs25Icons.Contains("fs25Lower") && fs25Icons.Contains("fs25TurnOn"), "Browser serves FS25 action icons to Safari");
        check((await http.GetStringAsync(origin + "/fs25.js")).Contains("ДАННЫЕ СОХРАНЕНИЯ"), "Browser serves the FS25 save dashboard to Safari");
        check((await http.GetAsync(origin+"/status")).StatusCode==HttpStatusCode.Unauthorized, "Browser starts without authorisation");
        async Task<HttpResponseMessage> Pair(string code, string? from) {
            using var request=new HttpRequestMessage(HttpMethod.Post,origin+"/pair") { Content=JsonContent.Create(new {code}) };
            if(from is not null) request.Headers.Add("Origin",from);
            return await http.SendAsync(request);
        }
        var pin=host.Browser!.Pairing.Open();
        check((await Pair(pin,"http://foreign.example")).StatusCode==HttpStatusCode.Forbidden,"Cross-origin browser pairing rejected without consuming PIN");
        check((await Pair(pin,null)).StatusCode==HttpStatusCode.Forbidden,"Missing origin rejected");
        using(var request=new HttpRequestMessage(HttpMethod.Get,origin)){request.Headers.Host="foreign.example:"+port;check((await http.SendAsync(request)).StatusCode==HttpStatusCode.Forbidden,"Browser rejects DNS rebinding Host");}
        check((await Pair("wrong",origin)).StatusCode==HttpStatusCode.Forbidden,"Incorrect browser PIN rejected");
        check((await Pair(pin,origin)).IsSuccessStatusCode,"Browser PIN accepted for same origin");
        check((await Pair(pin,origin)).StatusCode==HttpStatusCode.Forbidden,"Browser PIN cannot be replayed");
        var cookie=cookies.GetCookies(new Uri(origin))["simdeck_browser"]!;
        check(cookie.HttpOnly && !host.Store.IsTrusted(cookie.Value),"HTTP test credential is HttpOnly and isolated from native trust");
        using var bad=new ClientWebSocket();bad.Options.Cookies=cookies;bad.Options.SetRequestHeader("Origin","http://foreign.example");bad.Options.Proxy=new WebProxy();
        try {await bad.ConnectAsync(new Uri(origin.Replace("http:","ws:")+"/ws"),CancellationToken.None);check(false,"Foreign websocket denied");}catch(WebSocketException){check(true,"Foreign websocket denied");}
        using var socket=new ClientWebSocket();socket.Options.Cookies=cookies;socket.Options.SetRequestHeader("Origin",origin);socket.Options.Proxy=new WebProxy();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await socket.ConnectAsync(new Uri(origin.Replace("http:","ws:")+"/ws"),timeout.Token);
        async Task<JsonDocument> Read() {var b=new byte[65536];var n=0;WebSocketReceiveResult r;do{r=await socket.ReceiveAsync(new ArraySegment<byte>(b,n,b.Length-n),timeout.Token);n+=r.Count;}while(!r.EndOfMessage);return JsonDocument.Parse(b.AsMemory(0,n));}
        using var hello=await Read();var h=hello.RootElement;
        check(h.GetProperty("type").GetString()=="hello" && h.GetProperty("controls").GetArrayLength()==host.Profile.Actions.Count,"Browser receives real profile and telemetry protocol");
        var action=host.Profile.Actions.First(x=>x.Gesture=="press");
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new{protocolMajor=1,type="control.invoke",sessionId=h.GetProperty("sessionId").GetString(),profileId=host.Profile.Id,profileRevision=host.Profile.Revision,commandId="browser-check",actionId=action.Id,phase="press",pressId="browser-press"}),WebSocketMessageType.Text,true,timeout.Token);
        JsonDocument ack;do{ack=await Read();}while(ack.RootElement.GetProperty("type").GetString()!="control.ack");
        check(ack.RootElement.GetProperty("code").GetString()=="game_not_focused_or_input_disabled","Browser cannot bypass disabled game input");
        host.RevokeDevices();
        check((await http.GetAsync(origin+"/status")).StatusCode==HttpStatusCode.Unauthorized,"Revocation invalidates browser cookie");
        socket.Abort();await Task.Delay(150);
        pin=host.Browser.Pairing.Open();
        check((await Pair(pin,origin)).IsSuccessStatusCode,"New browser pairing works after revocation");
        using var replacement=new ClientWebSocket();replacement.Options.Cookies=cookies;replacement.Options.SetRequestHeader("Origin",origin);replacement.Options.Proxy=new WebProxy();
        await replacement.ConnectAsync(new Uri(origin.Replace("http:","ws:")+"/ws"),timeout.Token);
        check(replacement.State==WebSocketState.Open,"New controller connects after revocation");

        var latestCookies = new CookieContainer();
        using var latestHttp = new HttpClient(new HttpClientHandler { UseProxy=false, CookieContainer=latestCookies });
        pin=host.Browser.Pairing.Open();
        using(var request=new HttpRequestMessage(HttpMethod.Post,origin+"/pair") { Content=JsonContent.Create(new {code=pin}) }) {
            request.Headers.Add("Origin",origin);
            check((await latestHttp.SendAsync(request)).IsSuccessStatusCode,"New pairing replaces an active browser session");
        }
        using var latest=new ClientWebSocket();latest.Options.Cookies=latestCookies;latest.Options.SetRequestHeader("Origin",origin);latest.Options.Proxy=new WebProxy();
        await latest.ConnectAsync(new Uri(origin.Replace("http:","ws:")+"/ws"),timeout.Token);
        check(latest.State==WebSocketState.Open,"Replacement browser receives the released controller channel");
        latest.Abort();replacement.Abort();
    }
}
