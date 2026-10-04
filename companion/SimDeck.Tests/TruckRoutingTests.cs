using SimDeck.Core;
internal static class TruckRoutingTests
{
    public static void Run(Action<bool,string> check)
    {
        Ets2NavNode[] nodes=[new(0,0),new(500,0),new(500,500),new(0,500),new(1000,0),new(1000,500)];
        Ets2NavEdge[] edges=[new(0,1,[0,0,500,0],"highway",90),new(1,2,[500,0,500,500]),new(2,3,[500,500,0,500]),new(3,0,[0,500,0,0]),new(1,4,[500,0,1000,0],"ferry",30),new(4,5,[1000,0,1000,500])];
        var map=new Ets2RoadMap(2,[],[new("Town",500,500)],[new("fuel","fuel","Заправка",500,200)],nodes,edges);
        var router=new TruckRouting(map);var target=new Ets2Poi("end","custom","Финиш",500,450);
        var route=router.Route(new(50,0,target));
        check(Math.Abs(route.Metres-900)<1 && route.Maneuvers.Any(m=>m.Direction=="right"),"Truck route follows directed graph and yields a right turn");
        check(route.Nearby.Any(p=>p.Place.Kind=="fuel"&&p.AlongMetres>600),"Fuel station lies ahead at route distance, not distance from truck");
        var reverse=router.Route(new(450,0,new("r","custom","Назад",50,0)));
        check(reverse.Metres>1000,"One-way road cannot be traversed backwards as a shortcut");
        var stop=router.Route(new(50,0,target,[new("s","rest","Стоянка",250,0)]));
        check(stop.Stops.Length==1 && Math.Abs(stop.Metres-route.Metres)<1,"Ordered waypoint splits an edge without adding phantom distance");
        check(stop.StopMetres is [var distance] && Math.Abs(distance-200)<1,"Waypoint progress can remove a visited stop before rerouting");
        var ferry=new Ets2Poi("f","custom","Другой берег",1000,450);
        check(router.Route(new(50,0,ferry)).Metres>1000,"Ferry follows explicit connection");
        try{router.Route(new(50,0,ferry,AvoidFerries:true));check(false,"Ferry avoidance");}catch(InvalidOperationException){check(true,"Avoiding ferry reports no path instead of drawing a straight line");}
        check(router.Search("town").Single().Kind=="city" && router.Search("запр").Single().Kind=="fuel","Search finds cities and actual service points");
        try{router.Route(new(double.NaN,0,target));check(false,"Bad coordinate");}catch(ArgumentException){check(true,"Invalid navigation coordinates rejected");}
        try{router.Route(new(0,0,target,Enumerable.Repeat(target,11).ToArray()));check(false,"Waypoints limit");}catch(ArgumentException){check(true,"More than ten waypoints rejected");}
        try{router.Route(new(0,0,new(null!,"custom",null!,0,0)));check(false,"Null destination");}catch(ArgumentException){check(true,"Malformed destination cannot cause a server crash");}
        // Crossing lines at different levels must not create an invented junction.
        var disconnected=new TruckRouting(new(2,[],[],[],[new(0,0),new(500,0),new(250,-500),new(250,500)],
            [new(0,1,[0,0,500,0]),new(2,3,[250,-500,250,500])]));
        try{disconnected.Route(new(10,0,new("x","custom","Мост",250,450)));check(false,"Crossing roads");}catch(InvalidOperationException){check(true,"Geometric crossing is not a routable junction");}
    }
}
