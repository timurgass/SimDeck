package dev.simdeck

import org.json.JSONObject

data class Ets2Navigation(val remainingKm: Double?, val remainingMinutes: Double?, val speedLimitKmh: Double?)
data class Telemetry(val speedMps: Double, val rpm: Double, val gear: Int, val fuelFraction: Double?, val maxRpm: Double?, val gearboxMode: String? = null, val maxGear: Int? = null,
    val headlights: Int? = null, val actionStates: Map<String, Boolean> = emptyMap(), val f1: F1Data? = null, val acc: AccData? = null,
    val ets2Navigation: Ets2Navigation? = null, val fs25: Fs25Data? = null, val vehicle: VehicleInfo? = null, val fs25Prices: Fs25Prices? = null)
data class DeckAction(val id: String, val page: String, val label: String, val description: String, val key: String, val gesture: String, val group: String = "")
data class ControlFeedback(val active: Boolean? = null, val headlights: Int? = null, val description: String? = null)

object Protocol {
    // FS25's file bridge runs every 200 ms and tolerates partial file writes.
    // Match the server's 1500 ms live-state window; racing telemetry stays at 500 ms.
    fun telemetryFresh(profileId: String, sourceAge: Long, elapsed: Long): Boolean {
        val limit = if(profileId == "fs25") 1500L else 500L
        return sourceAge >= 0 && elapsed >= 0 && sourceAge < limit && elapsed < limit - sourceAge
    }
    fun controls(root: JSONObject): List<DeckAction> {
        val list = root.optJSONArray("controls") ?: return emptyList()
        require(list.length() <= 96)
        return (0 until list.length()).map { i ->
            val a = list.getJSONObject(i)
            val gesture = a.getString("gesture")
            require(gesture in listOf("press", "hold", "tapThenHold"))
            DeckAction(a.getString("id"), a.getString("page"), a.getString("label"), a.getString("description"), a.getString("key"), gesture, a.optString("group", ""))
        }.also { require(it.map { a -> a.id }.distinct().size == it.size) }
    }
    fun telemetry(root: JSONObject): Telemetry? {
        val d = root.optJSONObject("data") ?: return null
        val speed = d.optDouble("speedMps", Double.NaN)
        val rpm = d.optDouble("rpm", Double.NaN)
        val fuel = if (d.isNull("fuelFraction")) null else d.optDouble("fuelFraction", Double.NaN)
        if (!speed.isFinite() || speed < 0 || !rpm.isFinite() || rpm < 0 || (fuel != null && (!fuel.isFinite() || fuel !in 0.0..1.0)) || d.isNull("gear")) return null
        val maximum = if (d.isNull("maxRpm")) null else d.optDouble("maxRpm").takeIf { it.isFinite() && it > 0 }
        val mode = d.optString("gearboxMode").takeIf { it == "arcade" || it == "realistic" }
        val maxGear = d.optInt("maxGear", 0).takeIf { it in 1..128 }
        val lights = d.optInt("headlights", -1).takeIf { !d.isNull("headlights") && it in 0..2 }
        val states = mutableMapOf<String, Boolean>()
        d.optJSONObject("actionStates")?.let { s -> s.keys().forEach { key -> (s.opt(key) as? Boolean)?.let { states[key] = it } } }
        val nav = d.optJSONObject("ets2Navigation")?.let { n ->
            fun metric(name: String, max: Double) = n.optDouble(name, Double.NaN).takeIf { it.isFinite() && it > 0 && it < max }
            Ets2Navigation(metric("remainingKm", 10000.0), metric("remainingMinutes", 200000.0), metric("speedLimitKmh", 360.0))
        }
        return Telemetry(speed, rpm, d.getInt("gear"), fuel, maximum, mode, maxGear, lights, states,
            F1Data.parse(d.optJSONObject("f1")), AccData.parse(d.optJSONObject("acc")), nav,
            Fs25Data.parse(d.optJSONObject("fs25"), d.optJSONObject("fs25Advisor")), VehicleInfo.parse(d.optJSONObject("vehicle")), Fs25Prices.parse(d.optJSONObject("fs25Prices")))
    }
    fun feedback(action: String, data: Telemetry?, stale: Boolean): ControlFeedback {
        if (data == null || stale) return ControlFeedback()
        if (action == "lights" || action == "etsLights") return data.headlights?.let { ControlFeedback(it > 0, it, when(it) { 1 -> "Ближний свет"; 2 -> "Дальний свет"; else -> "Фары выключены" }) } ?: ControlFeedback()
        if (action == "gearbox") return ControlFeedback(description = when(data.gearboxMode) { "arcade" -> "Аркада"; "realistic" -> "Реализм"; else -> null })
        val active = data.actionStates[action] ?: return ControlFeedback()
        val description = when(action) {
            "ignition" -> null // Keep the required tap-then-hold instructions visible.
            "fourWheelDrive" -> if (active) "Полный привод включён" else "Полный привод выключен"
            "range" -> if (active) "Низкий ряд" else "Высокий ряд"
            "differentials" -> if (active) "Есть блокировка" else "Блокировки сняты"
            "couplers" -> if (active) "Соединено" else "Отсоединено"
            "etsEngine" -> if (active) "Двигатель работает" else "Двигатель выключен"
            "etsParkingBrake" -> if (active) "Ручник включён" else "Ручник выключен"
            "etsDifferential" -> if (active) "Блокировка включена" else "Блокировка выключена"
            "etsHighBeam" -> if (active) "Дальний свет" else "Дальний выключен"
            "etsHazards" -> if (active) "Аварийка включена" else "Аварийка выключена"
            "etsCruise" -> if (active) "Круиз активен" else "Круиз выключен"
            "fs25Lower" -> if (active) "Орудие опущено" else "Орудие поднято"
            "fs25TurnOn" -> if (active) "Агрегат работает" else "Агрегат выключен"
            "fs25Motor" -> if (active) "Двигатель работает" else "Двигатель выключен"
            else -> if (active) "Включено" else "Выключено"
        }
        return ControlFeedback(active = active, description = description)
    }
    fun gear(value: Int, mode: String? = null) = when { value < 0 -> "R"; value == 0 -> "N"; mode == "arcade" -> "D"; else -> value.toString() }
}
