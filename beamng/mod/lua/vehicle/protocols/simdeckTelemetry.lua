-- SimDeck telemetry v3. Local UDP only; no vehicle control or game-file overrides.
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
function M.fillStruct(packet, dt)
  local e = electrics.values
  -- Zeroed packets before the controller initializes intentionally have no signature.
  if e.gearIndex == nil or e.rpm == nil then return end
  packet.magic = "SMD3"
  packet.version = 3
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
      item.x, item.z = pos.x, pos.y
      item.flags = type(wheel.isPropulsed) == "boolean" and (wheel.isPropulsed and 3 or 1) or 0
      packet.wheelCount = packet.wheelCount + 1
    end
  end
end
return M
