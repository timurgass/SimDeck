-- Optional local bridge. Reads only the player's currently controlled machine.
-- Missing capabilities are omitted, never guessed from the last button press.
local SimDeckStatus = { elapsed = 0, sequence = 0, reportedWrite = false }
print("SimDeckStatus 1.0.5: script loaded")

local function status(object, method)
    if object ~= nil and type(object[method]) == "function" then
        local ok, value = pcall(object[method], object)
        if ok and type(value) == "boolean" then return value end
    end
    return nil
end

function SimDeckStatus:update(dt)
    self.elapsed = self.elapsed + dt
    if self.elapsed < 400 then return end
    self.elapsed = 0
    local player = g_localPlayer
    local vehicle = player ~= nil and player:getCurrentVehicle() or nil
    if vehicle == nil then return end

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
        print(string.format("SimDeckStatus 1.0.5: lowering unavailable; attached=%s joint=%s moveDown=%s",
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
    setXMLInt(xml, "simdeckStatus#version", 1)
    setXMLInt(xml, "simdeckStatus#sequence", self.sequence)
    if lowered ~= nil then setXMLBool(xml, "simdeckStatus#lowered", lowered) end
    if turnedOn ~= nil then setXMLBool(xml, "simdeckStatus#turnedOn", turnedOn) end
    if motor ~= nil then setXMLBool(xml, "simdeckStatus#motor", motor) end
    saveXMLFile(xml)
    delete(xml)
    if not self.reportedWrite then
        self.reportedWrite = true
        print("SimDeckStatus 1.0.5: live state file active")
    end
end

addModEventListener(SimDeckStatus)
