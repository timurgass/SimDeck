using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using SimDeck.Core;

namespace SimDeck.App;

public sealed partial class TruckGameGpsReader
{
    ulong mapObject,mapVtable;
    // The map UI is read only. A pin is edited through the game's ordinary
    // mouse handler, never by altering memory or calling an internal function.
    public bool TryMapView(out TruckMapView? view,out Func<bool> unchanged)
    {
        view=null;unchanged=()=>false;
        try
        {
            if(handle is null||pid==0)return false;
            using var process=Process.GetProcessById(pid);var module=process.MainModule!;
            if(FileVersionInfo.GetVersionInfo(module.FileName).FileMinorPart!=61)return false;
            if(mapVtable==0)mapVtable=(ulong)module.BaseAddress+ResolveMapVtable(File.ReadAllBytes(module.FileName));
            byte[]? data=null;
            if(mapObject!=0)try{data=ReadMap(mapObject);}catch(InvalidDataException){mapObject=0;}
            if(mapObject==0)
            {
                var until=Environment.TickCount64+18000;ulong address=0;
                var pattern=BitConverter.GetBytes(mapVtable);
                while(Environment.TickCount64<until&&VirtualQueryEx(handle,address,out var region,(nuint)Marshal.SizeOf<MemoryRegion>())!=0)
                {
                    var end=region.Base+region.Size;if(end<=address)break;address=end;
                    if(region.State!=0x1000||(region.Protect&0x100)!=0||(region.Protect&0xff) is not (4 or 8)||region.Type!=0x20000)continue;
                    for(ulong at=region.Base;at<end&&Environment.TickCount64<until;at+=999992)
                    {
                        byte[] block;try{block=Read(at,(int)Math.Min(1000000,end-at));}catch(InvalidDataException){continue;}
                        var offset=0;
                        while(offset<=block.Length-8)
                        {
                            var found=block.AsSpan(offset).IndexOf(pattern);if(found<0)break;offset+=found;
                            var candidate=at+(ulong)offset;offset+=8;if(!Pointer(candidate))continue;
                            try{data=ReadMap(candidate);mapObject=candidate;break;}catch(InvalidDataException){}
                        }
                        if(mapObject!=0)break;
                    }
                    if(mapObject!=0)break;
                }
            }
            if(data is null||mapObject==0)return false;
            var saved=data;var objectAddress=mapObject;
            float F(int o)=>BinaryPrimitives.ReadSingleLittleEndian(saved.AsSpan(o));
            float[] Floats(int o,int count)=>Enumerable.Range(0,count).Select(i=>F(o+i*4)).ToArray();
            view=new(F(0x18),F(0x1c),F(0x20),F(0x24),Floats(0x1a8,3),Floats(0x1d0,4),Floats(0x7dc,16));
            unchanged=()=>{
                try{var now=ReadMap(objectAddress);return now.AsSpan(0x18,16).SequenceEqual(saved.AsSpan(0x18,16))&&
                    now.AsSpan(0x1a8,12).SequenceEqual(saved.AsSpan(0x1a8,12))&&now.AsSpan(0x1d0,24).SequenceEqual(saved.AsSpan(0x1d0,24))&&now.AsSpan(0x7dc,64).SequenceEqual(saved.AsSpan(0x7dc,64));}
                catch(InvalidDataException){return false;}
            };
            return true;
        }
        catch(Exception e) when(e is IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException){return false;}
    }
    byte[] ReadMap(ulong address)
    {
        var d=Read(address,0x860);
        float F(int o)=>BinaryPrimitives.ReadSingleLittleEndian(d.AsSpan(o));
        if(new[]{0x18,0x1c,0x20,0x24,0x1a8,0x1ac,0x1b0,0x1d0,0x1d4,0x1d8,0x1dc}.Any(o=>!float.IsFinite(F(o)))||
            BinaryPrimitives.ReadUInt64LittleEndian(d)!=mapVtable||F(0x18)<0||F(0x1c)>1600||F(0x1c)-F(0x18)<1000||
            F(0x20)<0||F(0x24)>900||F(0x24)-F(0x20)<600||F(0x1ac)<100||F(0x1ac)>100000||
            Enumerable.Range(0,16).Any(i=>!float.IsFinite(F(0x7dc+i*4))))throw new InvalidDataException("Map is not open");
        return d;
    }
    public static ulong ResolveMapVtable(byte[] executable)
    {
        using var pe=new PEReader(new MemoryStream(executable));var headers=pe.PEHeaders;var imageBase=headers.PEHeader!.ImageBase;
        ulong Va(int raw){foreach(var s in headers.SectionHeaders)if(raw>=s.PointerToRawData&&raw<s.PointerToRawData+s.SizeOfRawData)return imageBase+(ulong)(s.VirtualAddress+raw-s.PointerToRawData);throw new InvalidDataException("Map descriptor outside image");}
        int[] References(ulong target){var p=BitConverter.GetBytes(target);var list=new List<int>();var o=0;while(o<=executable.Length-8){var n=executable.AsSpan(o).IndexOf(p);if(n<0)break;o+=n;list.Add(o);o+=8;}return list.ToArray();}
        var name=executable.AsSpan().IndexOf(Encoding.ASCII.GetBytes("ui_world_map\0"));if(name<0)throw new InvalidDataException("Unknown map class");
        var descriptors=References(Va(name)).SelectMany(p=>References(Va(p))).Select(Va).ToHashSet();
        var text=headers.SectionHeaders.Single(s=>s.Name==".text");var code=executable.AsSpan(text.PointerToRawData,text.SizeOfRawData).ToArray();
        var getters=Find(code,"48 8d 05 ? ? ? ? c3").Where(i=>descriptors.Contains((ulong)((long)imageBase+text.VirtualAddress+i+7+BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan(i+3)))));
        var tables=getters.SelectMany(i=>References(imageBase+(ulong)(text.VirtualAddress+i))).Where(o=>o>=40).Select(o=>Va(o)-40-imageBase).Distinct().ToArray();
        if(tables.Length!=1)throw new InvalidDataException("Ambiguous map class");return tables[0];
    }
    [StructLayout(LayoutKind.Sequential)]struct MemoryRegion
    {public ulong Base,Allocation;public uint AllocationProtect,Alignment;public ulong Size;public uint State,Protect,Type,Alignment2;}
    [DllImport("kernel32.dll")]static extern nuint VirtualQueryEx(Microsoft.Win32.SafeHandles.SafeProcessHandle process,ulong address,out MemoryRegion info,nuint length);
}
