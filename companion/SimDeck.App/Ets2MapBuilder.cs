using System.IO;
using System.Text.Json;
using SimDeck.Core;
using TsMap;
using TsMap.Common;

namespace SimDeck.App;

internal static class Ets2MapBuilder
{
    public static void Build(string gameDirectory, string destination)
    {
        var mapper = new TsMapper(gameDirectory, []);
        mapper.Parse();
        var guards = mapper.GetDlcGuardsForCurrentGame().Where(g=>g.Index==0 || HasDlc(gameDirectory,g.Name)).Select(g=>g.Index).ToHashSet();
        var roads = new List<Ets2Road>();
        var pois = new List<Ets2Poi>();var nodes = new List<Ets2NavNode>();var edges = new List<Ets2NavEdge>();
        var nodeIds = new Dictionary<string,int>();
        var boundaries=new Dictionary<ulong,List<(ulong Prefab,int[] Inputs,int[] Outputs)>>();
        int Node(string id,float x,float z,string? uid=null) { if(nodeIds.TryGetValue(id,out var value))return value;nodeIds[id]=nodes.Count;nodes.Add(new(x,z,uid));return nodes.Count-1; }
        int RoadNode(ulong uid,bool incoming,float x,float z)=>Node("road:"+uid+":"+incoming,x,z,uid.ToString());
        void Edge(int a,int b,float[] p,string kind="road",double speed=70) { if(p.All(float.IsFinite))edges.Add(new(a,b,p,kind,speed)); }
        void Link(int a,int b) { if(a!=b) {
            var p=new float[]{nodes[a].X,nodes[a].Z,nodes[b].X,nodes[b].Z};
            Edge(a,b,p,"link",30);
            if(double.Hypot(p[2]-p[0],p[3]-p[1]) is >0.2 and <80)roads.Add(new(p,4));
        } }
        foreach(var r in mapper.Roads.Where(r=>!r.IsSecret && guards.Contains(r.DlcGuard)))
        {
            var a=r.GetStartNode(); var b=r.GetEndNode(); if(a is null || b is null) continue;
            var radius=Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Z-b.Z,2));
            var p=new float[24];
            for(var i=0;i<12;i++)
            {
                var t=i/11f;
                p[i*2]=(float)TsRoadLook.Hermite(t,a.X,b.X,Math.Sin(a.Rotation)*radius,Math.Sin(b.Rotation)*radius);
                p[i*2+1]=(float)TsRoadLook.Hermite(t,a.Z,b.Z,-Math.Cos(a.Rotation)*radius,-Math.Cos(b.Rotation)*radius);
            }
            roads.Add(new(p,Math.Clamp(r.RoadLook.GetWidth(),4,80)));
            var highway=r.RoadLook.LanesLeft.Count>=2 && r.RoadLook.LanesRight.Count>=2 ||
                r.RoadLook.LanesLeft.Concat(r.RoadLook.LanesRight).Any(l=>l.Contains("highway",StringComparison.OrdinalIgnoreCase) || l.Contains("freeway",StringComparison.OrdinalIgnoreCase));
            var kind=highway?"highway":"road";var speed=highway?85:60;
            if(r.RoadLook.LanesRight.Count>0)Edge(RoadNode(a.Uid,false,a.X,a.Z),RoadNode(b.Uid,true,b.X,b.Z),p,kind,speed);
            if(r.RoadLook.LanesLeft.Count>0) {
                var reverse=Enumerable.Range(0,p.Length/2).Reverse().SelectMany(i=>new[]{p[2*i],p[2*i+1]}).ToArray();
                Edge(RoadNode(b.Uid,false,b.X,b.Z),RoadNode(a.Uid,true,a.X,a.Z),reverse,kind,speed);
            }
        }
        foreach(var r in mapper.Prefabs.Where(r=>!r.IsSecret && guards.Contains(r.DlcGuard)))
        {
            var prefab=r.Prefab;
            if(r.Nodes.Count==0 || prefab?.PrefabNodes is null || r.Origin>=prefab.PrefabNodes.Count) continue;
            var origin=mapper.GetNodeByUid(r.Nodes[0]); if(origin is null) continue;
            var n=prefab.PrefabNodes[r.Origin];
            var angle=origin.Rotation-Math.PI-Math.Atan2(n.RotZ,n.RotX)+Math.PI/2;
            (float x,float z) World(float x,float z) => ((float)(origin.X+(x-n.X)*Math.Cos(angle)-(z-n.Z)*Math.Sin(angle)),
                (float)(origin.Z+(x-n.X)*Math.Sin(angle)+(z-n.Z)*Math.Cos(angle)));
            var curves=prefab.NavCurves??[];var starts=new int[curves.Count];var ends=new int[curves.Count];
            for(var i=0;i<curves.Count;i++) {
                var c=curves[i];var a=World(c.X1,c.Z1);var b=World(c.X2,c.Z2);
                starts[i]=Node($"prefab:{r.Uid}:{i}:start",a.x,a.z);ends[i]=Node($"prefab:{r.Uid}:{i}:end",b.x,b.z);
                var length=Math.Clamp(c.Length,1,3000);var p=new float[24];
                for(var k=0;k<12;k++) {var t=k/11f;
                    var local=World((float)TsRoadLook.Hermite(t,c.X1,c.X2,c.StartTangent[0]*length,c.EndTangent[0]*length),
                        (float)TsRoadLook.Hermite(t,c.Z1,c.Z2,c.StartTangent[1]*length,c.EndTangent[1]*length));p[2*k]=local.x;p[2*k+1]=local.z;}
                Edge(starts[i],ends[i],p,"prefab",35);
                roads.Add(new(p,6));
            }
            for(var i=0;i<curves.Count;i++)foreach(var next in curves[i].Next)if(next<curves.Count)Link(ends[i],starts[next]);
            for(var i=0;i<Math.Min(r.Nodes.Count,prefab.PrefabNodes.Count);i++) {
                // Map node list is rotated: entry zero is the selected PPD origin.
                var gameNode=mapper.GetNodeByUid(r.Nodes[i]);if(gameNode is null)continue;var pn=prefab.PrefabNodes[(i+r.Origin)%prefab.PrefabNodes.Count];
                foreach(var curve in pn.InputCurves??[])if(curve>=0&&curve<curves.Count)Link(RoadNode(gameNode.Uid,true,gameNode.X,gameNode.Z),starts[curve]);
                foreach(var curve in pn.OutputCurves??[])if(curve>=0&&curve<curves.Count)Link(ends[curve],RoadNode(gameNode.Uid,false,gameNode.X,gameNode.Z));
                if(!boundaries.TryGetValue(gameNode.Uid,out var boundary))boundaries[gameNode.Uid]=boundary=[];
                boundary.Add((r.Uid,(pn.InputCurves??[]).Where(v=>v>=0&&v<curves.Count).Select(v=>starts[v]).ToArray(),
                    (pn.OutputCurves??[]).Where(v=>v>=0&&v<curves.Count).Select(v=>ends[v]).ToArray()));
            }
            void Poi(string id,string kind,string name,float x,float z) {var p=World(x,z);pois.Add(new($"{r.Uid}:{id}",kind,name,p.x,p.z));}
            for(var i=0;i<prefab.SpawnPoints.Count;i++) {var p=prefab.SpawnPoints[i];
                var type=p.Type switch { TsSpawnPointType.GasPos=>("fuel","Заправка"),TsSpawnPointType.ServicePos=>("repair","Сервис"),
                    TsSpawnPointType.WeightStationPos=>("weigh","Весовая станция"),TsSpawnPointType.TruckDealerPos=>("dealer","Дилер"),
                    TsSpawnPointType.RecruitmentPos=>("recruitment","Агентство"),TsSpawnPointType.BuyPos=>("garage","Гараж"),_=>default };
                if(type.Item1 is not null)Poi("spawn:"+i,type.Item1,type.Item2,p.X,p.Z);
            }
            foreach(var p in prefab.TriggerPoints.GroupBy(t=>t.TriggerId).Select(g=>g.First()))if(p.TriggerActionToken==ScsToken.StringToToken("hud_parking"))
                Poi("rest:"+p.TriggerId,"rest","Стоянка / отдых",p.X,p.Z);
            (float x,float z) MapWorld(TsMapPoint point) {
                // Control map points are tied to the real road control node, not an
                // approximate diagram coordinate inside the PPD map drawing.
                var control=point.ControlNodeIndex;
                if(control>=0 && control<prefab.PrefabNodes.Count) {
                    var gameIndex=(control-r.Origin+prefab.PrefabNodes.Count)%prefab.PrefabNodes.Count;
                    if(gameIndex<r.Nodes.Count && mapper.GetNodeByUid(r.Nodes[gameIndex]) is {} gameNode)
                        return (gameNode.X,gameNode.Z);
                }
                return World(point.X,point.Z);
            }
            float MapWidth(TsMapPoint point) {
                var lanes=point.LaneCount;
                if(lanes==-2 && point.ControlNodeIndex>=0 && point.ControlNodeIndex<prefab.PrefabNodes.Count)
                    lanes=prefab.PrefabNodes[point.ControlNodeIndex].LaneCount;
                return Math.Clamp(Math.Max(1,lanes)*4.5f+point.LaneOffset,4,80);
            }
            for(var i=0;i<prefab.MapPoints.Count;i++)
            {
                var a=prefab.MapPoints[i]; if(a.LaneCount==-1 || a.Hidden && prefab.MapPoints.Count>prefab.PrefabNodes.Count+1) continue;
                foreach(var j in a.Neighbours)
                {
                    if(j<=i || j>=prefab.MapPoints.Count) continue;
                    var b=prefab.MapPoints[j]; if(b.LaneCount==-1 || b.Hidden && prefab.MapPoints.Count>prefab.PrefabNodes.Count+1) continue;
                    var pa=MapWorld(a); var pb=MapWorld(b);
                    roads.Add(new([pa.x,pa.z,pb.x,pb.z],Math.Max(MapWidth(a),MapWidth(b))));
                }
            }
        }
        if(roads.Count<100) throw new InvalidDataException("В файлах игры не найдены дороги.");
        foreach(var boundary in boundaries.Values)foreach(var a in boundary)foreach(var b in boundary)
            if(a.Prefab!=b.Prefab)foreach(var exit in a.Outputs)foreach(var enter in b.Inputs)Link(exit,enter);
        // A road may continue through several road items without a prefab. Only join
        // identical game node IDs outside prefab control nodes, never nearby geometry.
        var prefabNodes=mapper.Prefabs.SelectMany(p=>p.Nodes).ToHashSet();
        foreach(var pair in nodeIds.ToArray()) {
            var parts=pair.Key.Split(':');
            if(parts.Length==3 && parts[0]=="road" && parts[2]=="True" &&
                !prefabNodes.Contains(ulong.Parse(parts[1])) && nodeIds.TryGetValue($"road:{parts[1]}:False",out var exit))Link(pair.Value,exit);
        }
        var cities=mapper.Cities.Select(c=>new Ets2City(c.City.Name,c.X,c.Z)).ToArray();
        foreach(var f in mapper.FerryConnections.Where(f=>guards.Contains(f.DlcGuard))) {
            pois.Add(new("ferry:"+f.FerryPortId,"ferry","Паром / поезд",f.X,f.Z));
            foreach(var c in mapper.LookupFerryConnection(f.FerryPortId)) {
                var start=nodeIds.Where(v=>v.Key.StartsWith("road:")&&v.Key.EndsWith(":True")).Select(v=>(n:nodes[v.Value],i:v.Value)).OrderBy(v=>double.Hypot(v.n.X-c.StartPortLocation.X,v.n.Z-c.StartPortLocation.Y)).FirstOrDefault();
                var end=nodeIds.Where(v=>v.Key.StartsWith("road:")&&v.Key.EndsWith(":False")).Select(v=>(n:nodes[v.Value],i:v.Value)).OrderBy(v=>double.Hypot(v.n.X-c.EndPortLocation.X,v.n.Z-c.EndPortLocation.Y)).FirstOrDefault();
                if(start.n is not null&&end.n is not null &&
                    double.Hypot(start.n.X-c.StartPortLocation.X,start.n.Z-c.StartPortLocation.Y)<1500 &&
                    double.Hypot(end.n.X-c.EndPortLocation.X,end.n.Z-c.EndPortLocation.Y)<1500)Edge(start.i,end.i,[start.n.X,start.n.Z,end.n.X,end.n.Z],"ferry",35);
            }
        }
        var areas=mapper.MapAreas.Where(a=>!a.IsSecret&&!a.Hidden&&guards.Contains(a.DlcGuard)).Select(a=>new Ets2MapArea(
            a.NodeUids.Select(mapper.GetNodeByUid).Where(n=>n is not null).SelectMany(n=>new[]{n.X,n.Z}).ToArray(),(int)(a.ColorIndex&3)))
            .Where(a=>a.P.Length is >=6 and <=1024).ToArray();
        var map=new Ets2RoadMap(2,roads.ToArray(),cities,pois.DistinctBy(p=>(p.Kind,(int)(p.X/4),(int)(p.Z/4))).ToArray(),nodes.ToArray(),edges.ToArray(),areas);
        _=new Ets2MapIndex(map); // Validate before replacing a previously usable cache.
        _=new TruckRouting(map);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var tmp=destination+".tmp";
        File.WriteAllBytes(tmp,JsonSerializer.SerializeToUtf8Bytes(map,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        File.Move(tmp,destination,true);
    }
    static bool HasDlc(string path,string name)
    {
        var aliases=new Dictionary<string,string> { ["dlc_blkw"]="dlc_balkan_w",["dlc_blke"]="dlc_balkan_e" };
        return name.Split("_and_").Select((p,i)=>i==0?p:"dlc_"+p).All(p=>File.Exists(Path.Combine(path,(aliases.GetValueOrDefault(p)??p)+".scs")));
    }
}
