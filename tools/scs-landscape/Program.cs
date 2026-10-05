// SPDX-License-Identifier: GPL-2.0-only
// Separate, read-only extractor. Communicates with Companion through JSON files.
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TruckLib;
using TruckLib.ScsMap;
using TsMap.FileSystem;

if(args.Length is not (5 or 6)) { Console.Error.WriteLine("Usage: SimDeck.ScsLandscape gameDirectory x z span output.json [terrain|satellite]"); return 2; }
try {
    float Number(int i)=>float.Parse(args[i],CultureInfo.InvariantCulture);
    var x=Number(1);var z=Number(2);var span=Number(3);
    if(!float.IsFinite(x)||!float.IsFinite(z)||Math.Abs(x)>1_000_000||Math.Abs(z)>1_000_000||!float.IsFinite(span)||span is <800 or >24000)throw new ArgumentException("Invalid viewport");
    if(!File.Exists(Path.Combine(args[0],"base_map.scs")))throw new ArgumentException("Game not found");
    var style=args.Length==6?args[5]:"terrain";if(style is not ("terrain" or "satellite"))throw new ArgumentException("Invalid map style");
    var output=Extractor.Extract(args[0],x,z,span,style);
    File.WriteAllText(args[4]+".tmp",JsonSerializer.Serialize(output,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    File.Move(args[4]+".tmp",args[4],true);
    Console.WriteLine($"{output.Polygons} surface polygons; {output.ForestPolygons} forest polygons; {output.WaterPolygons} water polygons");return 0;
}catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}

record Area(float[] P,string Kind);
record Landscape(int Version,float X,float Z,float Span,string DayImage,string NightImage,int Polygons,int ForestPolygons,int WaterPolygons,int ForestSchemes,int Skipped,string Style);
sealed class Archives : IFileSystem {
    static string Clean(string p)=>p.Replace('\\','/').Trim('/');
    public char DirectorySeparator=>'/';
    public bool FileExists(string p)=>UberFileSystem.Instance.GetFile(Clean(p)) is not null;
    public bool DirectoryExists(string p)=>UberFileSystem.Instance.GetDirectory(Clean(p)) is not null;
    public IList<string> GetFiles(string p)=>UberFileSystem.Instance.GetDirectory(Clean(p))?.GetFilesByExtension(Clean(p),null)??[];
    public byte[] ReadAllBytes(string p)=>UberFileSystem.Instance.GetFile(Clean(p))?.Entry.Read()??throw new FileNotFoundException(p);
    public string ReadAllText(string p)=>ReadAllText(p,Encoding.UTF8);
    public string ReadAllText(string p,Encoding e)=>e.GetString(ReadAllBytes(p));
    public Stream Open(string p)=>new MemoryStream(ReadAllBytes(p),false);
    public string GetParent(string p){var s=Clean(p);var i=s.LastIndexOf('/');return i<0?null!:s[..i];}
}
static class Extractor {
    static readonly Regex Units=new(@"\b(?:vegetation_data|vegetation_model|material_def|road_look)\s*:\s*([\w.]+)\s*\{([^{}]*)\}",RegexOptions.Singleline|RegexOptions.Compiled);
    static readonly Regex Trees=new(@"tree|forest|pine|spruce|fir_|oak|aspen|birch|redwood|palm|willow|boxelder|maple|cedar|cypress|beech|poplar|eucalyptus",RegexOptions.IgnoreCase|RegexOptions.Compiled);
    static string Text(string s,string key)=>Regex.Match(s,@"\b"+key+@"\s*:\s*""([^""]*)""").Groups[1].Value;
    internal static Landscape Extract(string root,float x,float z,float span,string style) {
        UberFileSystem.Instance.AddSourceDirectory(root);var files=new Archives();
        var defs=new Dictionary<string,string>();
        foreach(var p in files.GetFiles("def/world").Where(p=>p.Contains("vegetation")||p.Contains("terrain_material")||p.Contains("road_look")))
            foreach(Match m in Units.Matches(files.ReadAllText(p)))defs[m.Groups[1].Value]=m.Groups[2].Value;
        var forest=new HashSet<string>();
        foreach(var (name,body) in defs.Where(d=>d.Key.StartsWith("veg."))) {
            var models=Regex.Matches(body,@"model\[\]\s*:\s*([\w.]+)").Select(m=>m.Groups[1].Value);
            if(models.Any(m=>defs.TryGetValue(m,out var b)&&!b.Contains("detail_vegetation: true")&&Trees.IsMatch(Text(b,"sprite_model")+" "+Text(b,"model"))))forest.Add(name[4..]);
        }
        string Material(Token token) {
            if(token==TerrainQuadData.QuadErase)return "erase";
            if(!defs.TryGetValue("terrain_mat."+token,out var body))return "ground";
            var text=(Text(body,"name")+" "+Text(body,"path")).ToLowerInvariant();
            if(text.Contains("water/")||text.Contains("water river")||text.Contains("water ocean")||text.Contains("water lake"))return "water";
            if(text.Contains("asphalt")||text.Contains("concrete")||text.Contains("sidewalk")||text.Contains("paving"))return "urban";
            if(text.Contains("sand")||text.Contains("desert"))return "sand";
            if(text.Contains("rock")||text.Contains("stone")||text.Contains("cliff"))return "rock";
            if(text.Contains("crop")||text.Contains("field")||text.Contains("farmland"))return "field";
            if(text.Contains("grass")||text.Contains("meadow"))return "grass";
            return "ground";
        }
        var sectors=new List<SectorCoordinate>();
        // Neighbour sectors resolve nodes crossing the viewport boundary.
        for(var a=(int)Math.Floor((x-span/2)/4000)-1;a<=(int)Math.Floor((x+span/2)/4000)+1;a++)
            for(var b=(int)Math.Floor((z-span/2)/4000)-1;b<=(int)Math.Floor((z+span/2)/4000)+1;b++)sectors.Add(new(a,b));
        var map=Map.Open(files.DirectoryExists("map/usa")?"map/usa":"map/europe",files,sectors);
        var surfaces=new List<Area>();var trees=new List<Area>();var skipped=0;
        bool Visible(Vector3 p,float margin)=>Math.Abs(p.X-x)<=span/2+margin&&Math.Abs(p.Z-z)<=span/2+margin;
        foreach(var item in map.MapItems.Values.OfType<PolylineItem>()) {
            if(item is not (Terrain or Road)||item.Node.GetType().Name=="UnresolvedNode"||item.ForwardNode.GetType().Name=="UnresolvedNode"||item.Length is <=0 or >10000)continue;
            var mid=(item.Node.Position+item.ForwardNode.Position)/2;
            if(!Visible(mid,item.Length/2+6500))continue;
            var terrain=item as Terrain;var road=item as Road;
            float Width() {
                if(road is null||!defs.TryGetValue("road."+road.RoadType,out var b))return 0;
                var offset=Regex.Match(b,@"\boffset\s*:\s*([\d.]+)").Groups[1].Value;
                return Regex.Matches(b,@"lanes_(?:left|right)\[\]").Count*3.5f+(float.TryParse(offset,CultureInfo.InvariantCulture,out var o)?o:0);
            }
            var edge=Width()/2;
            for(var side=0;side<2;side++) {
                var ground=terrain is not null?(side==0?terrain.Right.Terrain:terrain.Left.Terrain):(side==0?road!.Right.Terrain:road!.Left.Terrain);
                var veg=terrain is not null?(side==0?terrain.Right.Vegetation:terrain.Left.Vegetation):(side==0?road!.Right.Vegetation:road!.Left.Vegetation);
                var q=ground.QuadData;if(q.Rows==0||q.Cols==0||q.Quads.Count!=q.Rows*q.Cols||q.Quads.Count>300000)continue;
                var offsets=q.Offsets.ToDictionary(v=>(int)v.Y*(q.Cols+1)+v.X,v=>item.Node.Position+v.Data);
                var sign=side==0?1f:-1f;
                Vector3 Center(float t)=>item.InterpolateCurve(t).Position+(terrain is null?Vector3.Zero:Vector3.Lerp(terrain.NodeOffset,terrain.ForwardNodeOffset,t));
                // Prefer the exact edited mesh vertices stored by the game. A
                // missing vertex is procedural; no terrain height is invented.
                Vector3 Vertex(int col,int row) {
                    if(offsets.TryGetValue(row*(q.Cols+1)+col,out var p))return p;
                    var t=(float)col/q.Cols;var c=Center(t);var d=Center(Math.Min(1,t+.001f))-Center(Math.Max(0,t-.001f));
                    var n=new Vector3(-d.Z,0,d.X);if(n.LengthSquared()>0)n=Vector3.Normalize(n);
                    float dist;
                    if(terrain is not null&&!terrain.AdaptiveTessellation)dist=ground.Size*row/q.Rows;
                    else {dist=0;for(var r=0;r<row;r++)dist+=EdgeTerrain.GetRowWidthAt(r);dist=Math.Min(dist,ground.Size);}
                    return c+n*sign*(edge+dist);
                }
                var points=new Vector3[q.Rows+1,q.Cols+1];
                for(var r=0;r<=q.Rows;r++)for(var c=0;c<=q.Cols;c++)points[r,c]=Vertex(c,r);
                var treeBands=veg.Where(v=>forest.Contains(v.Name.ToString())&&v.To>v.From).ToArray();
                string Kind(int r,int c,bool tree) {
                    var cell=q.Quads[r*q.Cols+c];
                    var i=(byte)((byte)cell.Opacity>=8?cell.BlendMaterial:cell.MainMaterial);
                    var kind=i<q.BrushMaterials.Count?Material(q.BrushMaterials[i].Name):"ground";
                    if(kind=="erase")return "";
                    if(!tree)return kind;
                    if(kind=="water"||cell.Vegetation.ToString()!="Normal")return "";
                    var p=(points[r,c]+points[r+1,c]+points[r,c+1]+points[r+1,c+1])/4;
                    var center=Center((c+.5f)/q.Cols);var dist=Vector2.Distance(new(p.X,p.Z),new(center.X,center.Z));
                    return treeBands.Any(v=>dist>=v.From&&dist<=v.To)?"forest":"";
                }
                // Merge neighbouring cells with identical classification along
                // each row, retaining their mesh boundary rather than rectangles.
                foreach(var tree in new[]{false,true})for(var r=0;r<q.Rows;r++) {
                    for(var c=0;c<q.Cols;) {
                        var kind=Kind(r,c,tree);if(kind==""){c++;continue;}
                        var end=c+1;while(end<q.Cols&&end-c<48&&Kind(r,end,tree)==kind)end++;
                        var poly=new List<float>();
                        for(var k=c;k<=end;k++){poly.Add(points[r,k].X);poly.Add(points[r,k].Z);}
                        for(var k=end;k>=c;k--){poly.Add(points[r+1,k].X);poly.Add(points[r+1,k].Z);}
                        if(poly.All(v=>float.IsFinite(v)&&Math.Abs(v)<=1_000_000)&&poly.Where((_,i)=>i%2==0).Min()<=x+span/2&&poly.Where((_,i)=>i%2==0).Max()>=x-span/2&&poly.Where((_,i)=>i%2==1).Min()<=z+span/2&&poly.Where((_,i)=>i%2==1).Max()>=z-span/2)
                            (tree?trees:surfaces).Add(new(poly.ToArray(),kind));
                        c=end;
                    }
                }
            }
            if(surfaces.Count+trees.Count>500000)throw new InvalidDataException("Landscape exceeds polygon limit");
        }
        var areas=surfaces.Concat(trees).ToArray();
        return new(2,x,z,span,LandscapeRaster.Render(areas,x,z,span,true,style),LandscapeRaster.Render(areas,x,z,span,false,style),areas.Length,trees.Count,surfaces.Count(a=>a.Kind=="water"),forest.Count,skipped,style);
    }
}
