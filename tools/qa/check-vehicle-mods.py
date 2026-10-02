"""Execute the production Lua bridges with engine API fixtures (pip install lupa==2.8)."""
from pathlib import Path
import json
import struct
from lupa.luajit21 import LuaRuntime

root = Path(__file__).resolve().parents[2]
lua = LuaRuntime(unpack_returned_tuples=True, encoding=None)
lua.execute(b'''
snapshots={}; current=nil
function print(...) end
function addModEventListener(listener) bridge=listener end
function getUserProfileAppPath() return '/fixture/' end
function createXMLFile(...) return {} end
function setXMLString(xml,key,value) xml[key]=value end
setXMLInt=setXMLString;setXMLBool=setXMLString;setXMLFloat=setXMLString
function saveXMLFile(xml) table.insert(snapshots,xml) end
function delete(...) end
function localToLocal(node,root,...) return node.x,0,node.z end
g_localPlayer={getCurrentVehicle=function() return current end}
tool={typeName='seeder',spec_attachable={},spec_turnOnVehicle={},getName=function() return 'Planter' end,getIsLowered=function() return true end,getIsTurnedOn=function() return false end,getFoldAnimTime=function() return .4 end}
tank={typeName='trailer',spec_trailer={},getName=function() return 'Tank' end}
tool.getAttachedImplements=function() return {{object=tank}} end
tank.getAttachedImplements=function() return {{object=tool}} end -- cycle must not recurse
current={typeName='tractor',configFileName='/private/tractor.xml',uniqueId='fixture',rootNode=1,spec_motorized={},getName=function() return 'Tractor' end,getIsMotorStarted=function() return true end,spec_wheels={wheels={{repr={x=-1,z=2}},{repr={x=1,z=2}},{repr={x=-1,z=-2}},{repr={x=1,z=-2}}}},getAttachedImplements=function() return {{object=tool}} end,getSelectedVehicle=function() return tool end}
''')
lua.execute((root / 'mods/FS25_SimDeckStatus/EquipmentCategories.lua').read_bytes())
lua.execute((root / 'mods/FS25_SimDeckStatus/SimDeckStatus.lua').read_bytes())
lua.execute(b'bridge:update(400)')
s=lua.globals().snapshots[1]
assert s[b'simdeckStatus#version']==2 and s[b'simdeckStatus#controlled'] is True
assert s[b'simdeckStatus.vehicle(0)#model']==b'tractor'
assert s[b'simdeckStatus.vehicle(0).wheel(0)#z']==-2
assert s[b'simdeckStatus.vehicle(1)#lowered'] is True
assert s[b'simdeckStatus.vehicle(1)#turnedOn'] is False
assert s[b'simdeckStatus.vehicle(2)#parentId']==b'1'
assert s[b'simdeckStatus.vehicle(3)#id'] is None
lua.execute(b"current={typeName='combine',spec_combine={},getName=function() return 'Combine' end};bridge:update(400)")
assert lua.globals().snapshots[2][b'simdeckStatus.vehicle(0)#kind']==b'combine'
assert lua.globals().snapshots[2][b'simdeckStatus.vehicle(1)#id'] is None
lua.execute(b"header={typeName='cutter',spec_cutter={},getName=function() return 'Header' end};current.getAttachedImplements=function() return {{object=header}} end;bridge:update(400)")
assert lua.globals().snapshots[3][b'simdeckStatus.vehicle(1)#kind']==b'header'
assert lua.globals().snapshots[3][b'simdeckStatus.vehicle(1)#mount']==b'front'
lua.execute(b"current.getAttachedImplements=function() return {} end;bridge:update(400)")
assert lua.globals().snapshots[4][b'simdeckStatus.vehicle(1)#id'] is None
lua.execute(b'current=nil;bridge:update(400)')
assert lua.globals().snapshots[5][b'simdeckStatus#controlled'] is False
assert lua.globals().snapshots[5][b'simdeckStatus.vehicle(0)#id'] is None
print('PASS FS25: nested equipment, cycle bound, wheel axes, class switch, dismount clears state')

manifest=json.loads((root/'assets/fs25-equipment.json').read_text(encoding='utf-8'))
lua.execute(b"shopCategory=nil; g_storeManager={getItemByXMLFilename=function() return {categoryNames={shopCategory}} end}")
for category, expected in manifest['categories'].items():
    # Every store category must override a misleading technical tractor type.
    lua.globals().shopCategory=category.upper().encode()
    lua.execute(b"current={typeName='tractor',configFileName='/fixture.xml'};bridge:update(400)")
    s=lua.globals().snapshots[len(lua.globals().snapshots)]
    assert s[b'simdeckStatus.vehicle(0)#kind']==expected.encode(),(category,expected)
