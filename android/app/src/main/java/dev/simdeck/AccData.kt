package dev.simdeck

import org.json.JSONObject

data class AccWheelData(val pressure: Double, val coreTemperature: Double, val brakeTemperature: Double,
    val wear: Double, val padLife: Double, val discLife: Double, val suspensionDamage: Double)

data class AccData(val wheels: List<AccWheelData>, val airTemperature: Double, val roadTemperature: Double,
    val waterTemperature: Double, val brakeBias: Double, val pitLimiter: Boolean,
    val ignition: Boolean, val starter: Boolean, val engineRunning: Boolean) {
    companion object {
        private fun JSONObject.number(key: String): Double? = optDouble(key, Double.NaN).takeIf { it.isFinite() }
        fun parse(o: JSONObject?): AccData? {
            if (o == null) return null
            val array = o.optJSONArray("wheels") ?: return null
            if (array.length() != 4) return null
            val wheels = (0..3).map { i ->
                val w = array.optJSONObject(i) ?: return null
                AccWheelData(w.number("pressure") ?: return null, w.number("coreTemperature") ?: return null,
                    w.number("brakeTemperature") ?: return null, w.number("wear") ?: return null,
                    w.number("padLife") ?: return null, w.number("discLife") ?: return null,
                    w.number("suspensionDamage") ?: return null)
            }
            return AccData(wheels, o.number("airTemperature") ?: return null, o.number("roadTemperature") ?: return null,
                o.number("waterTemperature") ?: return null, o.number("brakeBias") ?: return null,
                o.optBoolean("pitLimiter"), o.optBoolean("ignition"), o.optBoolean("starter"), o.optBoolean("engineRunning"))
        }
    }
}
