package dev.simdeck

import org.json.JSONObject
import kotlin.math.hypot
import java.util.Locale

data class RaceDriver(val index: Int, val name: String, val team: Int, val number: Int, val position: Int,
    val lap: Int, val distance: Double, val pit: Int, val result: Int, val driverStatus: Int,
    val gapAheadMs: Int, val gapLeaderMs: Int, val player: Boolean) {
    val shortName: String get() = name.trim().split(Regex("\\s+")).lastOrNull().orEmpty().take(3).uppercase(Locale.ROOT).ifEmpty { "#$number" }
    val onTrack: Boolean get() = result == 2 && driverStatus != 0
}
data class RaceData(val trackId: Int, val trackLength: Int, val sessionType: Int, val fresh: Boolean, val drivers: List<RaceDriver>) {
    companion object {
        fun parse(o: JSONObject?): RaceData? {
            if (o == null) return null
            val a = o.optJSONArray("drivers") ?: return null
            if (a.length() > 22) return null
            val drivers = (0 until a.length()).mapNotNull { i ->
                val d = a.optJSONObject(i) ?: return@mapNotNull null
                val index = d.optInt("index", -1); val distance = d.optDouble("distance", Double.NaN)
                val position = d.optInt("position", -1)
                if (index !in 0..21 || position !in 0..22 || !distance.isFinite() || kotlin.math.abs(distance) > 100000) return@mapNotNull null
                RaceDriver(index, d.optString("name").filterNot { it.isISOControl() }.take(48), d.optInt("team"), d.optInt("number"), position,
                    d.optInt("lap"), distance, d.optInt("pit"), d.optInt("result"), d.optInt("driverStatus"), d.optInt("gapAheadMs"), d.optInt("gapLeaderMs"), d.optBoolean("player"))
            }.distinctBy { it.index }.sortedWith(compareBy<RaceDriver> { if(it.position > 0) it.position else 99 }.thenBy { it.index })
            return RaceData(o.optInt("trackId", -1), o.optInt("trackLength"), o.optInt("sessionType"), o.optBoolean("fresh"), drivers)
        }
    }
}

data class Circuit(val id: Int, val name: String, val points: List<MapPoint>) {
    private val cumulative = points.zipWithNext().runningFold(0.0) { sum, (a,b) -> sum + hypot((b.x-a.x).toDouble(), (b.y-a.y).toDouble()) }
    fun at(distance: Double, trackLength: Int): MapPoint? {
        if (!distance.isFinite() || trackLength <= 0 || points.size < 2 || cumulative.last() <= 0) return null
        val fraction = ((distance % trackLength) + trackLength) % trackLength / trackLength
        val along = fraction * cumulative.last()
        val i = cumulative.indexOfFirst { it > along }.let { if(it < 1) 1 else it }
        val span = cumulative[i] - cumulative[i-1]
        val t = if(span > 0) ((along-cumulative[i-1])/span).toFloat() else 0f
        val a=points[i-1]; val b=points[i]
        return MapPoint(a.x+(b.x-a.x)*t, a.y+(b.y-a.y)*t)
    }
    companion object {
        fun parseAll(text: String): Map<Int,Circuit> {
            val a=JSONObject(text.removePrefix("\uFEFF")).getJSONArray("tracks")
            return (0 until a.length()).associate { i ->
                val o=a.getJSONObject(i); val p=o.getJSONArray("points")
                val points=(0 until p.length()).map { k -> val v=p.getJSONArray(k); MapPoint(v.getDouble(0).toFloat(),v.getDouble(1).toFloat()) }
                o.getInt("id") to Circuit(o.getInt("id"),o.getString("name"),points)
            }
        }
    }
}
