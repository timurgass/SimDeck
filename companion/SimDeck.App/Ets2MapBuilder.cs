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
            for(var i=0;i<prefab.MapPoints.Count;i++)
            {
                var a=prefab.MapPoints[i]; if(a.LaneCount==-1 || a.Hidden) continue;
                foreach(var j in a.Neighbours)
                {
                    if(j<=i || j>=prefab.MapPoints.Count) continue;
                    var b=prefab.MapPoints[j]; if(b.LaneCount==-1 || b.Hidden) continue;
                    var pa=World(a.X,a.Z); var pb=World(b.X,b.Z);
                    roads.Add(new([pa.x,pa.z,pb.x,pb.z],Math.Clamp(Math.Max(a.LaneCount,b.LaneCount)*4.5f,6,60)));
                }
            }
        }
        if(roads.Count<100) throw new InvalidDataException("В файлах игры не найдены дороги.");
        var cities=mapper.Cities.Select(c=>new Ets2City(c.City.Name,c.X,c.Z)).ToArray();
        var map=new Ets2RoadMap(1,roads.ToArray(),cities);
        _=new Ets2MapIndex(map); // Validate before replacing a previously usable cache.
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
