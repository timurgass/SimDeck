namespace SimDeck.Core;

// Geometry is extracted locally from the user's game, never distributed with SimDeck.
public sealed record Ets2Road(float[] P, float W = 10);
public sealed record Ets2City(string Name, float X, float Z);
public sealed record Ets2Poi(string Id, string Kind, string Name, float X, float Z);
public sealed record Ets2NavNode(float X, float Z, string? GameUid = null);
public sealed record Ets2NavEdge(int From, int To, float[] P, string Kind = "road", double Speed = 70);
public sealed record Ets2MapArea(float[] P, int Color = 0);
public sealed record Ets2RoadMap(int Version, Ets2Road[] Roads, Ets2City[] Cities,
    Ets2Poi[]? Pois = null, Ets2NavNode[]? Nodes = null, Ets2NavEdge[]? Edges = null, Ets2MapArea[]? Areas = null);
public sealed record Ets2MapSlice(string Revision, double X, double Z, double Span,
    Ets2Road[] Roads, Ets2City[] Cities, bool Truncated = false, Ets2Poi[]? Pois = null, Ets2MapArea[]? Areas = null);

public sealed class Ets2MapIndex
{
    const int Cell = 2048;
    readonly Ets2RoadMap map;
    readonly (float X0,float X1,float Z0,float Z1)[] bounds;
    readonly Dictionary<(int, int), List<int>> cells = [];
    public Ets2MapIndex(Ets2RoadMap map)
    {
        if (map.Version is not (1 or 2) || map.Roads.Length > 500_000 || map.Cities.Length > 5000 || (map.Pois?.Length ?? 0) > 100_000)
            throw new ArgumentException("Unsupported road map");
        this.map = map;bounds=new (float,float,float,float)[map.Roads.Length];
        if((map.Areas?.Length??0)>100000 || map.Areas?.Any(a=>a.P.Length is <6 or >1024 || a.P.Length%2!=0 || a.P.Any(v=>!float.IsFinite(v)||Math.Abs(v)>1000000))==true)
            throw new ArgumentException("Invalid map areas");
        if(map.Pois?.Any(p=>p is null || string.IsNullOrEmpty(p.Id) || p.Id.Length>100 || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length>150 ||
            !float.IsFinite(p.X) || !float.IsFinite(p.Z) || Math.Abs(p.X)>1_000_000 || Math.Abs(p.Z)>1_000_000) == true)
            throw new ArgumentException("Invalid map points");
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
            bounds[i]=(xs.Min(),xs.Max(),zs.Min(),zs.Max());
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
        var visible=ids.Where(i=>bounds[i].X0<=x+span/2 && bounds[i].X1>=x-span/2 && bounds[i].Z0<=z+span/2 && bounds[i].Z1>=z-span/2).ToArray();
        // Cell candidates include large areas outside the requested viewport. Clip
        // those first, then keep central geometry when the safety cap is reached.
        double Distance(int i) {var b=bounds[i];return Math.Pow(Math.Max(Math.Max(b.X0-x,x-b.X1),0),2)+Math.Pow(Math.Max(Math.Max(b.Z0-z,z-b.Z1),0),2);}
        var selected=visible.OrderBy(Distance).ThenBy(i=>i).Take(20000).Select(i=>map.Roads[i]).ToArray();
        return new(revision,x,z,span,selected,map.Cities.Where(c=>Math.Abs(c.X-x)<=span/2 && Math.Abs(c.Z-z)<=span/2).ToArray(),visible.Length>20000,
            map.Pois?.Where(p=>Math.Abs(p.X-x)<=span/2 && Math.Abs(p.Z-z)<=span/2).Take(2000).ToArray(),
            map.Areas?.Where(a=>a.P.Where((_,i)=>i%2==0).Min()<=x+span/2 && a.P.Where((_,i)=>i%2==0).Max()>=x-span/2 &&
                a.P.Where((_,i)=>i%2==1).Min()<=z+span/2 && a.P.Where((_,i)=>i%2==1).Max()>=z-span/2).Take(3000).ToArray());
    }
}
