using System.Buffers.Binary;
using System.Text;
using SimDeck.Core;

static class F1RaceTests
{
    static byte[] Packet(byte id,int size,uint frame=1,ulong uid=42,int format=2024) {
        var b=new byte[size]; BinaryPrimitives.WriteUInt16LittleEndian(b,(ushort)format); b[2]=(byte)(format-2000);b[5]=1;b[6]=id;b[27]=3;
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(7),uid); BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(23),frame);return b;
    }
    public static void Run(Action<bool,string> check) {
        long now=1000;var p=new F1TelemetryParser(()=>now);
        var s=Packet(1,753);s[36]=27;s[35]=10;BinaryPrimitives.WriteUInt16LittleEndian(s.AsSpan(33),4909);
        var names=Packet(4,1350);names[29]=4;
        for(var i=0;i<4;i++){var c=30+i*60;names[c+3]=(byte)i;names[c+5]=(byte)(10+i);Encoding.UTF8.GetBytes(i==3?"Игрок Тест":"Driver "+i).CopyTo(names,c+7);}
        var laps=Packet(2,1285);
        for(var i=0;i<4;i++){var c=29+i*57;laps[c+32]=(byte)(4-i);laps[c+33]=5;laps[c+44]=4;laps[c+45]=2;BinaryPrimitives.WriteSingleLittleEndian(laps.AsSpan(c+20),i*1000);}
        laps[29+3*57+16]=1;BinaryPrimitives.WriteUInt16LittleEndian(laps.AsSpan(29+3*57+14),2345);
        check(p.TryParse(s,out _)&&p.TryParse(names,out var t)&&t is null&&p.TryParse(laps,out t)&&t is null,"Race auxiliary packets do not refresh driving telemetry");
        p.TryParse(ProfileTests.Packet(6),out t);var r=t!.F1!.Race!;
        check(r.TrackId==27&&r.TrackLength==4909&&r.Drivers.Length==4&&r.Fresh,"Session identity and active participant count bound race data");
        check(r.Drivers[0].Player&&r.Drivers[0].Name=="Игрок Тест"&&r.Drivers[0].Team==3&&r.Drivers[0].Distance==3000,"Race order preserves vehicle index, UTF-8 name, team and lap distance");
        check(r.Drivers[0].GapAheadMs==62345,"Lap timing combines minute and millisecond fields");
        var p25=new F1TelemetryParser(()=>now);var s25=Packet(1,753,1,84,2025);s25[36]=27;s25[35]=15;BinaryPrimitives.WriteUInt16LittleEndian(s25.AsSpan(33),4909);
        var names25=Packet(4,1284,1,84,2025);names25[29]=4;for(var i=0;i<4;i++){var c=30+i*57;names25[c+3]=(byte)i;names25[c+5]=(byte)(20+i);Encoding.UTF8.GetBytes(i==3?"Игрок 25":"F1 25 Driver "+i).CopyTo(names25,c+7);}
        var laps25=Packet(2,1285,1,84,2025);for(var i=0;i<4;i++){var c=29+i*57;laps25[c+32]=(byte)(4-i);laps25[c+33]=2;laps25[c+44]=4;laps25[c+45]=2;BinaryPrimitives.WriteSingleLittleEndian(laps25.AsSpan(c+20),i*900);}
        var telemetry25=ProfileTests.Packet(6,1,84);BinaryPrimitives.WriteUInt16LittleEndian(telemetry25,2025);telemetry25[2]=25;
        check(p25.TryParse(s25,out _)&&p25.TryParse(names25,out _)&&p25.TryParse(laps25,out _)&&p25.TryParse(telemetry25,out var f125)&&f125!.F1!.Race!.Drivers[0].Name=="Игрок 25","F1 25 participant stride and 32-byte names are parsed");
        var bad=Packet(4,1350,2);bad[29]=23;
        check(!p.TryParse(bad,out _)&&!p.TryParse(laps[..1284],out _),"Malformed participants and truncated lap packet rejected");
        var invalid=(byte[])laps.Clone();BinaryPrimitives.WriteUInt32LittleEndian(invalid.AsSpan(23),2);BinaryPrimitives.WriteSingleLittleEndian(invalid.AsSpan(49),float.NaN);
        check(!p.TryParse(invalid,out _),"Non-finite car distance cannot enter race payload");
        now+=1001;p.TryParse(ProfileTests.Packet(6,2),out t);
        check(!t!.F1!.Race!.Fresh&&t.F1.Race.TrackId==27,"Expired lap data hides moving markers while circuit identity remains");
        p.TryParse(ProfileTests.Packet(6,1,99),out t);
        check(t!.F1!.Race!.Drivers.Length==0&&t.F1.Race.TrackId==-1,"New session cannot retain opponents or old circuit");
        check(p.TryParse(laps,out _)&&p.TryParse(ProfileTests.Packet(6,2,99),out t)&&t!.F1!.Race!.Drivers.Length==0,"Late old session cannot resurrect previous opponents");
    }
}
