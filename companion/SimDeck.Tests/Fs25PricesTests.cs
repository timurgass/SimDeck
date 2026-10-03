using System.IO;
using SimDeck.App;
using SimDeck.Core;

static class Fs25PricesTests
{
    public static void Run(Action<bool,string> check)
    {
        var dir=Path.Combine(Path.GetTempPath(),"simdeck-market-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var file=Path.Combine(dir,Fs25PricesReader.FileName);
        try {
            File.WriteAllText(file,"<simdeckPrices version='1'><offer crop='WHEAT' cropName='Wheat' station='Mill' pricePer1000='1250.5' formatted='1,251 $'/><offer crop='WHEAT' station='Bad' pricePer1000='NaN'/></simdeckPrices>");
            var read=Fs25PricesReader.Read(dir,DateTime.UtcNow)!;
            check(read.Data.Offers.Count==1 && read.Data.Offers[0].PricePer1000==1250.5,"FS25 prices preserve real per-1000 offer and reject nonfinite prices");
            check(Fs25PricesReader.Read(dir,DateTime.UtcNow,read.WrittenAtUtc) is null,"Unchanged market file is not republished as fresh");
            check(Fs25PricesReader.Read(dir,DateTime.UtcNow.AddMinutes(1)) is null,"Old market file is not mistaken for running game prices");
            var hub=new TelemetryHub();hub.Reset("fs25");hub.PublishFs25Live(new Dictionary<string,bool>{{"fs25Lower",true}},sourceAgeMs:2000);
            hub.PublishFs25Prices(read.Data);
            check(hub.Read().Age>=2000 && hub.Read().Data?.ActionStates?["fs25Lower"]==true,"New prices do not refresh expired equipment or erase action state");
            hub.Publish(new(0,0,0,null,0,0,0));
            check(hub.Read().Data?.Fs25Prices?.Offers.Count==1,"Save metadata refresh preserves independently received prices");
            hub.Reset("acc");hub.PublishFs25Prices(read.Data);
            check(hub.Read().Data is null,"FS25 market data cannot leak into another game profile");
            File.WriteAllText(file,"<!DOCTYPE x [<!ENTITY x SYSTEM 'file:///secret'>]><simdeckPrices version='1'>&x;</simdeckPrices>");
            check(Fs25PricesReader.Read(dir,DateTime.UtcNow) is null,"Market XML external entities are prohibited");
        } finally { File.Delete(file);Directory.Delete(dir); }
    }
}
