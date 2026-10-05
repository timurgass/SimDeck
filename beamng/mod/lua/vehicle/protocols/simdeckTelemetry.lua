-- SimDeck telemetry v4. Local UDP only; no vehicle control or game-file overrides.
local M = {}
local metadata
function M.getAddress() return "127.0.0.1" end
function M.getPort() return 4444 end
function M.getMaxUpdateRate() return 30 end
function M.isPhysicsStepUsed() return false end
function M.getStructDefinition()
  return [[
    char magic[4];
    unsigned int version;
    float speed;
    float rpm;
    int gear;
    unsigned int gearboxMode;
    float fuel;
    float throttle;
    float brake;
    float clutch;
    float maxRpm;
    int maxGear;
    unsigned int stateKnown;
    unsigned int stateActive;
    int headlights;
    char model[64];
    char name[96];
    char category[32];
    unsigned int wheelCount;
    struct { float x; float z; unsigned int flags; } wheels[16];
    float bodyDamage[6];
    unsigned int faultKnown;
    unsigned int faultActive;
    float coolantTemperature;
    float oilTemperature;
    struct { char name[24]; unsigned int known; unsigned int active; float brakeTemperature; } wheelDamage[16];
    unsigned int partCount;
    int totalDamagedParts;
    struct { char name[64]; float damage; } parts[8];
  ]]
end
local function fraction(value) return math.min(1, math.max(0, tonumber(value) or 0)) end
local function utf8Bound(s, limit)
  if #s <= limit then return s end
  local start = limit
  while start > 0 and s:byte(start) >= 128 and s:byte(start) < 192 do start = start - 1 end
  if start == 0 then return "" end
  local b = s:byte(start)
  local width = b >= 240 and 4 or b >= 224 and 3 or b >= 192 and 2 or 1
  return s:sub(1, start + width - 1 > limit and start - 1 or limit)
end
local function isOn(value) return value == true or (type(value) == "number" and value > 0) end
local function state(packet, index, value)
  if value == nil then return end
  local flag = 2 ^ index
  packet.stateKnown = packet.stateKnown + flag
  if isOn(value) then packet.stateActive = packet.stateActive + flag end
end
local function diffState()
  local known, active = false, false
  for _, device in pairs(powertrain.getDevicesByType("differential")) do
    local hasLock = false
    for _, mode in ipairs(device.availableModes or {}) do if mode == "locked" then hasLock = true end end
    if hasLock and #(device.availableModes or {}) > 1 then
      known = true
      active = active or device.mode == "locked"
    end
  end
  if known then return active end
end
local function couplingState()
  if not beamstate.hasCouplers() then return nil end
  for id, c in pairs(beamstate.couplerCache or {}) do
    if c.couplerTag and not c.couplerLock and not c.couplerWeld and beamstate.attachedCouplers[id] then return true end
  end
  if extensions.couplings and extensions.couplings.isCouplerAttached then return extensions.couplings.isCouplerAttached() end
  return false
end

local diagnostic, diagnosticTimer = nil, .2
local faults = {"engineDisabled", "engineLockedUp", "engineReducedTorque", "catastrophicOverrevDamage", "mildOverrevDamage",
  "engineHydrolocked", "impactDamage", "radiatorLeak", "oilpanLeak", "headGasketDamaged", "pistonRingsDamaged",
  "rodBearingsDamaged", "coolantOverheating", "oilOverheating", "transmissionBroken"}
local function readDamage(group, key)
  if not damageTracker or type(damageTracker.getDamage) ~= "function" then return nil end
  return isOn(damageTracker.getDamage(group, key))
