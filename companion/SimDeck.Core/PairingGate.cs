using System.Security.Cryptography;

namespace SimDeck.Core;

public sealed class PairingGate
{
    readonly Func<long> clock;
    readonly object gate = new();
    string code = "";
    long expires;
    int attempts;
    public PairingGate(Func<long>? clock = null) => this.clock = clock ?? (() => Environment.TickCount64);
    public string Open()
    {
        lock (gate)
        { code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(); expires = clock() + 120000; attempts = 0; return code; }
    }
    public bool IsOpen { get { lock (gate) return code.Length > 0 && clock() < expires && attempts < 5; } }
    public bool Consume(string candidate)
    {
        lock (gate)
        {
            if (!IsOpen) return false;
            attempts++;
            if (candidate != code) return false;
            code = ""; return true;
        }
    }
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
