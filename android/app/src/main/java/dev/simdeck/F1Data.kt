package dev.simdeck

import org.json.JSONObject

data class WheelData(val surface: Double?, val inner: Double?, val brake: Double?, val pressure: Double?, val wear: Double?, val damage: Double?)
data class MapPoint(val x: Float, val y: Float)
data class F1Data(val wheels: List<WheelData>, val engineTemperature: Double?, val values: Map<String, Double>, val trail: List<MapPoint>, val position: MapPoint?, val race: RaceData? = null, val mfdPanelIndex: Int? = null) {
    companion object {
        private fun JSONObject.number(key: String) = if (isNull(key)) null else optDouble(key, Double.NaN).takeIf { it.isFinite() }
        private fun point(o: JSONObject?): MapPoint? {
            if (o == null) return null
            val x = o.number("x") ?: return null; val y = o.number("y") ?: return null
            return if (kotlin.math.abs(x) <= 100000 && kotlin.math.abs(y) <= 100000) MapPoint(x.toFloat(), y.toFloat()) else null
        }
        fun parse(o: JSONObject?): F1Data? {
            if (o == null) return null
            val wheels = o.optJSONArray("wheels") ?: return null
            if (wheels.length() != 4) return null
            val w = (0..3).map { i ->
                val a = wheels.optJSONObject(i) ?: JSONObject()
                WheelData(a.number("surface"), a.number("inner"), a.number("brake"), a.number("pressure"), a.number("wear"), a.number("damage"))
            }
            val values = mutableMapOf<String, Double>()
            o.optJSONObject("values")?.let { v -> v.keys().forEach { k -> v.number(k)?.let { values[k] = it } } }
            val points = o.optJSONArray("trail")
            val trail = if (points == null || points.length() > 256) emptyList() else (0 until points.length()).mapNotNull { point(points.optJSONObject(it)) }
            return F1Data(w, o.number("engineTemperature"), values, trail, point(o.optJSONObject("position")), RaceData.parse(o.optJSONObject("race")), o.number("mfdPanelIndex")?.toInt()?.takeIf { it in 0..4 })
        }
    }
}
