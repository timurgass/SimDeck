using System.Buffers.Binary;
using System.Text;
using SimDeck.Core;
static class BeamNgDamageTests {
 public static void Run(Action<bool,string> check){
  var b=new byte[1616];Encoding.ASCII.GetBytes("SMD4").CopyTo(b,0);
  void U(int o,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o),v);
  void F(int o,float v)=>BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o),v);
  U(4,4);F(8,20);F(12,1500);F(40,6000);U(252,6);
  Encoding.ASCII.GetBytes("truck").CopyTo(b,220);for(var i=0;i<6;i++){F(256+i*12,i%2==0?-1:1);F(260+i*12,i/2*2-2);F(448+i*4,.1f*i);F(488+i*36+32,-1);}
  U(472,1u<<14|1u<<7);U(476,1u<<7);F(480,110);F(484,99);
  Encoding.ASCII.GetBytes("FL").CopyTo(b,488);U(512,7);U(516,2);F(520,350);
  U(1064,1);U(1068,12);Encoding.ASCII.GetBytes("Front suspension").CopyTo(b,1072);F(1136,.75f);
  check(SimDeckParser.TryParse(b,out var t)&&t?.BeamNg is not null&&t.Vehicle?.AxleCount==3,"BeamNG SMD4 retains actual 6-wheel vehicle and adds diagnostics");
  var d=t!.BeamNg!;check(d.Body.Count==6&&Math.Abs(d.Body["RR"]-.5)<.001&&d.Faults["radiatorLeak"]&&!d.Faults["transmissionBroken"]&&!d.Faults.ContainsKey("engineDisabled"),"BeamNG known healthy, faulty and unavailable states remain distinct");
  check(d.Wheels[0] is {Flat:true,Broken:false,BrakeDamaged:false,BrakeTemperature:350}&&d.Wheels[1].Flat==null,"BeamNG wheel state is paired by actual wheel index without guessing unknowns");
  check(d.Parts.Single().Name=="Front suspension"&&d.TotalDamagedParts==12&&d.CoolantTemperature==110,"BeamNG reports worst part names, full damage count and actual temperatures");
  var valid=b.ToArray();
  Encoding.ASCII.GetBytes("pessima").CopyTo(b,60);Encoding.ASCII.GetBytes("Ibishu vehiclesData.midsize.Name").CopyTo(b,124);
  check(SimDeckParser.TryParse(b,out t)&&t!.Vehicle!.Name=="Ibishu pessima","BeamNG localization keys fall back to the supplied model ID");valid.CopyTo(b,0);
  foreach(var offset in new[]{448,480}){F(offset,-.5f);check(!SimDeckParser.TryParse(b,out _),"BeamNG rejects negative values other than unknown sentinel at "+offset);valid.CopyTo(b,0);}
  foreach(var offset in new[]{448,480,520,1136}){F(offset,float.NaN);check(!SimDeckParser.TryParse(b,out _),"BeamNG rejects non-finite diagnostic field at "+offset);valid.CopyTo(b,0);}
  U(476,1u<<3);check(!SimDeckParser.TryParse(b,out _),"BeamNG rejects active fault outside known mask");valid.CopyTo(b,0);
  U(1064,9);check(!SimDeckParser.TryParse(b,out _),"BeamNG rejects overflowing parts array");valid.CopyTo(b,0);
  U(1068,0);check(!SimDeckParser.TryParse(b,out _),"BeamNG rejects contradictory part count");valid.CopyTo(b,0);
  F(448,-1);F(480,-1);check(SimDeckParser.TryParse(b,out t)&&!t!.BeamNg!.Body.ContainsKey("FL")&&t.BeamNg.CoolantTemperature==null,"BeamNG unavailable diagnostic values are omitted, never fabricated healthy");
  check(!SimDeckParser.TryParse(b[..448],out _),"BeamNG truncated SMD4 cannot masquerade as older packet");
 }
}
