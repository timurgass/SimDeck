using System.IO;
using SimDeck.App;
using SimDeck.Core;

// Device visual/gesture QA, recording backend only. Keep its settings in a separate directory.
static class DashboardQa
{
    public static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        // Do not mix simulated states with the user's save/live file reader.
        var isolatedData = Path.Combine(directory,"no-game-data"); Directory.CreateDirectory(isolatedData);
        var previousData = Environment.GetEnvironmentVariable(Fs25GamePaths.UserDataEnvVar);
        Environment.SetEnvironmentVariable(Fs25GamePaths.UserDataEnvVar,isolatedData);
        try {
        await using var host = new CompanionHost(directory, new RecordingInput(Path.Combine(directory,"input-events.json")));
        host.Store.Value.UdpPort = 34444;
        await host.StartAsync();
        Console.WriteLine("Dashboard QA: simulated telemetry, recording backend; no game input.");
        for(var tick=0; tick<18000 && !File.Exists(Path.Combine(directory,"stop")); tick++) {
            var file = Path.Combine(directory,"profile.txt");
            var profile = File.Exists(file) ? File.ReadAllText(file).Trim() : "beamng-default";
            if(GameProfiles.KnownIds.Contains(profile) && host.Profile.Id!=profile) host.SelectProfile(profile);
            host.Backend.Enabled=true;
            var states = new Dictionary<string,bool> { ["ignition"]=true,["hazards"]=true,["fourWheelDrive"]=true,["etsCruise"]=true,["etsParkingBrake"]=false,["fs25Lower"]=true,["fs25TurnOn"]=false,["fs25Motor"]=true,["accHeadlights"]=true };
            if(profile is not ("ams2" or "snowrunner")) host.Telemetry.Publish(new(45,6200,4,.64,0,0,0,8000, GearboxMode:profile=="beamng-default"?"realistic":null,Headlights:1,ActionStates:states,
                Acc:profile=="acc"?new(Enumerable.Range(0,4).Select(i=>new AccWheel(27.5+i*.1,83+i,405+i*8,4,90,95,0)).ToArray(),23,31,90,54,false,true,false,true):null,
                Ets2Navigation:profile is "ets2" or "ats"?new(184,134,80):null,
                F1:profile.StartsWith("f1-")?new(Enumerable.Range(0,4).Select(i=>new F1Wheel(86+i,95,400,23.5,12,0)).ToArray(),110,new Dictionary<string,double>{{"compound",16}},[],null,new(3,5300,15,true,[new(0,"Norris",1,4,1,8,2200,0,2,1,0,0,false),new(1,"Player",1,1,2,8,2100,0,2,1,1000,1000,true),new(2,"Piastri",1,81,3,8,2000,0,2,1,1000,2000,false)])):null));
            await Task.Delay(100);
        }
        } finally { Environment.SetEnvironmentVariable(Fs25GamePaths.UserDataEnvVar,previousData); }
    }
}
