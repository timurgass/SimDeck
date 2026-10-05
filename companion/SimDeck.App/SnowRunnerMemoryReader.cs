using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using SimDeck.Core;

namespace SimDeck.App;

// Layout verified against 1.886173.SNOW_DLC_18. Only QUERY_INFORMATION | VM_READ.
// No injection, game function calls, memory writes or modifications of the game/save files.
public sealed class SnowRunnerMemoryReader : IDisposable
{
    public const string SupportedBuild = "1.886173.SNOW_DLC_18";
    public const string SupportedHash = "198B10794B58B511CFFDFD3FC0F6D5927903FCCD1AA08F0E55DCCC95D00A4A2B";
    SafeProcessHandle? handle;
    Process? process;
    ulong imageBase;
    long retryAt;
    SnowRunnerCatalog catalog = new();
    public string Status { get; private set; } = "Запустите SnowRunner и сядьте в машину.";
    static bool Pointer(ulong p) => p is >=0x10000 and <0x0000800000000000 && (p&7)==0;
    byte[] Read(ulong address,int length)
    {
        if(!Pointer(address) || length is <1 or >65536) throw new InvalidDataException("Invalid SnowRunner address");
        var data=new byte[length];
        if(handle is null || !ReadProcessMemory(handle,address,data,(nuint)length,out var read) || read!=(nuint)length)
            throw new InvalidDataException("SnowRunner memory is no longer available");
        return data;
    }
    ulong Ptr(ulong address) => BinaryPrimitives.ReadUInt64LittleEndian(Read(address,8));
    float Float(ulong address) => BinaryPrimitives.ReadSingleLittleEndian(Read(address,4));
    string String(ulong address)
    {
        var data=Read(address,32); var length=BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(16));
        var capacity=BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(24));
        if(length is <1 or >128 || capacity<length || capacity>4096) return "";
        var bytes=capacity<16?data[..(int)length]:Read(BinaryPrimitives.ReadUInt64LittleEndian(data),(int)length);
        var value=Encoding.UTF8.GetString(bytes);
        return value.All(c => char.IsAsciiLetterOrDigit(c)||c=='_') ? value : "";
    }
    public bool TryRead(out Telemetry? frame)
    {
        frame=null;
        try
        {
            if(process is null || process.HasExited)
            {
                Reset();
                if(Environment.TickCount64<retryAt) return false;
                retryAt=Environment.TickCount64+5000;
                var games=Process.GetProcessesByName("SnowRunner");
                if(games.Length!=1) { foreach(var game in games)game.Dispose(); Status="Запустите один экземпляр SnowRunner.";return false; }
                process=games[0]; var module=process.MainModule ?? throw new InvalidDataException("Game module unavailable");
                using(var file=File.OpenRead(module.FileName))
                    if(Convert.ToHexString(SHA256.HashData(file))!=SupportedHash)
                    { Reset(); Status="Телеметрия этой сборки SnowRunner ещё не проверена. Кнопки доступны отдельно."; return false; }
                imageBase=(ulong)module.BaseAddress;
                var directory=Directory.GetParent(module.FileName)!.Parent!.Parent!.FullName;
                catalog=SnowRunnerCatalog.Load(directory);
                handle=OpenProcess(0x410,false,process.Id);
                if(handle.IsInvalid) throw new InvalidDataException("Read access unavailable");
            }
            var control=Ptr(imageBase+0x2a8eb78);
            if(!Pointer(control)) { Status="Войдите в машину SnowRunner.";return false; }
            if(Ptr(control)!=imageBase+0x222d270) throw new InvalidDataException("SnowRunner controller changed");
            var truck=Ptr(control+8);
            if(!Pointer(truck)) { Status="Войдите в машину SnowRunner.";return false; }
            var addon=Ptr(truck+0x60);
            if(Ptr(addon)!=imageBase+0x2258ab8) throw new InvalidDataException("SnowRunner truck layout changed");
            var begin=Ptr(truck+0x200);var end=Ptr(truck+0x208);
            if(!Pointer(begin)||end<begin||(end-begin)%8!=0||(end-begin)>128) throw new InvalidDataException("Invalid SnowRunner wheels");
            var count=(int)((end-begin)/8);
            var fuel=Float(addon+0x5e8);var capacity=Float(addon+0x5f0);
            var body=Ptr(truck+0x5d0);var velocity=Read(body+0x230,12);
            var vx=BinaryPrimitives.ReadSingleLittleEndian(velocity);var vy=BinaryPrimitives.ReadSingleLittleEndian(velocity.AsSpan(4));var vz=BinaryPrimitives.ReadSingleLittleEndian(velocity.AsSpan(8));
            if(!SnowRunnerTelemetry.Valid(fuel,capacity,vx,vy,vz,count)) throw new InvalidDataException("Invalid SnowRunner sample");
            var components=new List<SnowRunnerComponent>();
            foreach(var (offset,id,name) in new[]{(0x148,"transmission","Коробка"),(0x150,"engine","Двигатель"),(0x158,"fuelTank","Бак"),(0x160,"suspension","Подвеска")})
            {
                var stat=Read(Ptr(truck+(ulong)offset)+0x38,8);
                var component=SnowRunnerTelemetry.Component(id,name,BinaryPrimitives.ReadInt32LittleEndian(stat),BinaryPrimitives.ReadInt32LittleEndian(stat.AsSpan(4)));
                if(component is not null)components.Add(component);
            }
            // Native booleans are single bytes. Adjacent bytes are padding/other fields.
            var states=SnowRunnerTelemetry.ActionStates(Read(addon+0x78,8),Read(Ptr(truck+0x2a8)+0x140,8));
            var suspension=String(truck+0xd10);var definition=catalog.Resolve(suspension,count);
            var wheels=definition?.Wheels ?? Enumerable.Range(0,count).Select(i=>new VehicleWheel(i%2==0?-1:1,i/2)).ToArray();
            var vehicle=new VehicleInfo(definition?.Id??"unknown",definition?.Name??"Машина SnowRunner",definition?.Kind??"unknown",wheels,[],Wear:components.ToDictionary(c=>c.Id,c=>c.DamageFraction));
            if(Ptr(control+8)!=truck) { Status="Смена машины SnowRunner…";return false; }
            frame=new(double.Hypot(vx,vz),0,0,Math.Clamp(fuel/capacity,0,1),0,0,0,FuelLiters:fuel,ActionStates:states,Vehicle:vehicle,
                SnowRunner:new(SupportedBuild,capacity,components.ToArray()));
            Status=$"SnowRunner · {vehicle.Name} · {count} колёс · чтение 5 раз/с";return true;
        }
        catch(Exception ex) when(ex is InvalidDataException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { Reset(); Status="SnowRunner: данные недоступны; кнопки остаются доступны. "+ex.Message;return false; }
    }
    public void Reset() {handle?.Dispose();handle=null;process?.Dispose();process=null;imageBase=0;}
    public void Dispose()=>Reset();
    [DllImport("kernel32.dll",SetLastError=true)] static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(SafeProcessHandle process,ulong address,byte[] data,nuint size,out nuint read);
}
