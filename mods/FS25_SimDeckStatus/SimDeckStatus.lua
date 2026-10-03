-- Optional local bridge. Reads only the player's currently controlled machine.
-- Missing capabilities are omitted, never guessed from the last button press.
local SimDeckStatus = { elapsed = 0, sequence = 0, reportedWrite = false }
print("SimDeckStatus 1.4.0: script loaded")

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
        print(string.format("SimDeckStatus 1.4.0: lowering unavailable; attached=%s joint=%s moveDown=%s",
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
    writeVehicle(xml, "simdeckStatus.vehicle(0)", vehicle, "0", nil, 0, {}, { count = 0, wheelCount = 0 })
    saveXMLFile(xml)
    delete(xml)
    if not self.reportedWrite then
        self.reportedWrite = true
        print("SimDeckStatus 1.4.0: live state file active")
    end
end

addModEventListener(SimDeckStatus)
