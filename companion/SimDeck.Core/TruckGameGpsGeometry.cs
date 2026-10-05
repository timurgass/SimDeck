namespace SimDeck.Core;

public sealed partial class TruckRouting
{
    Dictionary<string,int[]>? gameNodeIds;
    public TruckRoutePlan GameRoute(TruckGpsNode[] route,CancellationToken cancel=default)=>GameRoute(route,[],cancel);
    public TruckRoutePlan GameRoute(TruckGpsNode[] route,TruckGpsWaypoint[] waypoints,CancellationToken cancel=default)
    {
        if(route.Length is <2 or >6000||route.Any(n=>n is null||!ulong.TryParse(n.Uid,out var uid)||uid==0||!Valid(n.X,n.Z)||!float.IsFinite(n.DistanceToEnd)||n.DistanceToEnd<0||!float.IsFinite(n.SecondsToEnd)||n.SecondsToEnd<0))
            throw new ArgumentException("Invalid game route");
        if(waypoints.Length>64||waypoints.Any(p=>p is null||!ulong.TryParse(p.Uid,out var uid)||uid==0||!Valid(p.X,p.Z)))
            throw new ArgumentException("Invalid game waypoints");
        var ids=gameNodeIds??=nodes.Select((n,i)=>(n.GameUid,i)).Where(v=>v.GameUid is not null).GroupBy(v=>v.GameUid!).ToDictionary(g=>g.Key,g=>g.Select(v=>v.i).ToArray());
        var points=new List<float>();
        for(var i=1;i<route.Length;i++)
        {
            cancel.ThrowIfCancellationRequested();var a=route[i-1];var b=route[i];if(a.Uid==b.Uid)continue;
            if(!ids.TryGetValue(a.Uid,out var starts)||!ids.TryGetValue(b.Uid,out var targets))
                throw new InvalidOperationException("Узел GPS отсутствует в карте: проверьте DLC и моды карты");
            if(starts.All(id=>double.Hypot(nodes[id].X-a.X,nodes[id].Z-a.Z)>4)||targets.All(id=>double.Hypot(nodes[id].X-b.X,nodes[id].Z-b.Z)>4))
                throw new InvalidOperationException("Координаты GPS не совпадают с установленной картой");
            var costs=new Dictionary<int,double>();var prev=new Dictionary<int,(int Node,int Edge)>();var queue=new PriorityQueue<int,double>();
            foreach(var id in starts){costs[id]=0;queue.Enqueue(id,0);}var targetSet=targets.ToHashSet();var end=-1;var visited=0;
            while(queue.TryDequeue(out var id,out var cost))
            {
                if(++visited%128==0)cancel.ThrowIfCancellationRequested();if(visited>20000)break;
                if(cost>costs[id])continue;if(targetSet.Contains(id)){end=id;break;}
                foreach(var edgeId in outgoing[id])
                {
                    var to=edges[edgeId].To;
                    // Consecutive physical GPS nodes fix which road the game chose.
                    // Only interpolate the road/prefab curves between those nodes.
                    if(nodes[to].GameUid is { } uid&&uid!=a.Uid&&uid!=b.Uid)continue;
                    var next=cost+Math.Max(.001,lengths[edgeId]);
                    if(!costs.TryGetValue(to,out var old)||next<old){costs[to]=next;prev[to]=(id,edgeId);queue.Enqueue(to,next);}
                }
            }
            if(end<0)throw new InvalidOperationException("Не удалось соединить выбранные игрой GPS-узлы по карте");
            var chain=new List<int>();while(prev.TryGetValue(end,out var link)){chain.Add(link.Edge);end=link.Node;if(chain.Count>edges.Length)throw new InvalidOperationException("Invalid GPS chain");}
            chain.Reverse();foreach(var id in chain)points.AddRange(edges[id].P);
        }
        if(points.Count<4)throw new InvalidOperationException("В GPS недостаточно узлов для линии пути");
        var target=waypoints.Length>0?waypoints[^1]:new TruckGpsWaypoint(route[^1].Uid,route[^1].X,route[^1].Z);
        var stops=waypoints.SkipLast(1).Select((p,i)=>new Ets2Poi($"gps:waypoint:{i}:{p.Uid}","custom",$"GPS-точка {i+1}",p.X,p.Z)).ToArray();
        var plan=MakePlan(points,0,stops,new("gps:"+target.Uid,"custom","Пункт назначения GPS",target.X,target.Z),[],"game-gps");
        return plan with{StopMetres=stops.Select(p=>Project(plan.Points,p.X,p.Z).Along).ToArray()};
    }
}
