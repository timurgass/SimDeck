using System.Buffers.Binary;
using System.Text.Json;
using SimDeck.Core;

static class F1DetailTests
{
    static byte[] Packet(byte id, int length, uint frame = 1, ulong session = 42)
    {
        var b = new byte[length]; BinaryPrimitives.WriteUInt16LittleEndian(b, 2024);
        b[2]=24; b[5]=1; b[6]=id; b[27]=3;
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(7), session);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(23), frame); return b;
    }
    static void F(byte[] b, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(offset), value);
    public static void Run(Action<bool,string> check)
    {
        long now = 1000; var p = new F1TelemetryParser(() => now);
        var careerStatus=Packet(7,1239); var sc=29+3*55;
        F(careerStatus,sc+5,8.632782f); F(careerStatus,sc+9,110); careerStatus[sc+21]=9; careerStatus[sc+26]=17; careerStatus[sc+41]=1;
        check(p.TryParse(careerStatus,out _), "Live career maxGears=9 is accepted");
        var motion = Packet(0,1349); F(motion,29+3*60,100); F(motion,29+3*60+8,200);
        var damage = Packet(10,953); var d = 29+3*42;
        for (var i=0;i<4;i++) { F(damage,d+i*4,10+i); damage[d+16+i]=(byte)(20+i); }
        damage[d+24]=7; damage[d+37]=33;
        var setup = Packet(5,1133); setup[29+3*50]=24; setup[29+3*50+1]=28; F(setup,1129,26);
        var session = Packet(1,753); session[29+6]=15; session[29+7]=3;
        check(p.TryParse(motion,out var t) && t is null && p.TryParse(damage,out t) && t is null && p.TryParse(setup,out t) && t is null && p.TryParse(session,out t) && t is null, "F1 auxiliary packets never publish a fresh driving frame");
        var telemetry=ProfileTests.Packet(6); var c=29+3*60;
        for(var i=0;i<4;i++) { telemetry[c+30+i]=(byte)(80+i); telemetry[c+34+i]=(byte)(90+i); F(telemetry,c+40+i*4,22+i); }
        BinaryPrimitives.WriteUInt16LittleEndian(telemetry.AsSpan(c+38),105);
        telemetry[1349]=1;
        p.TryParse(telemetry,out t);
        check(t!.F1!.MfdPanelIndex==1, "Player MFD page comes from telemetry packet tail, not per-car data");
        check(t!.FuelFraction is > 0.078 and < 0.079 && t.F1!.Values["compound"]==17, "Career gear count cannot discard fuel and tyre compound");
        check(t!.F1!.Wheels[2].Surface==82 && t.F1.Wheels[2].Inner==92 && t.F1.Wheels[2].Pressure==24 && t.F1.Wheels[2].Wear==12 && t.F1.Wheels[2].Damage==22, "Front-left wheel uses RL/RR/FL/FR wire order across independent packets");
        check(t.F1.EngineTemperature==105 && t.F1.Values["iceWear"]==33 && t.F1.Values["frontLeftWingDamage"]==7, "Engine and wing damage offsets match F1 24 specification");
        var p25=new F1TelemetryParser(()=>now);var damage25=Packet(10,1041);BinaryPrimitives.WriteUInt16LittleEndian(damage25,2025);damage25[2]=25;var d25=29+3*46;
        for(var i=0;i<4;i++){F(damage25,d25+i*4,15+i);damage25[d25+16+i]=(byte)(25+i);damage25[d25+24+i]=(byte)(5+i);}damage25[d25+28]=9;damage25[d25+41]=31;
        var telemetry25=(byte[])telemetry.Clone();BinaryPrimitives.WriteUInt16LittleEndian(telemetry25,2025);telemetry25[2]=25;
        check(p25.TryParse(damage25,out _)&&p25.TryParse(telemetry25,out var t25)&&t25!.F1!.Values["tyreBlister2"]==7&&t25.F1.Values["frontLeftWingDamage"]==9&&t25.F1.Values["iceWear"]==31,"F1 25 blister insertion preserves damage and engine offsets");
        check(t.F1.Values["frontWing"]==24 && t.F1.Values["nextFrontWing"]==26 && t.F1.Values["sessionType"]==15, "Setup next-pit wing is read after all 22 car records");
        check(t.F1.Position == new TrackPoint(100,200) && t.F1.Trail.Length==1, "Motion coordinates become real trail and player position");
        var json=JsonSerializer.Serialize(t,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        check(json.Contains("\"wheels\"") && json.Contains("\"nextFrontWing\":26") && json.Length<65536, "F1 detail payload is compatible with the bounded Android wire format");
        now+=600; p.TryParse(ProfileTests.Packet(6,2),out t);
        check(t!.F1!.Position is null && t.F1.Trail.Length==1 && t.F1.Wheels[2].Wear==12, "Stale motion hides moving marker but retains recorded trail");
        now+=1000; p.TryParse(ProfileTests.Packet(6,3),out t);
        check(t!.F1!.Wheels.All(w=>w.Wear is null) && !t.F1.Values.ContainsKey("nextFrontWing"), "Expired setup and damage cannot appear as live data");
        var bad=Packet(10,953,2); F(bad,d,float.NaN);
        check(!p.TryParse(bad,out _) && !p.TryParse(setup[..1132],out _), "Malformed damage and truncated setup are rejected");
        bad=ProfileTests.Packet(6,4); F(bad,c+40,float.NaN);
        check(!p.TryParse(bad,out _), "Non-finite tyre pressure is rejected before serialization");
        p.TryParse(ProfileTests.Packet(6,1,99),out t);
        check(t!.F1!.Trail.Length==0 && t.F1.Wheels.All(w=>w.Wear is null), "Session change clears trail and car damage");
        for(uint i=1;i<=700;i++) { motion=Packet(0,1349,i,99); F(motion,29+3*60,i*15); p.TryParse(motion,out _); }
        p.TryParse(ProfileTests.Packet(6,2,99),out t);
        check(t!.F1!.Trail.Length<=256 && t.F1.Trail.Length>100, "Long driven trail remains bounded for tablet transport");
    }
}
