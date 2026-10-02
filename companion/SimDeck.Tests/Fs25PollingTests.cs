using System.IO;
using SimDeck.App;
using SimDeck.Core;

static class Fs25PollingTests
{
    public static async Task Run(Action<bool,string> check,string directory)
    {
        var data=Path.Combine(directory,"fs25-slow-save");var save=Path.Combine(data,"savegame1");Directory.CreateDirectory(save);
        var fixture=Path.Combine(AppContext.BaseDirectory,"fixtures","fs25","save-mid-season");
        foreach(var file in Directory.GetFiles(fixture,"*.xml")) File.Copy(file,Path.Combine(save,Path.GetFileName(file)));
        var previousData=Environment.GetEnvironmentVariable(Fs25GamePaths.UserDataEnvVar);
        var previousInstall=Environment.GetEnvironmentVariable(Fs25GamePaths.InstallEnvVar);
        Environment.SetEnvironmentVariable(Fs25GamePaths.UserDataEnvVar,data);
        Environment.SetEnvironmentVariable(Fs25GamePaths.InstallEnvVar,data);
        using var release=new ManualResetEventSlim();using var stopWriter=new CancellationTokenSource();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writer=Task.Run(async()=>{while(!stopWriter.IsCancellationRequested){
            File.WriteAllText(Path.Combine(data,Fs25LiveReader.FileName),"<simdeckStatus version='2' controlled='true' motor='true'><vehicle id='0' model='fixture' name='Test tractor' kind='tractor'/></simdeckStatus>");
            try { await Task.Delay(200,stopWriter.Token); } catch(OperationCanceledException) { }
        }});
        CompanionHost? host=null;
        try {
            host=new CompanionHost(Path.Combine(directory,"fs25-slow-host"),new RecordingInput(Path.Combine(directory,"slow-save-input.json")),
                (d,now)=>{entered.TrySetResult();release.Wait(TimeSpan.FromSeconds(10));return Fs25SaveReader.Read(d,now);});
            host.Store.Value.Port=29473;host.Store.Value.UdpPort=24473;host.SelectProfile("fs25");
            await host.StartAsync(localOnly:true);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(1800);
            check(host.Telemetry.HasFs25Live && host.Telemetry.Read().Data?.Vehicle?.Name=="Test tractor",
                "Blocked FS25 save read does not interrupt live equipment polling beyond the freshness window");
            var change=Task.Run(()=>host.SelectProfile("acc"));
            var completed=await Task.WhenAny(change,Task.Delay(500))==change;
            check(completed,"Blocked FS25 save read does not hold the profile/input lock");
            release.Set();await change;await Task.Delay(300);
            check(host.Telemetry.Read().Data?.Fs25 is null,
                "Late FS25 save result cannot overwrite telemetry after switching to another game");
        } finally {
            release.Set();stopWriter.Cancel();await writer;
            if(host is not null)await host.DisposeAsync();
            Environment.SetEnvironmentVariable(Fs25GamePaths.UserDataEnvVar,previousData);
            Environment.SetEnvironmentVariable(Fs25GamePaths.InstallEnvVar,previousInstall);
        }
    }
}
