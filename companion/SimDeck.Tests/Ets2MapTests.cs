using SimDeck.Core;

internal static class Ets2MapTests
{
    public static void Run(Action<bool,string> check)
    {
        var road=new Ets2Road([-2100,-30,2100,30]);
        var map=new Ets2MapIndex(new(1,[road,new([50000,50000,50100,50100])],[new("Town",0,0)]));
        var slice=map.Slice("test",-1,-1,1600);
        check(slice.Roads.Length==1 && slice.Cities.Length==1,"ETS2 viewport finds crossing road once across negative and positive grid cells");
        check(map.Slice("test",50050,50050,1600).Roads.Length==1,"ETS2 viewport excludes distant geometry");
        foreach(var span in new[]{double.NaN,799,16001})
        { try { map.Slice("test",0,0,span);check(false,"invalid span"); } catch(ArgumentException) { check(true,"ETS2 invalid map span rejected"); } }
        try { _=new Ets2MapIndex(new(1,[new([float.NaN,0,1,1])],[]));check(false,"invalid road"); }
        catch(ArgumentException) { check(true,"ETS2 corrupt road cache rejected"); }
    }
}
