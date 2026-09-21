-- SimDeck telemetry v2. Local UDP only; no vehicle control or game-file overrides.
local M = {}
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
  ]]
end
local function fraction(value) return math.min(1, math.max(0, tonumber(value) or 0)) end
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
  packet.magic = "SMD2"
  packet.version = 2
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
end
return M
