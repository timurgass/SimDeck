package dev.simdeck

import org.json.JSONObject

data class Telemetry(val speedMps: Double, val rpm: Double, val gear: Int, val fuelFraction: Double?, val maxRpm: Double?, val gearboxMode: String? = null, val maxGear: Int? = null,
    val headlights: Int? = null, val actionStates: Map<String, Boolean> = emptyMap(), val f1: F1Data? = null)
data class DeckAction(val id: String, val page: String, val label: String, val description: String, val key: String, val gesture: String, val group: String = "")
data class ControlFeedback(val active: Boolean? = null, val headlights: Int? = null, val description: String? = null)

object Protocol {
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
        return Telemetry(speed, rpm, d.getInt("gear"), fuel, maximum, mode, maxGear, lights, states, F1Data.parse(d.optJSONObject("f1")))
    }
    fun feedback(action: String, data: Telemetry?, stale: Boolean): ControlFeedback {
        if (data == null || stale) return ControlFeedback()
        if (action == "lights") return data.headlights?.let { ControlFeedback(it > 0, it, when(it) { 1 -> "Ближний свет"; 2 -> "Дальний свет"; else -> "Фары выключены" }) } ?: ControlFeedback()
        if (action == "gearbox") return ControlFeedback(description = when(data.gearboxMode) { "arcade" -> "Аркада"; "realistic" -> "Реализм"; else -> null })
        val active = data.actionStates[action] ?: return ControlFeedback()
        val description = when(action) {
            "ignition" -> null // Keep the required tap-then-hold instructions visible.
            "fourWheelDrive" -> if (active) "Полный привод включён" else "Полный привод выключен"
            "range" -> if (active) "Низкий ряд" else "Высокий ряд"
            "differentials" -> if (active) "Есть блокировка" else "Блокировки сняты"
            "couplers" -> if (active) "Соединено" else "Отсоединено"
            else -> if (active) "Включено" else "Выключено"
        }
        return ControlFeedback(active = active, description = description)
    }
    fun gear(value: Int, mode: String? = null) = when { value < 0 -> "R"; value == 0 -> "N"; mode == "arcade" -> "D"; else -> value.toString() }
}
