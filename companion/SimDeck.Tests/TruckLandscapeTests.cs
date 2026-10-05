using SimDeck.Core;

internal static class TruckLandscapeTests
{
    public static void Run(Action<bool,string> check)
    {
        const string png="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==";
        var region=new TruckLandscape(1,-116000,-8000,10000,png,png,100,20,5,12,0);
        check(region.IsValid(),"Landscape accepts bounded local PNG surfaces");
        check(region.Covers(-115000,-8000,8000)&&!region.Covers(-114999,-8000,8000),"Landscape refreshes when viewport crosses the region edge");
        check(!region.Covers(-116000,-8000,12000),"Landscape refreshes after zooming beyond cached coverage");
        check(!(region with{X=double.NaN}).IsValid()&&!(region with{Span=double.PositiveInfinity}).IsValid(),"Landscape rejects nonfinite cache geometry");
        check(!(region with{ForestPolygons=101}).IsValid()&&!(region with{Polygons=500001}).IsValid(),"Landscape bounds extracted geometry counts");
        check(!(region with{DayImage="https://example.invalid/private.png"}).IsValid(),"Landscape never permits remote image URLs through the local layer");
        check(!(region with{NightImage="data:image/png;base64,"+new string('a',4000000)}).IsValid(),"Landscape bounds image transfer size");
        check((region with{Version=2,Style="satellite"}).IsValid()&&(region with{Version=2}).IsValid(),"Landscape accepts both locally rendered styles");
        check(!(region with{Version=1,Style="satellite"}).IsValid()&&!(region with{Version=2,Style="../satellite"}).IsValid()&&!(region with{Version=3}).IsValid(),"Landscape rejects unsupported styles and format versions");
    }
}
