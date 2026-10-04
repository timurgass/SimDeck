-- Optional local bridge. Reads only the player's currently controlled machine.
-- Missing capabilities are omitted, never guessed from the last button press.
local SimDeckStatus = { elapsed = 0, sequence = 0, reportedWrite = false }
print("SimDeckStatus 1.5.0: script loaded")

local function status(object, method)
    if object ~= nil and type(object[method]) == "function" then
        local ok, value = pcall(object[method], object)
        if ok and type(value) == "boolean" then return value end
    end
    return nil
end

local function value(object, method)
    if object ~= nil and type(object[method]) == "function" then
        local ok, result = pcall(object[method], object)
        if ok then return result end
    end
end

-- GIANTS FS25 Lights/Drivable/Cover/Pipe/Foldable specializations.
-- No state is inferred from keyboard commands or animation blink phases.
local function boolAttr(xml, name, state)
    if type(state) == "boolean" then setXMLBool(xml, "simdeckStatus#" .. name, state) end
end
local function paused()
    local state = status(g_currentMission, "getIsPaused")
    if state ~= nil then return state end
    if g_currentMission ~= nil and type(g_currentMission.paused) == "boolean" then return g_currentMission.paused end
    return nil
end
-- The pause notification also runs when simulation updates are suspended.
-- Only subscribe if this version of the game publishes the event.
function SimDeckStatus:onPauseGameChange(isPaused)
    if type(isPaused) ~= "boolean" then return end
    self.confirmedPause = isPaused
    self.elapsed = 200
    self:update(0)
end
function SimDeckStatus:loadMap()
    self.confirmedPause = nil
    if g_messageCenter ~= nil and MessageType ~= nil and MessageType.PAUSE ~= nil then
        g_messageCenter:subscribe(MessageType.PAUSE, self.onPauseGameChange, self)
    end
end
function SimDeckStatus:deleteMap()
    if g_messageCenter ~= nil and type(g_messageCenter.unsubscribeAll) == "function" then g_messageCenter:unsubscribeAll(self) end
end
local function writePause(xml)
    local current = SimDeckStatus.confirmedPause
    if current == nil then current = paused() end
    boolAttr(xml, "paused", current)
