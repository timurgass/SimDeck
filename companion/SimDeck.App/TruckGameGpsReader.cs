using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using SimDeck.Core;

namespace SimDeck.App;

// Read-only Prism layouts/signatures researched from ETS2LA/ets2la_plugin,
// MIT Copyright (c) 2024 Dario Wouters. See tools/truck-gps/NOTICE.md.
// Never calls game functions, loads a DLL, or writes process memory.
public sealed class TruckGameGpsReader : IDisposable
{
    SafeProcessHandle? handle;
    int pid;
    ulong instanceAddress;
    uint gpsOffset;
    long retryAt;
    public string Status { get; private set; }="Запустите выбранную игру";
    static bool Pointer(ulong p)=>p is >=0x10000 and <0x0000800000000000 && (p&7)==0;
    byte[] Read(ulong address,int length)
    {
        if(address is <0x10000 or >=0x0000800000000000||length is <1 or >1000000)throw new InvalidDataException("Invalid GPS memory range");
        var b=new byte[length];
        if(handle is null||!ReadProcessMemory(handle,address,b,(nuint)length,out var read)||read!=(nuint)length)
            throw new InvalidDataException("GPS memory unavailable");
        return b;
    }
    ulong Ptr(ulong address)=>BinaryPrimitives.ReadUInt64LittleEndian(Read(address,8));
    public bool TryRead(string profile,out TruckGpsNode[] nodes)=>TryRead(profile,out nodes,out _);
    public bool TryRead(string profile,out TruckGpsNode[] nodes,out TruckGpsWaypoint[] waypoints)
    {
        nodes=[];waypoints=[];
        if(profile is not ("ats" or "ets2")){Status="Неизвестная игра";return false;}
        try
        {
            var name=profile=="ats"?"amtrucks":"eurotrucks2";
            var games=Process.GetProcessesByName(name);
            using var process=games.Length==1?games[0]:null;
            foreach(var p in games)if(p!=process)p.Dispose();
            if(process is null){Reset();Status="Запустите выбранную игру";return false;}
            if(process.Id!=pid)
            {
                if(Environment.TickCount64<retryAt)return false;
                Reset();retryAt=Environment.TickCount64+10000;
                var module=process.MainModule??throw new InvalidDataException("Game module unavailable");
                var v=FileVersionInfo.GetVersionInfo(module.FileName);
                if(v.FileMajorPart!=1||v.FileMinorPart is <59 or >61){Status="Чтение GPS для этой версии игры ещё не проверено";return false;}
                var resolved=Resolve(File.ReadAllBytes(module.FileName),v.FileMinorPart);
                instanceAddress=(ulong)module.BaseAddress+resolved.InstanceRva;gpsOffset=resolved.GpsOffset;
                handle=OpenProcess(0x1010,false,process.Id); // QUERY_LIMITED_INFORMATION | VM_READ
                if(handle.IsInvalid)throw new InvalidDataException("Read access unavailable");
                pid=process.Id;
            }
            var controller=Ptr(instanceAddress);
            if(!Pointer(controller)){Status="Войдите в грузовик";return false;}
            // Validate the controller class using its descriptor getter's code;
            // decode the address rather than invoking the virtual function.
            var vtable=Ptr(controller);var getter=Ptr(vtable+5*8);var code=Read(getter,16);
            if(code[0]==0xe9){getter=(ulong)((long)getter+5+BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(1)));code=Read(getter,16);}
            if(!code.AsSpan(0,3).SequenceEqual(new byte[]{0x48,0x8d,0x05})||code[7]!=0xc3)
                throw new InvalidDataException("Unknown controller descriptor");
            var descriptor=(ulong)((long)getter+7+BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(3)));
            var className=Ptr(Ptr(descriptor));
            if(Encoding.ASCII.GetString(Read(className,16)).Split('\0')[0]!="game_ctrl")
            {Status="GPS доступен после загрузки заезда";return false;}
            var route=Ptr(controller+gpsOffset+0x28);
            if(route==0){Status="В игровом GPS маршрут не задан";return true;}
            var waypointAddress=controller+gpsOffset+0x1f8;
            var waypointHeader=Read(waypointAddress,24);
            var waypointItems=BinaryPrimitives.ReadUInt64LittleEndian(waypointHeader.AsSpan(8));
            var waypointCount=BinaryPrimitives.ReadUInt64LittleEndian(waypointHeader.AsSpan(16));
            if(waypointCount>64||waypointCount>0&&!Pointer(waypointItems))throw new InvalidDataException("Invalid GPS waypoint count");
            var waypointData=waypointCount==0?Array.Empty<byte>():Read(waypointItems,checked((int)waypointCount*24));
            var targets=new List<TruckGpsWaypoint>();
            for(var i=0;i<(int)waypointCount;i++)
            {
                var node=BinaryPrimitives.ReadUInt64LittleEndian(waypointData.AsSpan(i*24+8));var n=Read(node,56);
                var uid=BinaryPrimitives.ReadUInt64LittleEndian(n.AsSpan(48));
                var x=BinaryPrimitives.ReadInt32LittleEndian(n)/256f;var z=BinaryPrimitives.ReadInt32LittleEndian(n.AsSpan(8))/256f;
                if(uid==0||Math.Abs(x)>1000000||Math.Abs(z)>1000000)throw new InvalidDataException("Invalid GPS waypoint");
                targets.Add(new(uid.ToString(),x,z));
            }
            var header=Read(route+0x50,24);var items=BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(8));var count=BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(16));
            if(count>6000||count>0&&!Pointer(items))throw new InvalidDataException("Invalid GPS node count");
            if(count==0){Status="В игровом GPS маршрут не задан";return true;}
            var data=Read(items,checked((int)count*32));var list=new List<TruckGpsNode>();
            for(var i=0;i<(int)count;i++)
            {
                var b=data.AsSpan(i*32,32);var node=BinaryPrimitives.ReadUInt64LittleEndian(b);var n=Read(node,56);
                var uid=BinaryPrimitives.ReadUInt64LittleEndian(n.AsSpan(48));
                var x=BinaryPrimitives.ReadInt32LittleEndian(n)/256f;var z=BinaryPrimitives.ReadInt32LittleEndian(n.AsSpan(8))/256f;
                var distance=BinaryPrimitives.ReadSingleLittleEndian(b[20..]);var seconds=BinaryPrimitives.ReadSingleLittleEndian(b[24..]);
                if(uid==0||Math.Abs(x)>1000000||Math.Abs(z)>1000000||!float.IsFinite(distance)||distance is <0 or >10000000||!float.IsFinite(seconds)||seconds is <0 or >10000000)
                    throw new InvalidDataException("Invalid GPS node");
                list.Add(new(uid.ToString(),x,z,distance,seconds));
            }
            if(Ptr(controller+gpsOffset+0x28)!=route||!Read(route+0x50,24).SequenceEqual(header)||!Read(items,data.Length).SequenceEqual(data)||
                !Read(waypointAddress,24).SequenceEqual(waypointHeader)||(waypointData.Length>0&&!Read(waypointItems,waypointData.Length).SequenceEqual(waypointData)))
            {Status="Игра перестраивает маршрут";return false;}
            nodes=list.ToArray();waypoints=targets.ToArray();Status="Узлы маршрута прочитаны из GPS игры";return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException or OverflowException)
        {Status="Чтение GPS недоступно: "+ex.Message;return false;}
    }
    public static (ulong InstanceRva,uint GpsOffset) Resolve(byte[] executable,int minor)
    {
        using var pe=new PEReader(new MemoryStream(executable));
        var section=pe.PEHeaders.SectionHeaders.Single(s=>s.Name==".text");
        var text=executable.AsSpan(section.PointerToRawData,section.SizeOfRawData).ToArray();
        var bases=Find(text,minor>=61?"48 8b ? ? ? ? ? 48 8b ? 48 8b ? 48 8b 80":"48 8b 15 ? ? ? ? 48 8b ? 48 8b 41 ? 48 8b 92");
        var offsets=bases.Where(i=>(text[i+2]&0xc7)==0x05).Select(i=>(long)section.VirtualAddress+i+7+BinaryPrimitives.ReadInt32LittleEndian(text.AsSpan(i+3))).Distinct().ToArray();
        var gps=Find(text,"48 8d 88 ? ? ? ? 48 85 c9 74 ? 48 8b 01 48 8d 54").Select(i=>BinaryPrimitives.ReadUInt32LittleEndian(text.AsSpan(i+3))).Distinct().ToArray();
        var image=pe.PEHeaders.PEHeader?.SizeOfImage??0;
        if(offsets.Length!=1||gps.Length!=1||offsets[0]<0||offsets[0]+8>image||gps[0] is <0x1000 or >0x10000)
            throw new InvalidDataException("Сигнатуры GPS не совпадают с поддерживаемой версией игры");
        return((ulong)offsets[0],gps[0]);
    }
    public static int[] Find(byte[] data,string pattern)
    {
        var p=pattern.Split(' ').Select(v=>v=="?"?-1:Convert.ToInt32(v,16)).ToArray();var found=new List<int>();
        for(var i=0;i<=data.Length-p.Length;i++){var match=true;for(var j=0;j<p.Length;j++)if(p[j]>=0&&data[i+j]!=p[j]){match=false;break;}if(match)found.Add(i);}
        return found.ToArray();
    }
    void Reset(){handle?.Dispose();handle=null;pid=0;instanceAddress=0;gpsOffset=0;}
    public void Dispose()=>Reset();
    [DllImport("kernel32.dll",SetLastError=true)]static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int id);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(SafeProcessHandle process,ulong address,[Out]byte[] buffer,nuint size,out nuint read);
}
