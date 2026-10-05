package dev.simdeck

import org.json.JSONObject

data class Ets2Navigation(val remainingKm: Double?, val remainingMinutes: Double?, val speedLimitKmh: Double?,
    val worldX:Double?=null,val worldZ:Double?=null,val heading:Double?=null,
    val scale:Double?=null,val restMinutes:Double?=null,val gameMinutes:Double?=null,val destinationCity:String?=null)
data class Telemetry(val speedMps: Double, val rpm: Double, val gear: Int, val fuelFraction: Double?, val maxRpm: Double?, val gearboxMode: String? = null, val maxGear: Int? = null,
    val headlights: Int? = null, val actionStates: Map<String, Boolean> = emptyMap(), val f1: F1Data? = null, val acc: AccData? = null,
    val ets2Navigation: Ets2Navigation? = null, val fs25: Fs25Data? = null, val vehicle: VehicleInfo? = null, val fs25Prices: Fs25Prices? = null, val fuelLiters:Double?=null,val beamNg:BeamNgDamage?=null,val snowRunner:SnowRunnerData?=null)
data class DeckAction(val id: String, val page: String, val label: String, val description: String, val key: String, val gesture: String, val group: String = "")
data class ControlFeedback(val active: Boolean? = null, val headlights: Int? = null, val description: String? = null)

internal val fs25StateLabels=mapOf(
    "fs25Lower" to ("Поднято" to "Опущено"),
    "fs25LowerAll" to ("Все подняты" to "Все опущены"),
    "fs25TurnOn" to ("Выключено" to "Работает"),
    "fs25TurnOnAll" to ("Все выключены" to "Все работают"),
    "fs25Motor" to ("Остановлен" to "Запущен"),
    "fs25Fold" to ("Сложено" to "Разложено"),
    "fs25Lights" to ("Выключен" to "Включён"),
    "fs25HighBeam" to ("Выключен" to "Включён"),
    "fs25WorkLightFront" to ("Выключен" to "Включён"),
    "fs25WorkLightBack" to ("Выключен" to "Включён"),
    "fs25Beacon" to ("Выключен" to "Включён"),
    "fs25TurnLeft" to ("Выключен" to "Включён"),
    "fs25TurnRight" to ("Выключен" to "Включён"),
    "fs25Hazard" to ("Выключена" to "Включена"),
    "fs25Cruise" to ("Выключен" to "Включён"),
    "fs25Cover" to ("Закрыто" to "Открыто"),
    "fs25Pipe" to ("Убрана" to "Выдвинута"),
    "fs25Chopper" to ("Валок" to "Измельчение"),
    "fs25Helper" to ("Не работает" to "Работает"),
    "fs25Pause" to ("Время идёт" to "Время остановлено"),
    "fs25Radio" to ("Выключено" to "Включено"),
    "fs25DoubleSpray" to ("Выключена" to "Включена"),
    "fs25Axle" to ("Опущена" to "Поднята")
)

internal fun fs25StateDescription(action:String,active:Boolean?)=fs25StateLabels[action]?.let { (off,on) -> when(active) { true->on;false->off;null->"Состояние неизвестно" } }

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
            fun position(name:String)=n.optDouble(name,Double.NaN).takeIf { it.isFinite() && kotlin.math.abs(it)<=1000000 }
            val heading=n.optDouble("heading",Double.NaN).takeIf { it.isFinite() && it in 0.0..1.0 }
            fun zeroMetric(name:String,max:Double)=n.optDouble(name,Double.NaN).takeIf{it.isFinite()&&it>=0&&it<max}
            Ets2Navigation(metric("remainingKm", 10000.0), metric("remainingMinutes", 200000.0), metric("speedLimitKmh", 360.0),position("worldX"),position("worldZ"),heading,
                metric("scale",31.0),zeroMetric("restMinutes",100000.0),zeroMetric("gameMinutes",100000000.0),n.optString("destinationCity").takeIf{!n.isNull("destinationCity")&&it.isNotBlank()}?.take(100))
        }
        return Telemetry(speed, rpm, d.getInt("gear"), fuel, maximum, mode, maxGear, lights, states,
            F1Data.parse(d.optJSONObject("f1")), AccData.parse(d.optJSONObject("acc")), nav,
            Fs25Data.parse(d.optJSONObject("fs25"), d.optJSONObject("fs25Advisor")), VehicleInfo.parse(d.optJSONObject("vehicle")), Fs25Prices.parse(d.optJSONObject("fs25Prices")),d.optDouble("fuelLiters",Double.NaN).takeIf{it.isFinite() && it in 0.0..5500.0},BeamNgDamage.parse(d.optJSONObject("beamNg")),SnowRunnerData.parse(d.optJSONObject("snowRunner")))
    }
    fun feedback(action: String, data: Telemetry?, stale: Boolean): ControlFeedback {
        if (data == null || stale) return ControlFeedback()
        if (action == "lights" || action == "etsLights") return data.headlights?.let { ControlFeedback(it > 0, it, when(it) { 1 -> "Ближний свет"; 2 -> "Дальний свет"; else -> "Фары выключены" }) } ?: ControlFeedback()
        if (action == "gearbox") return ControlFeedback(description = when(data.gearboxMode) { "arcade" -> "Аркада"; "realistic" -> "Реализм"; else -> null })
        val active = data.actionStates[action] ?: return ControlFeedback(description=fs25StateDescription(action,null))
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
        return ControlFeedback(active = active, description = fs25StateDescription(action,active) ?: description)
    }
    fun gear(value: Int, mode: String? = null) = when { value < 0 -> "R"; value == 0 -> "N"; mode == "arcade" -> "D"; else -> value.toString() }
}
