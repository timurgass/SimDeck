namespace SimDeck.Core;

// Locally extracted surfaces, never shipped as game assets with SimDeck.
public sealed record TruckLandscape(int Version,double X,double Z,double Span,string DayImage,string NightImage,
    int Polygons,int ForestPolygons,int WaterPolygons,int ForestSchemes,int Skipped,string Style="terrain")
{
    public bool IsValid()=>((Version==1&&Style=="terrain")||(Version==2&&Style is "terrain" or "satellite"))&&double.IsFinite(X)&&double.IsFinite(Z)&&Math.Abs(X)<=1_000_000&&Math.Abs(Z)<=1_000_000&&
        double.IsFinite(Span)&&Span is >=800 and <=24000&&Polygons is >=0 and <=500000&&ForestPolygons>=0&&WaterPolygons>=0&&
        ForestPolygons<=Polygons&&WaterPolygons<=Polygons&&Image(DayImage)&&Image(NightImage);
    static bool Image(string s)=>s is {Length:>50 and <4_000_000}&&s.StartsWith("data:image/png;base64,iVBORw0KGgo",StringComparison.Ordinal);
    public bool Covers(double x,double z,double span)=>Math.Abs(x-X)+span/2<=Span/2&&Math.Abs(z-Z)+span/2<=Span/2;
}
