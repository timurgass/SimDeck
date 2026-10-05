using SimDeck.App;
using SimDeck.Core;

internal static class TruckGameGpsTests
{
    public static void Run(Action<bool,string> check)
    {
        Ets2NavNode[] nodes=[new(0,0,"1"),new(500,0,"2"),new(500,500,"3"),new(0,500,"4")];
        Ets2NavEdge[] edges=[new(0,1,[0,0,500,0]),new(1,2,[500,0,500,500]),new(0,3,[0,0,0,500]),new(3,2,[0,500,500,500])];
        var r=new TruckRouting(new(2,[],[],[],nodes,edges));
        TruckGpsNode N(int i)=>new((i+1).ToString(),nodes[i].X,nodes[i].Z,1000,60);
        var a=r.GameRoute([N(0),N(1),N(2)]);var b=r.GameRoute([N(0),N(3),N(2)]);
        check(a.Source=="game-gps"&&a.Points.SequenceEqual(new float[]{0,0,500,0,500,500}),"GPS follows game-chosen nodes rather than a new destination route");
        check(b.Points.SequenceEqual(new float[]{0,0,0,500,500,500})&&!a.Points.SequenceEqual(b.Points),"Reroute with the same node count changes the actual path");
        check(r.GameRoute([N(0),N(0),N(1)]).Points.Length==4,"Repeated adjacent GPS node does not add a phantom leg");
        try{r.GameRoute([N(0),N(2)]);check(false,"Skipped GPS node");}catch(InvalidOperationException){check(true,"GPS interpolation cannot shortcut through an unreported game node");}
        try{r.GameRoute([N(0),N(1) with{Uid="999"}]);check(false,"Unknown GPS UID");}catch(InvalidOperationException){check(true,"Unmapped GPS node reports unavailable instead of inventing a road");}
        try{r.GameRoute([N(0),N(1) with{X=510}]);check(false,"GPS mismatch");}catch(InvalidOperationException){check(true,"Mismatched GPS and map coordinates rejected");}
        try{r.GameRoute([N(0),N(1) with{SecondsToEnd=float.NaN}]);check(false,"GPS invalid float");}catch(ArgumentException){check(true,"Malformed GPS data rejected");}
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{r.GameRoute([N(0),N(1)],cancel.Token);check(false,"GPS cancel");}catch(OperationCanceledException){check(true,"GPS geometry respects cancellation");}
        check(TruckGameGpsReader.Find([0x48,0x8b,0x11,0xc3],"48 8b ? c3").SequenceEqual(new[]{0})&&TruckGameGpsReader.Find([0x48,0x8b],"48 8b ?").Length==0,"GPS signature scan handles wildcards and truncated instructions");
        using var reader=new TruckGameGpsReader();check(!reader.TryRead("wrong-profile",out var invalid)&&invalid.Length==0,"GPS reader never attaches to an unknown game profile");
    }
}
