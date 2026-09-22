using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SimDeck.Core;

namespace SimDeck.App;

// Explicitly started, temporary LAN test endpoint. Native clients retain pinned TLS.
public sealed class BrowserHost(CompanionHost host) : IAsyncDisposable
{
    public PairingGate Pairing { get; } = new();
    public int Port { get; private set; }
    const string Cookie = "simdeck_browser";
    readonly ConcurrentDictionary<string, long> sessions = new();
    readonly HashSet<string> addresses = ["127.0.0.1", "::1"];
    readonly object gate = new();
    CancellationTokenSource revoked = new();
    WebApplication? app;
    public void Revoke() { lock(gate) { sessions.Clear(); var old = revoked; revoked = new(); old.Cancel(); old.Dispose(); } }
    public async Task StartAsync(int port, bool localOnly)
    {
        Port = port;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            foreach (var ip in nic.GetIPProperties().UnicastAddresses) addresses.Add(ip.Address.ToString());
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(o => {
            o.Limits.MaxRequestBodySize = 1024;
            o.Listen(localOnly ? IPAddress.Loopback : IPAddress.Any, port);
        });
        app = builder.Build();
        app.Use(async (c, next) => {
            // Reject DNS rebinding and non-LAN peers. No forwarded headers are trusted.
            if (!addresses.Contains(c.Request.Host.Host) || c.Request.Host.Port != Port || !Local(c.Connection.RemoteIpAddress))
            { c.Response.StatusCode = 403; return; }
            c.Response.Headers.CacheControl = "no-store";
            c.Response.Headers["X-Content-Type-Options"] = "nosniff";
            c.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
            await next(c);
        });
        app.UseWebSockets();
        app.MapGet("/", () => Asset("index.html", "text/html; charset=utf-8"));
        app.MapGet("/app.js", () => Asset("app.js", "text/javascript; charset=utf-8"));
        app.MapGet("/style.css", () => Asset("style.css", "text/css; charset=utf-8"));
        app.MapGet("/f1-circuits.json", () => Asset("f1-circuits.json", "application/json; charset=utf-8"));
        app.MapPost("/pair", async (HttpContext c) => {
            if (!SameOrigin(c)) return Results.StatusCode(403);
            try {
                using var doc = await JsonDocument.ParseAsync(c.Request.Body, cancellationToken: c.RequestAborted);
                if (!Pairing.Consume(doc.RootElement.GetProperty("code").GetString() ?? "")) return Results.StatusCode(403);
                var token = PairingGate.NewToken();
                // Only hashes in memory; never add HTTP credentials to the native trust store.
                Revoke();
                await host.DisconnectControllerAsync(c.RequestAborted);
                sessions[PairingGate.Hash(token)] = Environment.TickCount64 + 8 * 60 * 60 * 1000;
                c.Response.Cookies.Append(Cookie, token, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromHours(8) });
                return Results.Json(new { ok = true });
            } catch (Exception e) when(e is JsonException or KeyNotFoundException or InvalidOperationException) { return Results.BadRequest(); }
        });
        app.MapGet("/status", (HttpContext c) => Trusted(c) ? Results.Json(new { paired = true }) : Results.StatusCode(401));
        app.Map("/ws", async c => {
            CancellationTokenSource lifetime;
            lock(gate) {
                if (!SameOrigin(c) || !Trusted(c)) { c.Response.StatusCode = 401; return; }
                lifetime = CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted, revoked.Token);
                var expiry = sessions[PairingGate.Hash(c.Request.Cookies[Cookie]!)];
                lifetime.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1,expiry-Environment.TickCount64)));
            }
            using var cleanup = lifetime;
            c.RequestAborted = lifetime.Token;
            var browserToken = c.Request.Cookies[Cookie]!;
            await host.ServeController(c, "Safari / браузер", "browser:" + PairingGate.Hash(browserToken));
        });
        await app.StartAsync();
    }
    bool Trusted(HttpContext c) => c.Request.Cookies.TryGetValue(Cookie, out var token) && token.Length == 64 &&
        sessions.TryGetValue(PairingGate.Hash(token), out var expiry) && expiry > Environment.TickCount64;
    static bool SameOrigin(HttpContext c) => c.Request.Headers.Origin.ToString() == $"http://{c.Request.Host}";
    static bool Local(IPAddress? ip) {
        if (ip is null) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return true;
        var b = ip.GetAddressBytes();
        return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168));
    }
    static IResult Asset(string name, string mime) {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SimDeck.Browser." + name)!;
        using var reader = new StreamReader(stream);
        return Results.Text(reader.ReadToEnd(), mime);
    }
    public async ValueTask DisposeAsync() {
        Revoke();
        if (app is not null) { await app.StopAsync(); await app.DisposeAsync(); }
        revoked.Dispose();
    }
}
