using SimDeck.Core;

static class TruckGpsEditingTests
{
    public static async Task Run(Action<bool,string> check)
    {
        var revision=new string('A',64);TruckGpsWaypoint a=new("10",0,0),b=new("20",10,10),goal=new("30",20,20);
        TruckGpsSnapshot? state=new(revision,[a,b,goal]);var clicks=0;var editor=new TruckGpsEditor();
        var request=new TruckGpsDeleteRequest(Guid.NewGuid().ToString(),revision,"gps:waypoint:0:10");
        bool Click(TruckGpsWaypoint p){clicks++;state=new(revision,[b,goal]);return true;}
        var result=await editor.DeleteAsync(request,()=>state,Click,()=>true);
        check(result.Deleted&&clicks==1&&state!.Waypoints[^1]==goal,"Game waypoint removal verified while preserving goal and other pins");
        var repeat=await editor.DeleteAsync(request,()=>state,Click,()=>true);
        check(repeat==result&&clicks==1,"Repeating a completed command never clicks a removed pin and adds it again");
        check(!(await editor.DeleteAsync(request with{PointId="gps:waypoint:1:20"},()=>state,Click,()=>true)).Deleted&&clicks==1,"Reused command ID cannot target another pin");
        var stale=request with{CommandId=Guid.NewGuid().ToString(),Revision=new string('B',64)};
        check(!(await editor.DeleteAsync(stale,()=>state,Click,()=>true)).Deleted&&clicks==1,"Stale displayed route refused before mouse input");
        var end=request with{CommandId=Guid.NewGuid().ToString(),PointId="gps:waypoint:1:30"};
        check(!(await editor.DeleteAsync(end,()=>state,Click,()=>true)).Deleted&&clicks==1,"Final game destination cannot be deleted as an intermediate point");
        var blocked=request with{CommandId=Guid.NewGuid().ToString(),PointId="gps:waypoint:0:20"};
        check(!(await editor.DeleteAsync(blocked,()=>state,Click,()=>false)).Deleted&&clicks==1,"Disabled or unfocused input prevents game editing");
        state=new(revision,[a,a,goal]);
        var duplicate=request with{CommandId=Guid.NewGuid().ToString(),PointId="gps:waypoint:1:10"};
        check((await editor.DeleteAsync(duplicate,()=>state,p=>{state=new(revision,[a,goal]);return true;},()=>true)).Deleted,"Repeated GPS UID still removes only selected occurrence");
        state=new(revision,[a,b,goal]);
        var changed=request with{CommandId=Guid.NewGuid().ToString()};
        check(!(await editor.DeleteAsync(changed,()=>state,p=>{state=new(revision,[a,goal]);return true;},()=>true)).Deleted,"Removing an unexpected pin never reports success");
        var missing=request with{CommandId=Guid.NewGuid().ToString()};
        check(!(await editor.DeleteAsync(missing,()=>null,Click,()=>true)).Deleted,"Unavailable GPS never produces blind input");
        var malformed=request with{CommandId="not-a-guid"};
        check(!(await editor.DeleteAsync(malformed,()=>state,Click,()=>true)).Deleted,"Malformed command IDs refused");
        var fake=new TestBackend();var input=new InputEngine(fake);input.Configure([new("test",1)]);input.BeginSession("s");
        using(var lease=input.TryExclusive())check(lease is not null&&input.Invoke("s","1","test","press","1").Code=="input_busy"&&fake.Count==0,"GPS operation excludes overlapping controller input");
        check(input.Invoke("s","2","test","press","2").Success&&input.TryExclusive() is null,"Held controller key prevents starting a GPS edit");input.ReleaseAll();
        var view=new TruckMapView(17,1423,90,840,[0,5000,0],[-.70710677f,.70710677f,0,0], [.85f,0,0,0,0,1.59f,0,0,0,0,-1,-2,0,0,-1,0]);
        var p1=view.Project(0,0,0,1920,1080);var p2=view.Project(0,0,0,1280,720);
        check(p1.HasValue&&p2.HasValue&&Math.Abs(p1.Value.X-960)<1&&Math.Abs(p1.Value.Y-498)<1&&Math.Abs(p1.Value.X/1.5-p2.Value.X)<1,"Map projection respects quaternion, pin head, resolution and letterboxing");
        check(view.Project(1e6f,0,1e6f,1920,1080) is null&&(view with{Rotation=[0,0,0,0]}).Project(0,0,0,1920,1080) is null&&view.Project(float.NaN,0,0,1920,1080) is null,"Off-screen or invalid projection refuses to click");
    }
    sealed class TestBackend:IInputBackend{public int Count;public bool CanInject=>true;public bool Send(ushort code,bool down){Count++;return true;}}
}
