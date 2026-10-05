using System.Text;
using System.IO;
using SimDeck.Core;

static class Fs25OperationsTests
{
    static MemoryStream Xml(string text) => new(Encoding.UTF8.GetBytes(text));
    public static void Run(Action<bool,string> check)
    {
        const string farms = "<farms><farm farmId='1'><finances><stats day='0'><soldMilk>1200</soldMilk><purchaseFuel>-200</purchaseFuel><bad>NaN</bad></stats><stats day='1'><wagePayment>-100</wagePayment></stats></finances></farm><farm farmId='2'/></farms>";
        using var f = Xml(farms);
        using var p = Xml("<placeables><placeable farmId='1' uniqueId='preplaced_farmSilo_0123456789abcdef0123456789abcdef'><silo><storage farmId='1'><node fillType='WHEAT' fillLevel='10000'/><node fillType='BARLEY' fillLevel='NaN'/></storage><storage farmId='2'><node fillType='WHEAT' fillLevel='2000'/></storage></silo><productionPoint><production id='flour' isEnabled='true'/><storage farmId='1'><node fillType='FLOUR' fillLevel='500'/></storage></productionPoint></placeable><placeable farmId='0'><silo><storage><node fillType='WHEAT' fillLevel='9999'/></storage></silo></placeable></placeables>");
        using var v = Xml("<vehicles><vehicle farmId='1' filename='data/vehicles/test/tractor.xml' propertyState='LEASED' operatingTime='7200000'><wearable damage='0.35'/><fillUnit><unit fillType='WHEAT' fillLevel='1500'/></fillUnit></vehicle><vehicle farmId='2' filename='secret/path/trailer.xml' propertyState='OWNED'><wearable damage='1.5'/></vehicle><vehicle farmId='1' propertyState='OWNED'><pallet/><fillUnit><unit fillType='SEEDS' fillLevel='1000'/></fillUnit></vehicle></vehicles>");
        var data = Fs25OperationsReader.Parse(f,p,v);
        var day = data.Ledgers[0].Days.Single(d=>d.Day==0);
        check(day.Income==1200 && day.Expenses==200 && day.Net==1000 && day.Entries.Length==2,"FS25 ledger preserves signed transactions and rejects NaN");
        check(data.Ledgers[0].Days.First().Day==1 && data.Ledgers[1].Days.Length==0,"FS25 ledger retains actual farm and newest saved day");
        check(data.Stocks.Where(s=>s.FarmId==1 && s.Crop=="WHEAT").Sum(s=>s.Litres)==11500 && data.Stocks.Single(s=>s.FarmId==2).Litres==2000,"FS25 storage ownership overrides building owner; vehicle cargo is separate");
        check(data.Stocks.All(s=>s.FarmId>0) && data.Stocks.Any(s=>s.Crop=="FLOUR") && data.Stocks.Any(s=>s.Crop=="SEEDS"),"FS25 excludes NPC stocks and includes production storage/pallet contents");
        check(data.Fleet.Length==2 && data.Fleet[0].Property=="LEASED" && data.Fleet[0].Hours==2 && data.Fleet[0].Damage==35 && data.Fleet[1].Damage==null,"FS25 fleet converts operating milliseconds and damage; pallets are not vehicles");
        check(data.Fleet[1].Model=="trailer" && data.Stocks.First().Location=="farmSilo","FS25 display metadata omits local paths and object UUIDs");
        check(data.Productions.Single().Enabled==true && data.Productions[0].FarmId==1,"FS25 recipe flag is saved state, with farm ownership");
        using var only = Xml(farms);var optional = Fs25OperationsReader.Parse(only,null,null);
        check(!optional.StorageAvailable && !optional.FleetAvailable && optional.Stocks.Length==0,"FS25 older saves distinguish missing inventory files from empty storage");
        var rejected=false;
        try {using var x=Xml("<!DOCTYPE farms [<!ENTITY boom SYSTEM 'file:///unavailable'>]><farms>&boom;</farms>");Fs25OperationsReader.Parse(x,null,null);}catch(System.Xml.XmlException){rejected=true;}
        check(rejected,"FS25 operations XML prohibits external entities");
        using var sf=Xml(farms);using var sv=Xml("<vehicles><vehicle farmId='1'><fillUnit><unit fillType='AIR' fillLevel='100'/><unit fillType='ELECTRICCHARGE' fillLevel='100'/><unit fillType='DIESEL' fillLevel='50'/></fillUnit></vehicle></vehicles>");
        check(Fs25OperationsReader.Parse(sf,null,sv).Stocks is [{Crop:"DIESEL",Litres:50}],"FS25 inventory excludes air pressure and electrical charge");
        using var bigf=Xml(farms);using var bigv=Xml("<vehicles>"+string.Concat(Enumerable.Range(0,2200).Select(i=>"<vehicle farmId='1' propertyState='OWNED' filename='"+new string('x',160)+i+".xml'><fillUnit><unit fillType='WHEAT' fillLevel='50'/></fillUnit></vehicle>"))+"</vehicles>");
        var bounded=Fs25OperationsReader.Parse(bigf,null,bigv);
        check(bounded.Truncated && System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(bounded,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)).Length<=262144 && bounded.Ledgers[0].Days.First().Net==-100,"FS25 large inventory stays within message budget and preserves finance totals");
    }
}
