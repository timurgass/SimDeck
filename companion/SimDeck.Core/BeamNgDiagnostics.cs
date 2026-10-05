using System.Buffers.Binary;
namespace SimDeck.Core;
public sealed record BeamNgWheelDamage(int Index, string Name, bool? Broken, bool? Flat, bool? BrakeDamaged, double? BrakeTemperature);
public sealed record BeamNgPartDamage(string Name, double Damage);
public sealed record BeamNgDiagnostics(IReadOnlyDictionary<string,double> Body, IReadOnlyDictionary<string,bool> Faults,
    BeamNgWheelDamage[] Wheels, BeamNgPartDamage[] Parts, int? TotalDamagedParts, double? CoolantTemperature, double? OilTemperature)
{
    public static readonly string[] FaultKeys = ["engineDisabled","engineLockedUp","engineReducedTorque","catastrophicOverrevDamage","mildOverrevDamage",
        "engineHydrolocked","impactDamage","radiatorLeak","oilpanLeak","headGasketDamaged","pistonRingsDamaged","rodBearingsDamaged","coolantOverheating","oilOverheating","transmissionBroken"];
    public static bool TryParse(ReadOnlySpan<byte> b,int wheels,out BeamNgDiagnostics? result)
    {
        result=null;if(b.Length!=1616||wheels is <0 or >16)return false;
        var body=new Dictionary<string,double>();string[] zones=["FL","FR","ML","MR","RL","RR"];
        for(var i=0;i<6;i++){var v=BinaryPrimitives.ReadSingleLittleEndian(b[(448+i*4)..]);if(!float.IsFinite(v)||(v != -1 && (v<0 || v>1)))return false;if(v>=0)body[zones[i]]=v;}
        var known=BinaryPrimitives.ReadUInt32LittleEndian(b[472..]);var active=BinaryPrimitives.ReadUInt32LittleEndian(b[476..]);
        if((known>>FaultKeys.Length)!=0||(active&~known)!=0)return false;
        var faults=new Dictionary<string,bool>();for(var i=0;i<FaultKeys.Length;i++)if((known&(1u<<i))!=0)faults[FaultKeys[i]]=(active&(1u<<i))!=0;
        var coolant=BinaryPrimitives.ReadSingleLittleEndian(b[480..]);var oil=BinaryPrimitives.ReadSingleLittleEndian(b[484..]);
        if(!ValidTemperature(coolant)||!ValidTemperature(oil))return false;
        var list=new List<BeamNgWheelDamage>();
        for(var i=0;i<wheels;i++){
            var o=488+i*36;var k=BinaryPrimitives.ReadUInt32LittleEndian(b[(o+24)..]);var a=BinaryPrimitives.ReadUInt32LittleEndian(b[(o+28)..]);var t=BinaryPrimitives.ReadSingleLittleEndian(b[(o+32)..]);
            if(k>7||(a&~k)!=0||!ValidTemperature(t))return false;
            bool? State(int bit)=>(k&(1u<<bit))!=0?(a&(1u<<bit))!=0:null;
            list.Add(new(i,VehicleKinds.Text(b.Slice(o,24)),State(0),State(1),State(2),t>=0?t:null));
        }
        var count=BinaryPrimitives.ReadUInt32LittleEndian(b[1064..]);var total=BinaryPrimitives.ReadInt32LittleEndian(b[1068..]);
        if(count>8||total is <-1 or >100000||total>=0&&count>total||total<0&&count>0)return false;
        var parts=new List<BeamNgPartDamage>();
        for(var i=0;i<count;i++){var o=1072+i*68;var score=BinaryPrimitives.ReadSingleLittleEndian(b[(o+64)..]);if(!float.IsFinite(score)||score is <=0 or >1)return false;parts.Add(new(VehicleKinds.Text(b.Slice(o,64)),score));}
        result=new(body,faults,list.ToArray(),parts.ToArray(),total>=0?total:null,coolant>=0?coolant:null,oil>=0?oil:null);return true;
    }
    static bool ValidTemperature(float v)=>float.IsFinite(v)&&(v==-1||v is >=0 and <=2500);
}
