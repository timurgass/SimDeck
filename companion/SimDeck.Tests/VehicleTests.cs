using System.Buffers.Binary;
using System.Text;
using System.IO;
using SimDeck.Core;
using SimDeck.App;

static class VehicleTests {
    public static void Run(Action<bool,string> check) {
        var b=new byte[448]; Encoding.ASCII.GetBytes("SMD3").CopyTo(b,0);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4),3);
        void Float(int offset,float value)=>BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(offset),value);
        Float(8,20); Float(12,1000); Float(24,.5f); Float(40,6000);
        Encoding.UTF8.GetBytes("semi").CopyTo(b,60); Encoding.UTF8.GetBytes("Gavril T-Series").CopyTo(b,124); Encoding.UTF8.GetBytes("truck").CopyTo(b,220);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(252),6);
        for(var i=0;i<6;i++){Float(256+i*12,i%2==0?-1:1);Float(260+i*12,i/2*2-2);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(264+i*12),i<2?1u:3u);}
        check(SimDeckParser.TryParse(b,out var beam)&&beam?.Vehicle is {Kind:"truck",AxleCount:3}&&beam.Vehicle.Wheels.Count==6,"BeamNG SMD3 selects three-axle truck from actual wheel geometry");
        var invalid=b.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(invalid.AsSpan(252),17);
        check(!SimDeckParser.TryParse(invalid,out _),"BeamNG rejects oversized wheel array");
        Float(256,float.NaN);check(!SimDeckParser.TryParse(b,out _),"BeamNG rejects non-finite wheel geometry");
        check(VehicleKinds.Resolve("WoodHarvester")=="forestry"&&VehicleKinds.Resolve("customDragon")=="unknown","Unknown model never becomes a guessed tractor or car");
        var dir=Path.Combine(Path.GetTempPath(),"simdeck-vehicles-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try {
            var path=Path.Combine(dir,Fs25LiveReader.FileName);
            string root="<simdeckStatus version='2' controlled='true' lowered='true'><vehicle id='0' instance='1' name='Tractor' kind='tractor'><wheel x='-1' z='-2'/><wheel x='1' z='-2'/></vehicle>";
            File.WriteAllText(path,root+"<vehicle id='1' parentId='0' name='Planter' kind='implement' lowered='true' turnedOn='false' fold='0.5'/><vehicle id='2' parentId='1' name='Tank' kind='trailer'/></simdeckStatus>");
            var live=Fs25LiveReader.ReadSnapshot(dir,DateTime.UtcNow);
            check(live?.Vehicle is {Kind:"tractor",AxleCount:1}&&live.Vehicle.Attachments.Count==2&&live.Vehicle.Attachments[0].ParentId==live.Vehicle.Id&&live.Vehicle.Attachments[1].ParentId=="1","FS25 carries nested equipment hierarchy without ID collisions");
            check(live?.Vehicle?.Attachments[0] is {Lowered:true,TurnedOn:false,Fold:.5},"FS25 equipment states come from the mod");
            var hub=new TelemetryHub();hub.Reset("fs25");hub.PublishFs25Live(live!.States,live.Vehicle);
            check(hub.Read().Data?.Vehicle?.Name=="Tractor","FS25 live identity overlays base telemetry");
            hub.ClearFs25Live();check(hub.Read().Data?.Vehicle is null,"Missing FS25 bridge clears previous identity");
            File.WriteAllText(path,"<simdeckStatus version='2' controlled='false'/>");live=Fs25LiveReader.ReadSnapshot(dir,DateTime.UtcNow);
            check(live?.Vehicle?.Controlled==false&&live.States.Count==0,"Leaving FS25 vehicle explicitly removes stale equipment states");
            File.WriteAllText(path,root+"<vehicle id='1' parentId='2'/></simdeckStatus>");check(Fs25LiveReader.ReadSnapshot(dir,DateTime.UtcNow) is null,"FS25 rejects dangling or cyclic attachment hierarchy");
            File.WriteAllText(path,root+"</simdeckStatus>");File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddSeconds(-3));check(Fs25LiveReader.ReadSnapshot(dir,DateTime.UtcNow) is null,"Expired FS25 identity cannot select previous vehicle");
        }finally {Directory.Delete(dir,true);}
        // Exact RenCloud SDK packed offsets, including trailer zone after offset 6000.
        b=new byte[ScsTelemetryParser.SnapshotSize];
        void UInt(int o,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o),v);
        void Text(int o,string s)=>Encoding.UTF8.GetBytes(s).CopyTo(b,o);
        b[0]=1;UInt(40,12);UInt(52,1);UInt(68,12);UInt(80,6);BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(8),42);
        Float(948,20);Float(952,1200);Float(740,2500);Float(704,600);Float(1000,300);
        Text(2364,"Volvo");Text(2428,"fh16");Text(2492,"FH16");
        for(var i=0;i<6;i++){Float(1676+i*4,i%2==0?-1:1);Float(1804+i*4,2-i/2*2);b[1532+i]=(byte)(i>=2?1:0);}
        b[6080]=1;UInt(6148,4);Text(6920,"trailer.model");Text(7240,"Refrigerated trailer");
        for(var i=0;i<4;i++){Float(6676+i*4,i%2==0?-1:1);Float(6804+i*4,2-i/2*2);}
        check(ScsTelemetryParser.TryParse(b,out _,out var ets)&&ets?.Vehicle is {Name:"Volvo FH16",AxleCount:3}&&ets.Vehicle.Attachments.Count==1&&ets.Vehicle.Attachments[0].Wheels.Count==4,"ETS2 reads current truck and connected trailer from SDK memory");
        b[6080]=0;UInt(80,4);Text(2428,"other\0");Text(2492,"New truck\0");
        check(ScsTelemetryParser.TryParse(b,out _,out ets)&&ets?.Vehicle?.Attachments.Count==0&&ets.Vehicle.Wheels.Count==4&&ets.Vehicle.Name=="Volvo New truck","ETS2 switching chassis and uncoupling removes previous configuration");
    }
}
