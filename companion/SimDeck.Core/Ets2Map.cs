namespace SimDeck.Core;

// Geometry is extracted locally from the user's game, never distributed with SimDeck.
public sealed record Ets2Road(float[] P, float W = 10);
public sealed record Ets2City(string Name, float X, float Z);
public sealed record Ets2RoadMap(int Version, Ets2Road[] Roads, Ets2City[] Cities);
public sealed record Ets2MapSlice(string Revision, double X, double Z, double Span,
    Ets2Road[] Roads, Ets2City[] Cities, bool Truncated = false);

public sealed class Ets2MapIndex
{
    const int Cell = 2048;
    readonly Ets2RoadMap map;
    readonly Dictionary<(int, int), List<int>> cells = [];
    public Ets2MapIndex(Ets2RoadMap map)
    {
        if (map.Version != 1 || map.Roads.Length > 500_000 || map.Cities.Length > 5000)
            throw new ArgumentException("Unsupported road map");
        this.map = map;
        if(map.Cities.Any(c=>string.IsNullOrWhiteSpace(c.Name) || c.Name.Length>100 || !float.IsFinite(c.X) || !float.IsFinite(c.Z)))
            throw new ArgumentException("Invalid city geometry");
        for (var i = 0; i < map.Roads.Length; i++)
        {
            var p = map.Roads[i].P;
            if(!float.IsFinite(map.Roads[i].W) || map.Roads[i].W is < 1 or > 100) throw new ArgumentException("Invalid road width");
            if (p.Length is < 4 or > 128 || p.Length % 2 != 0 || p.Any(v => !float.IsFinite(v) || Math.Abs(v) > 1_000_000))
                throw new ArgumentException("Invalid road geometry");
            var xs = p.Where((_, n) => n % 2 == 0).ToArray();
            var zs = p.Where((_, n) => n % 2 == 1).ToArray();
            var x0 = Tile(xs.Min()); var x1 = Tile(xs.Max()); var z0 = Tile(zs.Min()); var z1 = Tile(zs.Max());
            if ((long)(x1-x0+1)*(z1-z0+1) > 1024) throw new ArgumentException("Road bounds too large");
            for(var x=x0;x<=x1;x++) for(var z=z0;z<=z1;z++)
            { if(!cells.TryGetValue((x,z),out var list)) cells[(x,z)]=list=[]; list.Add(i); }
        }
    }
    static int Tile(double v) => (int)Math.Floor(v / Cell);
    public Ets2MapSlice Slice(string revision, double x, double z, double span)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z) || Math.Abs(x)>1_000_000 || Math.Abs(z)>1_000_000 ||
            !double.IsFinite(span) || span is < 800 or > 16000) throw new ArgumentException("Invalid map viewport");
        var ids=new HashSet<int>();
        for(var a=Tile(x-span/2);a<=Tile(x+span/2);a++) for(var b=Tile(z-span/2);b<=Tile(z+span/2);b++)
            if(cells.TryGetValue((a,b),out var list)) ids.UnionWith(list);
        var selected=ids.Order().Take(8000).Select(i=>map.Roads[i]).ToArray();
        return new(revision,x,z,span,selected,map.Cities.Where(c=>Math.Abs(c.X-x)<=span/2 && Math.Abs(c.Z-z)<=span/2).ToArray(),ids.Count>8000);
    }
}