end
local function diagnostics()
  local result = {body={}, known=0, active=0, coolant=-1, oil=-1, parts={}, total=-1}
  for i, key in ipairs({"FL", "FR", "ML", "MR", "RL", "RR"}) do
    local value = damageTracker and type(damageTracker.getDamage)=="function" and damageTracker.getDamage("body", key)
    result.body[i] = type(value)=="number" and value==value and fraction(value) or -1
  end
  local engines = powertrain.getDevicesByType("combustionEngine") or {}
  local engine = next(engines) and select(2,next(engines))
  local thermals = engine and engine.thermals
  local engineData = engine and v.data[engine.name] or {}
  if thermals then
    result.coolant = tonumber(thermals.coolantTemperature) or -1
    result.oil = tonumber(thermals.oilTemperature) or -1
  end
  local hasRadiator = thermals and result.coolant>=0 and (not tonumber(engineData.radiatorArea) or engineData.radiatorArea>0)
  for i, key in ipairs(faults) do
    local value
    if i==15 then
      if type(powertrain.getDevicesByCategory)=="function" then
        for _, device in pairs(powertrain.getDevicesByCategory("gearbox") or {}) do
          if type(device.isBroken)=="boolean" then value=(value or false) or device.isBroken end
        end
      end
    elseif engine and (i<8 or thermals and (i~=8 and i~=13 or hasRadiator)) then value=readDamage("engine",key) end
    if value~=nil then
      local bit=2^(i-1); result.known=result.known+bit
      if value then result.active=result.active+bit end
    end
  end
  if beamstate and type(beamstate.getPartDamageData)=="function" then
    result.total=0
    for _, part in pairs(beamstate.getPartDamageData() or {}) do
      local score=tonumber(part.damage)
      if score and score==score and score>0 then
        result.total=result.total+1
        result.parts[#result.parts+1]={name=utf8Bound(tostring(part.name or "Detail"),63), damage=fraction(score)}
      end
    end
    table.sort(result.parts,function(a,b) return a.damage==b.damage and a.name<b.name or a.damage>b.damage end)
    while #result.parts>8 do table.remove(result.parts) end
  end
  return result
end

function M.fillStruct(packet, dt)
  local e = electrics.values
  -- Zeroed packets before the controller initializes intentionally have no signature.
  if e.gearIndex == nil or e.rpm == nil then return end
  packet.magic = "SMD4"
  packet.version = 4
  packet.speed = math.abs(e.wheelspeed or e.airspeed or 0)
  packet.rpm = math.max(0, e.rpm)
  packet.gear = e.gearIndex
  packet.gearboxMode = e.gearboxMode == "arcade" and 1 or e.gearboxMode == "realistic" and 2 or 0
  packet.fuel = fraction(e.fuel)
  packet.throttle = fraction(e.throttle)
  packet.brake = fraction(e.brake)
  packet.clutch = fraction(e.clutch)
  packet.maxRpm = math.max(0, e.maxrpm or 0)
  packet.maxGear = math.max(0, e.maxGearIndex or 0)
  packet.stateKnown = 0
  packet.stateActive = 0
  -- Use requested signal states, not blink pulses: a selected button stays down.
  state(packet, 0, e.hazard_enabled)
  state(packet, 1, e.signal_left_input)
  state(packet, 2, e.signal_right_input)
  state(packet, 3, e.fog)
  state(packet, 4, e.mode4WD)
  state(packet, 5, e.modeRangeBox)
  state(packet, 6, diffState())
  state(packet, 7, couplingState())
  if e.ignitionLevel ~= nil then state(packet, 8, e.ignitionLevel >= 2) end
  state(packet, 9, e.lightbar)
  packet.headlights = -1
  -- lights_state is the authoritative selector, also used by BeamNG's radial menu.
  if e.lights_state == 0 or e.lights_state == 1 or e.lights_state == 2 then
    packet.headlights = e.lights_state
  elseif e.highbeam ~= nil or e.lowbeam ~= nil then
    packet.headlights = isOn(e.highbeam) and 2 or (isOn(e.lowbeam) or isOn(e.lowhighbeam)) and 1 or 0
  end
  local model = tostring(v.data.model or (v.vehicleDirectory or ""):match("vehicles/([^/]+)") or "")
  if not metadata or metadata.model ~= model then
    local info = {}
    if type(jsonReadFile) == "function" and type(v.vehicleDirectory) == "string" then
      local ok, result = pcall(jsonReadFile, v.vehicleDirectory .. "info.json")
      if ok and type(result) == "table" then info = result end
    end
    local name = tostring(info.Name or (v.data.information and v.data.information.name) or model)
    if info.Brand then name = tostring(info.Brand) .. " " .. name end
    local category = info["Body Style"] or info.Type or "unknown"
    -- Only explicit stock model identifiers supplement broad metadata categories.
    local catalog = { semi="truck", pickup="pickup", van="van", citybus="bus", roamer="suv" }
    metadata = { model=model, name=name, category=tostring(catalog[model] or category) }
  end
  -- ASCII identifiers and bounded UTF-8 display names; incomplete UTF-8 is rejected by Companion.
  packet.model = utf8Bound(metadata.model, 63)
  packet.name = utf8Bound(metadata.name, 95)
  packet.category = utf8Bound(metadata.category, 31)
  packet.wheelCount = 0
  for _, wheel in pairs(wheels.wheels or {}) do
    if packet.wheelCount >= 16 then break end
    local node = v.data.nodes and v.data.nodes[wheel.node1]
    local pos = node and node.pos
    if pos and type(pos.x) == "number" and type(pos.y) == "number" and pos.x == pos.x and pos.y == pos.y then
      local item = packet.wheels[packet.wheelCount]
      item.x, item.z = -pos.x, pos.y
      item.flags = type(wheel.isPropulsed) == "boolean" and (wheel.isPropulsed and 3 or 1) or 0
      local detail = packet.wheelDamage[packet.wheelCount]
      detail.name = utf8Bound(tostring(wheel.name or ""),23)
      detail.known, detail.active = 0, 0
      local brakeDamage
      if (tonumber(wheel.initialBrakeTorque or wheel.brakeTorque) or 0)>0 then brakeDamage=readDamage("wheels","brake"..tostring(wheel.name)) end
      local values={wheel.isBroken, wheel.isTireDeflated, brakeDamage}
      -- Do not iterate with ipairs: unknown fields may leave holes.
      for index=1,3 do
        local value=values[index]
        if type(value)=="boolean" then
          local bit=2^(index-1);detail.known=detail.known+bit
          if value then detail.active=detail.active+bit end
        end
      end
      detail.brakeTemperature=tonumber(wheel.brakeSurfaceTemperature) or -1
      packet.wheelCount = packet.wheelCount + 1
    end
  end
  diagnosticTimer=diagnosticTimer+(tonumber(dt) or 0)
  if not diagnostic or diagnosticTimer>=.2 then
    diagnosticTimer=0
    local ok, value=pcall(diagnostics)
    diagnostic=ok and value or {body={-1,-1,-1,-1,-1,-1},known=0,active=0,coolant=-1,oil=-1,parts={},total=-1}
  end
  for i=0,5 do packet.bodyDamage[i]=diagnostic.body[i+1] end
  packet.faultKnown,packet.faultActive=diagnostic.known,diagnostic.active
  packet.coolantTemperature,packet.oilTemperature=diagnostic.coolant,diagnostic.oil
  packet.partCount,packet.totalDamagedParts=#diagnostic.parts,diagnostic.total
  for i,part in ipairs(diagnostic.parts) do packet.parts[i-1].name=part.name;packet.parts[i-1].damage=part.damage end
end
return M
