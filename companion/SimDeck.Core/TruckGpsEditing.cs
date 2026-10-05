namespace SimDeck.Core;

public sealed record TruckGpsDeleteRequest(string CommandId,string Revision,string PointId);
public sealed record TruckGpsEditResult(bool Deleted,string Message);
public sealed record TruckGpsSnapshot(string Revision,TruckGpsWaypoint[] Waypoints);

// A mouse click is not idempotent: clicking a removed pin again can ADD a pin.
// Cache every attempted command for the application lifetime, including failures.
public sealed class TruckGpsEditor
{
    readonly SemaphoreSlim gate=new(1,1);
    readonly Dictionary<string,(TruckGpsDeleteRequest Request,TruckGpsEditResult Result)> completed=[];
    public async Task<TruckGpsEditResult> DeleteAsync(TruckGpsDeleteRequest request,
        Func<TruckGpsSnapshot?> capture,Func<TruckGpsWaypoint,bool> click,Func<bool> allowed,
        CancellationToken cancel=default)
    {
        if(request is null||!Guid.TryParse(request.CommandId,out _)||request.Revision is null||request.Revision.Length!=64||
            request.Revision.Any(c=>!Uri.IsHexDigit(c))||request.PointId is null||request.PointId.Length>100)
            return new(false,"Некорректная команда удаления");
        if(!await gate.WaitAsync(0,cancel))return new(false,"Изменение GPS уже выполняется");
        try
        {
            if(completed.TryGetValue(request.CommandId,out var old))return old.Request==request?old.Result:new(false,"Идентификатор команды уже использован");
            if(completed.Count>=5000)return new(false,"Лимит команд GPS: перезапустите Companion");
            // Reserve before any input, so an aborted HTTP response never repeats a click.
            var result=new TruckGpsEditResult(false,"Удаление не подтверждено; обновите список точек GPS");
            completed.Add(request.CommandId,(request,result));
            try
            {
                if(!allowed())return Remember(new(false,"Откройте карту выбранной игры, остановив грузовик; разрешите ввод в Companion"));
                var before=capture();
                if(before is null||before.Revision!=request.Revision)return Remember(new(false,"Маршрут изменился. Обновите список GPS-точек"));
                var fields=request.PointId.Split(':');
                if(fields.Length!=4||fields[0]!="gps"||fields[1]!="waypoint"||!int.TryParse(fields[2],out var index)||
                    index<0||index>=before.Waypoints.Length-1||before.Waypoints[index].Uid!=fields[3])
                    return Remember(new(false,"Можно удалить только промежуточную GPS-точку"));
                var expected=before.Waypoints.Where((_,i)=>i!=index).ToArray();
                if(!allowed()||!click(before.Waypoints[index]))return Remember(new(false,"Откройте игровую карту и покажите выбранный значок целиком. Не двигайте карту во время удаления"));
                for(var i=0;i<20;i++)
                {
                    await Task.Delay(250,cancel);
                    var after=capture();
                    if(after is null)continue;
                    if(after.Waypoints.SequenceEqual(expected))return Remember(new(true,"Точка удалена из GPS игры"));
                    if(!after.Waypoints.SequenceEqual(before.Waypoints))return Remember(new(false,"GPS изменился иначе, чем ожидалось. Проверьте игровую карту"));
                }
                return Remember(result);
            }
            catch(OperationCanceledException){return Remember(result);}
            TruckGpsEditResult Remember(TruckGpsEditResult value){completed[request.CommandId]=(request,value);return value;}
        }
        finally{gate.Release();}
    }
}

public sealed record TruckMapView(float Left,float Right,float Bottom,float Top,float[] Center,float[] Rotation,float[] Projection)
{
    // Standard view projection; Prism stores quaternions in WXYZ order and a
    // row-major projection. Layout coordinates are bottom-up, client pixels top-down.
    public (double X,double Y)? Project(float x,float y,float z,int width,int height)
    {
        if(Center.Length!=3||Rotation.Length!=4||Projection.Length!=16||width<640||height<480||
            !new[]{Left,Right,Bottom,Top,x,y,z}.Concat(Center).Concat(Rotation).Concat(Projection).All(float.IsFinite)||Right<=Left||Top<=Bottom)return null;
        var norm=Rotation.Sum(v=>v*v);if(Math.Abs(norm-1)>.01)return null;
        double w=Rotation[0],qx=-Rotation[1],qy=-Rotation[2],qz=-Rotation[3];
        double vx=x-Center[0],vy=y-Center[1],vz=z-Center[2];
        var tx=2*(qy*vz-qz*vy);var ty=2*(qz*vx-qx*vz);var tz=2*(qx*vy-qy*vx);
        var px=vx+w*tx+qy*tz-qz*ty;var py=vy+w*ty+qz*tx-qx*tz;var pz=vz+w*tz+qx*ty-qy*tx;
        var m=Projection;var cw=m[12]*px+m[13]*py+m[14]*pz+m[15];if(cw<=.01)return null;
        var nx=(m[0]*px+m[1]*py+m[2]*pz+m[3])/cw;var ny=(m[4]*px+m[5]*py+m[6]*pz+m[7])/cw;
        var lx=Left+(Right-Left)*(nx+1)/2;var ly=Bottom+(Top-Bottom)*(ny+1)/2;
        // Pin anchor is at the road node; the numbered head is above it.
        ly+=20;
        if(lx<Left+32||lx>Right-32||ly<Bottom+40||ly>Top-40||lx>1100&&ly>680)return null;
        var scale=Math.Min(width/1440d,height/900d);
        return((width-1440*scale)/2+lx*scale,(height+900*scale)/2-ly*scale);
    }
}