for category,t,expected in [('frontloadertools','baleGrab','balegrab'),('wheelloadertools','dynamicMountAttacherFork','palletfork'),('misc','fuelTrailer','fueltank'),('grapetools','vinePrepruner','vinepruner')]:
    lua.globals().shopCategory=category.encode();lua.globals().fixtureType=t.encode()
    lua.execute(b"current={typeName=fixtureType,configFileName='/fixture.xml',spec_dynamicMountAttacher={}};bridge:update(400)")
    assert lua.globals().snapshots[len(lua.globals().snapshots)][b'simdeckStatus.vehicle(0)#kind']==expected.encode()
lua.execute(b"shopCategory=nil; current=nil; held={typeName='chainsaw'}; g_localPlayer.getHeldHandTool=function() return held end; bridge:update(400)")
assert lua.globals().snapshots[len(lua.globals().snapshots)][b'simdeckStatus.vehicle(0)#kind']==b'chainsaw'
lua.execute(b"held=nil;bridge:update(400)")
assert lua.globals().snapshots[len(lua.globals().snapshots)][b'simdeckStatus#controlled'] is False
for category, capability, expected in [('sprayers','spec_motorized','selfsprayer'),('mowers','spec_motorized','selfmower'),('forageMixers','spec_motorized','selfmixer'),('slurryTanks','spec_motorized','selfslurry'),('balers','spec_motorized','balerdrivable'),('potatoHarvesting','spec_attachable','rootimplement'),('forageHarvesters','spec_attachable','forageimplement')]:
    lua.globals().shopCategory=category.encode();lua.globals().fixtureCapability=capability.encode()
    lua.execute(b"current={typeName='generic',configFileName='/fixture.xml'};current[fixtureCapability]={};bridge:update(400)")
    assert lua.globals().snapshots[len(lua.globals().snapshots)][b'simdeckStatus.vehicle(0)#kind']==expected.encode()
print(f'PASS FS25: {len(manifest["categories"])} store categories, tool refinements, held tool and release')

lua=LuaRuntime(unpack_returned_tuples=True,encoding=None)
lua.execute(b'''
electrics={values={gearIndex=1,rpm=1500,wheelspeed=12,fuel=.5,gearboxMode='realistic',maxrpm=6000,maxGearIndex=12}}
powertrain={getDevicesByType=function() return {} end}
beamstate={hasCouplers=function() return false end};extensions={}
v={data={model='semi',nodes={}},vehicleDirectory='/vehicles/semi/'}
wheels={wheels={}}
for i=0,5 do v.data.nodes[i]={pos={x=i%2==0 and -1 or 1,y=math.floor(i/2)*2-2}};wheels.wheels[i]={node1=i,isPropulsed=i>=2} end
function jsonReadFile(path) return {Name=v.data.model=='semi' and 'T-Series' or 'Unknown model',Brand='Gavril',Type='Car'} end
''')
module=lua.execute((root / 'beamng/mod/lua/vehicle/protocols/simdeckTelemetry.lua').read_bytes())
lua.globals().bridge=module
lua.execute(b"ffi=require('ffi');ffi.cdef('struct SimDeckPacket {'..bridge.getStructDefinition()..'};');packet=ffi.new('struct SimDeckPacket');bridge.fillStruct(packet, .033)")
size=lua.eval(b'ffi.sizeof(packet)'); assert size==448, size
raw=lua.eval(b'ffi.string(packet,ffi.sizeof(packet))')
assert raw[:4]==b'SMD3' and struct.unpack_from('<I',raw,252)[0]==6
assert raw[220:252].split(b'\0')[0]==b'truck'
assert struct.unpack_from('<ffI',raw,256)==(-1,-2,1)
assert struct.unpack_from('<I',raw,264+2*12)[0]==3
lua.execute(b"v.data.model='custom';v.vehicleDirectory='/vehicles/custom/';wheels.wheels={};bridge.fillStruct(packet,.033)")
raw=lua.eval(b'ffi.string(packet,ffi.sizeof(packet))')
assert raw[60:124].split(b'\0')[0]==b'custom'
assert struct.unpack_from('<I',raw,252)[0]==0
print('PASS BeamNG: actual FFI 448-byte packet, truck wheel geometry, drive flags, identity refresh')
