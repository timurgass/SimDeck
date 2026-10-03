-- Read current selling-station prices, independently of the controlled vehicle.
-- Separate file/cadence keeps this larger payload out of the 200 ms equipment bridge.
SimDeckMarketPrices = { elapsed = 5000 }

local function call(object, method, ...)
    if object == nil or type(object[method]) ~= "function" then return nil end
    local ok, result = pcall(object[method], object, ...)
    if ok then return result end
    return nil
end

local function clean(value)
    return tostring(value or ""):gsub("[%z\1-\31]", " "):sub(1, 160)
end

function SimDeckMarketPrices:update(dt)
    self.elapsed = self.elapsed + dt
    if self.elapsed < 5000 then return end
    local mission = g_currentMission
    local stations = call(mission and mission.storageSystem, "getUnloadingStations")
    if type(stations) ~= "table" or g_fillTypeManager == nil then return end
    self.elapsed = 0
    local rows = {}
    for _, station in pairs(stations) do
        -- appearsOnStats is not price-menu visibility: the stock Riverbend map
        -- disables it for grain mills, river terminals and the train buyer.
        if station.isSellingPoint and type(station.acceptedFillTypes) == "table" then
            for index, accepted in pairs(station.acceptedFillTypes) do
                local fill = accepted and call(g_fillTypeManager, "getFillTypeByIndex", index) or nil
                local price = fill and call(station, "getEffectiveFillTypePrice", index) or nil
                if fill ~= nil and type(price) == "number" and price == price and price > 0 and price < 100000 then
                    if #rows < 2048 then
                        table.insert(rows, { crop = clean(fill.name), cropName = clean(fill.title or fill.name),
                            station = clean(call(station, "getName") or call(station.owningPlaceable, "getName") or ""),
                            price = price * 1000, formatted = clean(call(g_i18n, "formatMoney", price * 1000, 0, true, true)) })
                    end
                end
            end
        end
    end
    table.sort(rows, function(a,b) if a.crop ~= b.crop then return a.crop < b.crop end return a.station < b.station end)
    local xml = createXMLFile("simdeckPrices", getUserProfileAppPath() .. "simdeckPrices.xml", "simdeckPrices")
    if xml == nil or xml == 0 then return end
    setXMLInt(xml, "simdeckPrices#version", 1)
    for i, row in ipairs(rows) do
        local key = string.format("simdeckPrices.offer(%d)", i-1)
        setXMLString(xml, key .. "#crop", row.crop)
        setXMLString(xml, key .. "#cropName", row.cropName)
        setXMLString(xml, key .. "#station", row.station)
        setXMLFloat(xml, key .. "#pricePer1000", row.price)
        setXMLString(xml, key .. "#formatted", row.formatted)
    end
    saveXMLFile(xml); delete(xml)
end