end
local function machines(root)
    local list, seen = {}, {}
    local function visit(v, depth)
        if v == nil or seen[v] or depth > 8 or #list >= 32 then return end
        seen[v] = true; list[#list + 1] = v
        for _, entry in ipairs(value(v, "getAttachedImplements") or {}) do visit(entry.object, depth + 1) end
    end
    visit(root, 0)
    return list
end
local function uniform(list, read)
    local result = nil
    for _, object in ipairs(list) do
        local state = read(object)
        if type(state) == "boolean" then
            if result ~= nil and state ~= result then return nil end
            result = state
        end
    end
    return result
end
local function foldState(v)
    if v.spec_foldable ~= nil and v.spec_foldable.hasFoldingParts == true then return status(v, "getIsUnfolded") end
end
local function extraStates(xml, vehicle, implement)
    writePause(xml)
    local all = machines(vehicle)
    boolAttr(xml, "loweredAll", uniform(all, function(v)
        if status(v, "getAllowsLowering") == true then return status(v, "getIsLowered") end
    end))
    boolAttr(xml, "turnedOnAll", uniform(all, function(v)
        if v.spec_turnOnVehicle ~= nil then return status(v, "getIsTurnedOn") end
    end))
    boolAttr(xml, "unfoldedAll", uniform(all, foldState))
    if vehicle.spec_lights ~= nil and Lights ~= nil then
        local mask = value(vehicle, "getLightsTypesMask")
        if type(mask) == "number" and mask >= 0 and mask == math.floor(mask) then
            for _, pair in ipairs({{"lights", "LIGHT_TYPE_DEFAULT"}, {"highBeam", "LIGHT_TYPE_HIGHBEAM"},
                {"workLightFront", "LIGHT_TYPE_WORK_FRONT"}, {"workLightBack", "LIGHT_TYPE_WORK_BACK"}}) do
                local bit = Lights[pair[2]]
                if type(bit) == "number" and bit >= 0 and bit <= 30 then
                    boolAttr(xml, pair[1], math.floor(mask / 2 ^ bit) % 2 == 1)
                end
            end
        end
        boolAttr(xml, "beacon", status(vehicle, "getBeaconLightsVisibility"))
        local turn = value(vehicle, "getTurnLightState")
        if type(turn) == "number" and Lights.TURNLIGHT_LEFT ~= nil and Lights.TURNLIGHT_RIGHT ~= nil and Lights.TURNLIGHT_HAZARD ~= nil then
            boolAttr(xml, "turnLeft", turn == Lights.TURNLIGHT_LEFT or turn == Lights.TURNLIGHT_HAZARD)
            boolAttr(xml, "turnRight", turn == Lights.TURNLIGHT_RIGHT or turn == Lights.TURNLIGHT_HAZARD)
            boolAttr(xml, "hazard", turn == Lights.TURNLIGHT_HAZARD)
        end
    end
    local cruise = value(vehicle, "getCruiseControlState")
    if type(cruise) == "number" and Drivable ~= nil and Drivable.CRUISECONTROL_STATE_OFF ~= nil then
        boolAttr(xml, "cruise", cruise ~= Drivable.CRUISECONTROL_STATE_OFF)
    end
    local cover = implement.spec_cover
    if cover ~= nil and cover.hasCovers == true and type(cover.state) == "number" then boolAttr(xml, "coverOpen", cover.state > 0) end
    local pipeOwner = implement.spec_pipe ~= nil and implement or vehicle
    local pipe = pipeOwner.spec_pipe
    if pipe ~= nil and pipe.hasMovablePipe == true then
        local state = value(pipeOwner, "getCurrentPipeState")
        -- State 0 means the pipe is moving; do not misreport this as retracted.
        if type(state) == "number" and state >= 1 then boolAttr(xml, "pipeOut", state ~= 1) end
    end
    local combine = (implement.spec_combine ~= nil and implement or vehicle).spec_combine
    if combine ~= nil and type(combine.isSwathActive) == "boolean" then boolAttr(xml, "chopper", not combine.isSwathActive) end
    local helper = vehicle.spec_aiFieldWorker
    if helper ~= nil then boolAttr(xml, "helper", helper.isActive) end
    if implement.spec_sprayer ~= nil then boolAttr(xml, "doubleSpray", implement.spec_sprayer.doubledAmountIsActive) end
end
local function text(s)
    s = tostring(s or ""):gsub("[%z\1-\31]", "")
    if #s <= 160 then return s end
    local start = 160
    while start > 0 and s:byte(start) >= 128 and s:byte(start) < 192 do start = start - 1 end
    if start == 0 then return "" end
    local b = s:byte(start)
    local width = b >= 240 and 4 or b >= 224 and 3 or b >= 192 and 2 or 1
    return s:sub(1, start + width - 1 > 160 and start - 1 or 160)
end
local function kind(vehicle)
    local t = tostring(vehicle.typeName or ""):lower()
    -- Store categories distinguish machines that share a technical type (e.g.
    -- wheel loaders and trucks both use 'tractor'). Use the live shop registry.
    local item
    if g_storeManager ~= nil and type(g_storeManager.getItemByXMLFilename) == "function" then
        local ok, result = pcall(g_storeManager.getItemByXMLFilename, g_storeManager, vehicle.configFileName)
        if ok then item = result end
    end
    local categories = type(item)=="table" and type(item.categoryNames)=="table" and item.categoryNames or {}
    local category = table.concat(categories, " "):lower()
    -- Shared tool categories need a more specific capability before their default.
    if category:find("tools", 1, true) or category:find("misc", 1, true) or category:find("grape", 1, true) then
        if vehicle.spec_dynamicMountAttacher ~= nil and t:find("fork", 1, true) then return "palletfork" end
        if t:find("balegrab", 1, true) then return "balegrab" end
        if t:find("loggrab", 1, true) then return "loggrab" end
        if t:find("shovel", 1, true) then return "bucket" end
        if t:find("vineprepruner", 1, true) then return "vinepruner" end
        if vehicle.spec_plow ~= nil then return "plow" end
        if vehicle.spec_cultivator ~= nil then return "cultivator" end
        if vehicle.spec_mulcher ~= nil then return "mulcher" end
        if vehicle.spec_sprayer ~= nil then return "fronttank" end
        if vehicle.spec_manureSpreader ~= nil then return "manurespreader" end
        if t:find("saltspreader", 1, true) then return "saltspreader" end
        if t:find("fueltrailer", 1, true) then return "fueltank" end
        if t:find("locomotive", 1, true) then return "train" end
        if t:find("highpressurewasher", 1, true) then return "washer" end
        if t:find("handtoolmower", 1, true) then return "handmower" end
    end
    for _, name in ipairs(categories) do
        local mapped = SimDeckEquipmentCategories and SimDeckEquipmentCategories[tostring(name):lower()]
        if mapped ~= nil then
            if vehicle.spec_motorized ~= nil then
                if mapped == "sprayer" then return "selfsprayer" end
                if mapped == "mower" then return "selfmower" end
                if mapped == "mixerwagon" then return "selfmixer" end
                if mapped == "slurrytank" then return "selfslurry" end
                if mapped == "baler" then return "balerdrivable" end
                if mapped == "tractor" and vehicle.spec_combine ~= nil then return "modularcarrier" end
            elseif vehicle.spec_attachable ~= nil then
                if mapped == "rootharvester" then return "rootimplement" end
                if mapped == "forageharvester" then return "forageimplement" end
            end
            if t:find("balerstationary", 1, true) then return "balerstationary" end
            if tostring(name):lower() == "sugarcaneharvesters" then return "sugarcaneharvester" end
            if tostring(name):lower() == "vegetableharvesters" and vehicle.spec_motorized ~= nil then return "vegetableharvester" end
            return mapped
        end
    end
    if t:find("chainsaw", 1, true) then return "chainsaw" end
    if t:find("handtoolmower", 1, true) then return "handmower" end
    if t:find("highpressurewasher", 1, true) then return "washer" end
    if t:find("crawler", 1, true) or t:find("tracked", 1, true) then return "tracked" end
    if vehicle.spec_combine ~= nil then return "combine" end
    if vehicle.spec_woodHarvester ~= nil or vehicle.spec_forwarder ~= nil then return "forestry" end
    if t:find("telehandler", 1, true) then return "telehandler" end
    if t:find("loader", 1, true) then return "loader" end
    if t:find("tractor", 1, true) then return "tractor" end
    if t:find("truck", 1, true) then return "truck" end
    if t:find("car", 1, true) then return "car" end
    if vehicle.spec_motorized ~= nil and vehicle.spec_sprayer ~= nil then return "sprayer" end
    if vehicle.spec_trailer ~= nil then return "trailer" end
    if vehicle.spec_cutter ~= nil then return "header" end
    if vehicle.spec_sowingMachine ~= nil then return "seeder" end
    if vehicle.spec_plow ~= nil then return "plow" end
    if vehicle.spec_cultivator ~= nil then return "cultivator" end
    if vehicle.spec_attachable ~= nil then return "implement" end
    return "unknown"
end
local function writeVehicle(xml, key, vehicle, id, parentId, depth, visited, counter, parentVehicle)
    if vehicle == nil or visited[vehicle] or depth > 8 or counter.count >= 32 then return end
    visited[vehicle] = true
    counter.count = counter.count + 1
    local filename = tostring(vehicle.configFileName or ""):gsub("\\", "/")
    setXMLString(xml, key .. "#id", id)
    setXMLString(xml, key .. "#parentId", parentId or "")
    setXMLString(xml, key .. "#model", text(filename:match("([^/]+)%.xml$") or ""))
    setXMLString(xml, key .. "#instance", text(value(vehicle, "getUniqueId") or vehicle.uniqueId or ""))
    setXMLString(xml, key .. "#name", text(value(vehicle, "getName") or ""))
    setXMLString(xml, key .. "#kind", kind(vehicle))
    local lowered = status(vehicle, "getIsLowered")
    if lowered ~= nil then setXMLBool(xml, key .. "#lowered", lowered) end
    if vehicle.spec_turnOnVehicle ~= nil then
        local turnedOn = status(vehicle, "getIsTurnedOn")
        if turnedOn ~= nil then setXMLBool(xml, key .. "#turnedOn", turnedOn) end
    end
    local fold = value(vehicle, "getFoldAnimTime")
    if type(fold) == "number" and fold == fold and fold >= 0 and fold <= 1 then setXMLFloat(xml, key .. "#fold", fold) end
    local root = vehicle.rootNode or (vehicle.components and vehicle.components[1] and vehicle.components[1].node)
    local parentRoot = parentVehicle and (parentVehicle.rootNode or (parentVehicle.components and parentVehicle.components[1] and parentVehicle.components[1].node))
    local mount = "unknown"
    if root ~= nil and root ~= 0 and parentRoot ~= nil and parentRoot ~= 0 and type(localToLocal) == "function" then
        local ok, _, _, z = pcall(localToLocal, root, parentRoot, 0, 0, 0)
        if ok and type(z) == "number" then
            if z > 0.35 then mount = "front" elseif z < -0.35 then mount = "rear" end
        end
    end
    if mount == "unknown" and kind(vehicle) == "header" and parentVehicle and parentVehicle.spec_combine then mount = "front" end
    setXMLString(xml, key .. "#mount", mount)
    local wheelIndex = 0
    for _, wheel in pairs(vehicle.spec_wheels and vehicle.spec_wheels.wheels or {}) do
        local node = wheel.repr or wheel.driveNode
        if wheelIndex < 32 and counter.wheelCount < 256 and root ~= nil and root ~= 0 and node ~= nil and node ~= 0 and type(localToLocal) == "function" then
            local ok, x, _, z = pcall(localToLocal, node, root, 0, 0, 0)
            if ok and type(x) == "number" and type(z) == "number" and x == x and z == z and math.abs(x) < 100 and math.abs(z) < 100 then
                local wkey = key .. string.format(".wheel(%d)", wheelIndex)
                setXMLFloat(xml, wkey .. "#x", x)
                setXMLFloat(xml, wkey .. "#z", -z)
                wheelIndex = wheelIndex + 1
                counter.wheelCount = counter.wheelCount + 1
            end
        end
    end
    local attached = value(vehicle, "getAttachedImplements") or (vehicle.spec_attacherJoints and vehicle.spec_attacherJoints.attachedImplements) or {}
    for _, entry in ipairs(attached) do
        if entry.object ~= nil and not visited[entry.object] and counter.count < 32 then
            local nextId = tostring(counter.count)
            writeVehicle(xml, "simdeckStatus.vehicle(" .. counter.count .. ")", entry.object, nextId, id, depth + 1, visited, counter, vehicle)
        end
    end
end

function SimDeckStatus:update(dt)
    if SimDeckMarketPrices ~= nil then
        local ok = pcall(SimDeckMarketPrices.update, SimDeckMarketPrices, dt)
        if not ok and not self.reportedMarketError then
            self.reportedMarketError = true
            print("SimDeckStatus: market prices unavailable; equipment updates continue")
        end
    end
    self.elapsed = self.elapsed + dt
    if self.elapsed < 200 then return end
    self.elapsed = 0
    local player = g_localPlayer
    local vehicle = value(player, "getCurrentVehicle") or value(player, "getHeldHandTool")
    if vehicle == nil then
        -- Explicitly clear the previous machine as soon as the player gets out.
        local xml = createXMLFile("simdeckStatus", getUserProfileAppPath() .. "simdeckStatus.xml", "simdeckStatus")
        if xml ~= nil and xml ~= 0 then
            setXMLInt(xml, "simdeckStatus#version", 2)
            setXMLBool(xml, "simdeckStatus#controlled", false)
            writePause(xml)
            saveXMLFile(xml); delete(xml)
        end
        return
    end

    local implement = vehicle
    if type(vehicle.getSelectedVehicle) == "function" then
        local selected = vehicle:getSelectedVehicle()
        if selected ~= nil then implement = selected end
    end
    if implement == vehicle and type(vehicle.getSelectedImplement) == "function" then
        local selected = vehicle:getSelectedImplement()
        if selected ~= nil and selected.object ~= nil then implement = selected.object end
    end
    local lowered = nil
    -- FS25 stores the V action's current state on the towing vehicle joint.
    local attachmentSpec = vehicle.spec_attacherJoints
    if attachmentSpec ~= nil then
        local attached, joints = attachmentSpec.attachedImplements, attachmentSpec.attacherJoints
        if type(attached) == "table" and type(joints) == "table" then
            local selectedEntry, onlyEntry, count = nil, nil, 0
            for _, entry in pairs(attached) do
                local joint = joints[entry.jointDescIndex]
                if entry.object ~= nil and joint ~= nil and type(joint.moveDown) == "boolean" then
                    if entry.object == implement then selectedEntry = entry end
                    onlyEntry, count = entry, count + 1
                end
            end
            local entry = selectedEntry
            if entry == nil and count == 1 then entry = onlyEntry end
            if entry ~= nil then
                implement = entry.object
                -- This is also the method FS25 uses for its V action label.
                lowered = status(implement, "getIsLowered")
                if lowered == nil then lowered = joints[entry.jointDescIndex].moveDown end
            end
        end
    end
    if lowered == nil and implement ~= vehicle and status(implement, "getAllowsLowering") == true then
        lowered = status(implement, "getIsLowered")
    end
    if lowered == nil and not self.reportedLoweringProbe and attachmentSpec ~= nil
        and type(attachmentSpec.attachedImplements) == "table"
        and #attachmentSpec.attachedImplements > 0 then
        self.reportedLoweringProbe = true
        local first = attachmentSpec.attachedImplements[1]
        local joint = first ~= nil and attachmentSpec.attacherJoints ~= nil
            and attachmentSpec.attacherJoints[first.jointDescIndex] or nil
        print(string.format("SimDeckStatus 1.5.0: lowering unavailable; attached=%s joint=%s moveDown=%s",
            tostring(#attachmentSpec.attachedImplements),
            tostring(first ~= nil and first.jointDescIndex or nil),
            tostring(joint ~= nil and joint.moveDown or nil)))
    end
    local powerTarget = implement
    if powerTarget.spec_turnOnVehicle == nil then powerTarget = vehicle end
    local turnedOn = nil
    if powerTarget.spec_turnOnVehicle ~= nil then turnedOn = status(powerTarget, "getIsTurnedOn") end
    local motor = nil
    if vehicle.spec_motorized ~= nil then motor = status(vehicle, "getIsMotorStarted") end

    local path = getUserProfileAppPath() .. "simdeckStatus.xml"
    local xml = createXMLFile("simdeckStatus", path, "simdeckStatus")
    if xml == nil or xml == 0 then return end
    self.sequence = self.sequence + 1
    setXMLInt(xml, "simdeckStatus#version", 2)
    setXMLBool(xml, "simdeckStatus#controlled", true)
    setXMLInt(xml, "simdeckStatus#sequence", self.sequence)
    if lowered ~= nil then setXMLBool(xml, "simdeckStatus#lowered", lowered) end
    if turnedOn ~= nil then setXMLBool(xml, "simdeckStatus#turnedOn", turnedOn) end
    if motor ~= nil then setXMLBool(xml, "simdeckStatus#motor", motor) end
    extraStates(xml, vehicle, implement)
    writeVehicle(xml, "simdeckStatus.vehicle(0)", vehicle, "0", nil, 0, {}, { count = 0, wheelCount = 0 })
    saveXMLFile(xml)
    delete(xml)
    if not self.reportedWrite then
        self.reportedWrite = true
        print("SimDeckStatus 1.5.0: live state file active")
    end
end

addModEventListener(SimDeckStatus)
