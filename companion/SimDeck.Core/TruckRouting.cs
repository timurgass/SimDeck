namespace SimDeck.Core;

public sealed record TruckRouteRequest(double X, double Z, Ets2Poi Destination, Ets2Poi[]? Stops = null,
    string Mode = "fast", bool AvoidFerries = false, bool AvoidHighways = false, double? Heading = null);
public sealed record TruckManeuver(int Point, double AtMetres, string Direction);
public sealed record TruckRouteStop(Ets2Poi Place, double AlongMetres, double OffRouteMetres);
public sealed record TruckRoutePlan(float[] Points, double Metres, double MinutesAtScaleOne,
    TruckManeuver[] Maneuvers, TruckRouteStop[] Nearby, Ets2Poi[] Stops, Ets2Poi Destination,
    string Source = "simdeck", string? Revision = null, double[]? StopMetres = null);

/// <summary>Directed routes through game road nodes and PPD navigation links, never through geometric crossings.</summary>
public sealed partial class TruckRouting
{
    readonly Ets2RoadMap map;
    readonly Ets2NavEdge[] edges;
    readonly Ets2NavNode[] nodes;
    readonly List<int>[] outgoing;
    readonly double[] lengths;
    readonly Dictionary<(int,int),List<int>> cells=[];
    const int Cell=512;
    record Snap(int Edge, int Segment, double T, double X, double Z, double Along, double Lateral);
    public TruckRouting(Ets2RoadMap map)
    {
        this.map=map; nodes=map.Nodes??[];edges=map.Edges??[];
        if(nodes.Length>1_000_000 || edges.Length>2_000_000 || nodes.Any(n=>!Valid(n.X,n.Z)))throw new ArgumentException("Invalid routing nodes");
        outgoing=Enumerable.Range(0,nodes.Length).Select(_=>new List<int>()).ToArray();lengths=new double[edges.Length];
        for(var k=0;k<edges.Length;k++) {
            var e=edges[k];if(e.From<0 || e.To<0 || e.From>=nodes.Length || e.To>=nodes.Length || e.P.Length is <4 or >128 || e.P.Length%2!=0 ||
                e.P.Where((_,i)=>i%2==0).Select((x,i)=>Valid(x,e.P[2*i+1])).Any(v=>!v) || !double.IsFinite(e.Speed) || e.Speed is <1 or >130)
                throw new ArgumentException("Invalid routing edge");
            outgoing[e.From].Add(k);lengths[k]=Length(e.P);
            // Ferry geometries do not provide road anchors across open water.
            if(e.Kind is "ferry" or "link")continue;
            for(var i=2;i<e.P.Length;i+=2) {
                int xa=Tile(Math.Min(e.P[i-2],e.P[i])),xb=Tile(Math.Max(e.P[i-2],e.P[i])),za=Tile(Math.Min(e.P[i-1],e.P[i+1])),zb=Tile(Math.Max(e.P[i-1],e.P[i+1]));
                if((long)(xb-xa+1)*(zb-za+1)>1024)throw new ArgumentException("Routing edge too large");
                for(var x=xa;x<=xb;x++)for(var z=za;z<=zb;z++){if(!cells.TryGetValue((x,z),out var list))cells[(x,z)]=list=[];if(!list.Contains(k))list.Add(k);}
            }
        }
    }
    static bool Valid(double x,double z)=>double.IsFinite(x)&&double.IsFinite(z)&&Math.Abs(x)<=1_000_000&&Math.Abs(z)<=1_000_000;
    static int Tile(double x)=>(int)Math.Floor(x/Cell);
    public Ets2Poi[] Search(string query,double? x=null,double? z=null,string? kind=null)
    {
        query=query.Trim();if(query.Length>100)throw new ArgumentException("Search too long");
        var cities=map.Cities.Select((c,i)=>new Ets2Poi("city:"+i,"city",c.Name,c.X,c.Z));
        return cities.Concat(map.Pois??[]).Where(p=>(kind is null || p.Kind==kind) && (query.Length==0||p.Name.Contains(query,StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p=>x.HasValue&&z.HasValue?double.Hypot(p.X-x.Value,p.Z-z.Value):0).ThenBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).Take(80).ToArray();
    }
    List<Snap> Anchors(double x,double z,double? heading=null)
    {
        var ids=new HashSet<int>();var radius=3;
        for(var a=Tile(x)-radius;a<=Tile(x)+radius;a++)for(var b=Tile(z)-radius;b<=Tile(z)+radius;b++)if(cells.TryGetValue((a,b),out var list))ids.UnionWith(list);
        var candidates=new List<Snap>();
        foreach(var k in ids) {
            var p=edges[k].P;double along=0,best=double.MaxValue;Snap? snap=null;
            for(var i=2;i<p.Length;i+=2){var dx=p[i]-p[i-2];var dz=p[i+1]-p[i-1];var len=double.Hypot(dx,dz);
                var t=len<.001?0:Math.Clamp(((x-p[i-2])*dx+(z-p[i-1])*dz)/(len*len),0,1);var sx=p[i-2]+dx*t;var sz=p[i-1]+dz*t;
                var lateral=double.Hypot(x-sx,z-sz);var score=lateral;
                if(heading.HasValue&&len>.1) {var hx=-Math.Sin(heading.Value*2*Math.PI);var hz=-Math.Cos(heading.Value*2*Math.PI);score+=(1-(dx*hx+dz*hz)/len)*12;}
                if(score<best){best=score;snap=new(k,i,t,sx,sz,along+t*len,lateral);}along+=len;
            }
            if(snap is not null && snap.Lateral<=1500)candidates.Add(snap);
        }
        var near=candidates.OrderBy(v=>v.Lateral).Take(32).ToList();if(near.Count==0)return [];
        var limit=Math.Min(1500,near[0].Lateral+Math.Max(12,near[0].Lateral*.04));
        var anchors=near.Where(v=>v.Lateral<=limit).ToList();
        if(heading.HasValue) {
            var aligned=anchors.Where(v=>HeadingPenalty(v,heading.Value)<32).ToList();
            if(aligned.Count>0)anchors=aligned;
        }
        return anchors.OrderBy(v=>v.Lateral+(heading.HasValue?HeadingPenalty(v,heading.Value):0)).Take(8).ToList();
    }
    double HeadingPenalty(Snap v,double heading){var p=edges[v.Edge].P;var i=v.Segment;var dx=p[i]-p[i-2];var dz=p[i+1]-p[i-1];return (1-(dx*-Math.Sin(heading*2*Math.PI)+dz*-Math.Cos(heading*2*Math.PI))/Math.Max(.001,double.Hypot(dx,dz)))*30;}
    double Cost(int id,TruckRouteRequest r) {var e=edges[id];if(r.AvoidFerries&&e.Kind=="ferry")return double.PositiveInfinity;
        var value=r.Mode=="short"?lengths[id]:lengths[id]/e.Speed;
        if((r.AvoidHighways||r.Mode=="scenic")&&e.Kind=="highway")value*=5;
        return Math.Max(.001,value);}
    public TruckRoutePlan Route(TruckRouteRequest request,CancellationToken cancel=default)
    {
        var stops=request.Stops??[];if(!Valid(request.X,request.Z)||stops.Length>10||request.Mode is not ("fast" or "short" or "scenic") ||
            request.Heading.HasValue&&(!double.IsFinite(request.Heading.Value)||request.Heading<0||request.Heading>1) ||
            stops.Append(request.Destination).Any(p=>p is null||!Valid(p.X,p.Z)||string.IsNullOrEmpty(p.Id)||p.Id.Length>100||string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>150))throw new ArgumentException("Invalid route request");
        if(edges.Length==0)throw new InvalidOperationException("Маршрутная сеть ещё не подготовлена");
        var points=new List<float>();var x=request.X;var z=request.Z;double minutes=0;var stopMetres=new List<double>();
        foreach(var target in stops.Append(request.Destination)){cancel.ThrowIfCancellationRequested();var leg=Leg(x,z,target.X,target.Z,request,points.Count==0?request.Heading:null,cancel);
            if(points.Count>0&&leg.Points.Count>0&&double.Hypot(points[^2]-leg.Points[0],points[^1]-leg.Points[1])>80)
                throw new InvalidOperationException("Нельзя соединить остановки без разворота: выберите доступный въезд");
            points.AddRange(leg.Points);minutes+=leg.Minutes;x=target.X;z=target.Z;stopMetres.Add(Length(points.ToArray()));
        }
        return MakePlan(points,minutes,stops,request.Destination,stopMetres.Take(stops.Length).ToArray());
    }
    TruckRoutePlan MakePlan(List<float> points,double minutes,Ets2Poi[] stops,Ets2Poi destination,double[] stopMetres,string source="simdeck")
    {
        var clean=new List<float>();for(var i=0;i<points.Count;i+=2)if(clean.Count==0||double.Hypot(clean[^2]-points[i],clean[^1]-points[i+1])>.05){clean.Add(points[i]);clean.Add(points[i+1]);}
        if(clean.Count>100_000)throw new InvalidOperationException("Маршрут слишком длинный");var p=clean.ToArray();var total=Length(p);var cumulative=Cumulative(p);
        var maneuvers=new List<TruckManeuver>();double last=-100;
        for(var i=2;i<p.Length-2;i+=2) {
            var before=i/2-1;while(before>0&&cumulative[i/2]-cumulative[before]<18)before--;
            var after=i/2+1;while(after<cumulative.Length-1&&cumulative[after]-cumulative[i/2]<18)after++;
            var ax=p[i]-p[before*2];var az=p[i+1]-p[before*2+1];var bx=p[after*2]-p[i];var bz=p[after*2+1]-p[i+1];
            var angle=Math.Atan2(ax*bz-az*bx,ax*bx+az*bz)*180/Math.PI;
            if(Math.Abs(angle)>=35&&cumulative[i/2]-last>65){maneuvers.Add(new(i/2,cumulative[i/2],Math.Abs(angle)>145?"uturn":angle>0?"right":"left"));last=cumulative[i/2];}
        }
        if(p.Length>0)maneuvers.Add(new(p.Length/2-1,total,"arrive"));
        var nearby=(map.Pois??[]).Where(v=>v.Kind is "fuel" or "rest" or "repair" or "weigh" or "ferry").Select(v=>{var a=Project(p,v.X,v.Z);return new TruckRouteStop(v,a.Along,a.Lateral);})
            .Where(v=>v.OffRouteMetres<=120).OrderBy(v=>v.AlongMetres).Take(100).ToArray();
        return new(p,total,minutes,maneuvers.ToArray(),nearby,stops,destination,source,StopMetres:stopMetres);
    }
    (List<float> Points,double Minutes) Leg(double x,double z,double tx,double tz,TruckRouteRequest req,double? heading,CancellationToken cancel)
    {
        var starts=Anchors(x,z,heading);var targets=Anchors(tx,tz);if(starts.Count==0||targets.Count==0)throw new InvalidOperationException("Рядом с точкой нет доступной дороги");
        var costs=new Dictionary<int,double>();var previous=new Dictionary<int,(int Node,int Edge)>();var roots=new Dictionary<int,Snap>();var queue=new PriorityQueue<int,double>();
        foreach(var a in starts){var c=Cost(a.Edge,req)*(lengths[a.Edge]-a.Along)/Math.Max(.001,lengths[a.Edge])+a.Lateral/70+(heading.HasValue?HeadingPenalty(a,heading.Value)/70:0);var n=edges[a.Edge].To;if(!costs.TryGetValue(n,out var old)||c<old){costs[n]=c;roots[n]=a;queue.Enqueue(n,c);}}
        double best=double.PositiveInfinity;Snap? startBest=null,endBest=null;int endNode=-1;bool direct=false;
        foreach(var a in starts)foreach(var b in targets)if(a.Edge==b.Edge&&b.Along>=a.Along){var c=Cost(a.Edge,req)*(b.Along-a.Along)/Math.Max(.001,lengths[a.Edge])+(a.Lateral+b.Lateral)/70;
            if(c<best){best=c;startBest=a;endBest=b;direct=true;}}
        var visited=0;
        while(queue.TryDequeue(out var n,out var priority)) {
            if(++visited%512==0)cancel.ThrowIfCancellationRequested();if(visited>4_000_000)throw new InvalidOperationException("Маршрутная сеть слишком сложна");
            if(!costs.TryGetValue(n,out var known)||priority>known)continue;if(priority>=best)break;
            foreach(var b in targets.Where(b=>edges[b.Edge].From==n)){var c=priority+Cost(b.Edge,req)*b.Along/Math.Max(.001,lengths[b.Edge])+b.Lateral/70;if(c<best){best=c;endBest=b;endNode=n;direct=false;}}
            foreach(var id in outgoing[n]){var edge=edges[id];var next=priority+Cost(id,req);if(!costs.TryGetValue(edge.To,out var old)||next<old){costs[edge.To]=next;previous[edge.To]=(n,id);roots.Remove(edge.To);queue.Enqueue(edge.To,next);}}
        }
        if(endBest is null||!double.IsFinite(best))throw new InvalidOperationException("Доступный путь не найден. Попробуйте другую точку или разрешите паромы");
        var result=new List<float>();double minutes=0;
        if(direct){result=Section(edges[startBest!.Edge].P,startBest.Along,endBest.Along);minutes=(endBest.Along-startBest.Along)/1000/edges[startBest.Edge].Speed*60;}
        else {var chain=new List<int>();var n=endNode;while(previous.TryGetValue(n,out var link)){chain.Add(link.Edge);n=link.Node;if(chain.Count>edges.Length)throw new InvalidOperationException("Invalid route chain");}
            var a=roots[n];chain.Reverse();result.AddRange(Section(edges[a.Edge].P,a.Along,lengths[a.Edge]));minutes+=(lengths[a.Edge]-a.Along)/1000/edges[a.Edge].Speed*60;
            foreach(var id in chain){result.AddRange(edges[id].P);minutes+=lengths[id]/1000/edges[id].Speed*60;}
            result.AddRange(Section(edges[endBest.Edge].P,0,endBest.Along));minutes+=endBest.Along/1000/edges[endBest.Edge].Speed*60;
        }
        return(result,minutes);
    }
    static List<float> Section(float[] p,double from,double to){var result=new List<float>();double along=0;for(var i=2;i<p.Length;i+=2){var dx=p[i]-p[i-2];var dz=p[i+1]-p[i-1];var len=double.Hypot(dx,dz);
        if(along+len>=from&&along<=to){double a=len<.001?0:Math.Clamp((from-along)/len,0,1),b=len<.001?1:Math.Clamp((to-along)/len,0,1);result.Add((float)(p[i-2]+dx*a));result.Add((float)(p[i-1]+dz*a));result.Add((float)(p[i-2]+dx*b));result.Add((float)(p[i-1]+dz*b));}along+=len;}return result;}
    public static double Length(float[] p){double total=0;for(var i=2;i<p.Length;i+=2)total+=double.Hypot(p[i]-p[i-2],p[i+1]-p[i-1]);return total;}
    static double[] Cumulative(float[] p){var a=new double[p.Length/2];for(var i=1;i<a.Length;i++)a[i]=a[i-1]+double.Hypot(p[i*2]-p[i*2-2],p[i*2+1]-p[i*2-1]);return a;}
    public static (double Along,double Lateral) Project(float[] p,double x,double z){double along=0,best=double.PositiveInfinity,at=0;for(var i=2;i<p.Length;i+=2){var dx=p[i]-p[i-2];var dz=p[i+1]-p[i-1];var len=double.Hypot(dx,dz);var t=len<.001?0:Math.Clamp(((x-p[i-2])*dx+(z-p[i-1])*dz)/(len*len),0,1);var d=double.Hypot(x-p[i-2]-t*dx,z-p[i-1]-t*dz);if(d<best){best=d;at=along+t*len;}along+=len;}return(at,best);}
}
