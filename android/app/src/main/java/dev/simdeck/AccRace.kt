package dev.simdeck

import org.json.JSONObject

data class AccMapPoint(val bin:Int,val x:Double,val z:Double)
data class AccDriverData(val index:Int,val name:String,val shortName:String,val team:String,val number:Int,
    val cup:Int,val position:Int,val lap:Int,val x:Double,val z:Double,val location:Int,
    val bestLapMs:Int?,val lastLapMs:Int?,val gapAheadMs:Int?,val gapLeaderMs:Int?,val player:Boolean,val fresh:Boolean)
data class AccRaceData(val track:String,val trackLength:Int,val sessionType:Int,val fresh:Boolean,val replay:Boolean,
    val points:List<AccMapPoint>,val bins:Int,val drivers:List<AccDriverData>) {
    companion object {
        fun parse(o:JSONObject?):AccRaceData? {
            if(o==null)return null
            val points=o.optJSONArray("points")?:return null
            val drivers=o.optJSONArray("drivers")?:return null
            val bins=o.optInt("bins")
            if(bins!=256||points.length()>bins||drivers.length()>100)return null
            fun coordinate(p:JSONObject,key:String)=p.optDouble(key,Double.NaN).takeIf{it.isFinite()&&it in -100000.0..100000.0}
            fun ms(p:JSONObject,key:String)=p.optInt(key,-1).takeIf{!p.isNull(key)&&it in 0..86400000}
            val map=(0 until points.length()).map {i->val p=points.optJSONObject(i)?:return null
                val bin=p.optInt("bin",-1);if(bin !in 0 until bins)return null
                AccMapPoint(bin,coordinate(p,"x")?:return null,coordinate(p,"z")?:return null)}
            if(map.zipWithNext().any{(a,b)->b.bin<=a.bin})return null
            val cars=(0 until drivers.length()).map {i->val d=drivers.optJSONObject(i)?:return null
                val index=d.optInt("index",-1);val position=d.optInt("position",-1);val location=d.optInt("location",-1)
                if(index !in 0..65535||position !in 0..100||location !in 0..4)return null
                AccDriverData(index,d.optString("name").take(160),d.optString("shortName").take(16),d.optString("team").take(160),d.optInt("number"),d.optInt("cup",-1),position,d.optInt("lap"),
                    coordinate(d,"x")?:return null,coordinate(d,"z")?:return null,location,ms(d,"bestLapMs"),ms(d,"lastLapMs"),ms(d,"gapAheadMs"),ms(d,"gapLeaderMs"),d.optBoolean("player"),d.optBoolean("fresh"))}
            if(cars.map{it.index}.distinct().size!=cars.size)return null
            return AccRaceData(o.optString("track").take(120),o.optInt("trackLength"),o.optInt("sessionType"),o.optBoolean("fresh"),o.optBoolean("replay"),map,bins,cars)
        }
    }
}
