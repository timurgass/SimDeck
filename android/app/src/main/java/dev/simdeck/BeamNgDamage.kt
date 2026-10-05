package dev.simdeck
import org.json.JSONObject

data class BeamNgWheelDamage(val index:Int,val name:String,val broken:Boolean?,val flat:Boolean?,val brakeDamaged:Boolean?,val brakeTemperature:Double?)
data class BeamNgPartDamage(val name:String,val damage:Double)
data class BeamNgDamage(val body:Map<String,Double>,val faults:Map<String,Boolean>,val wheels:List<BeamNgWheelDamage>,val parts:List<BeamNgPartDamage>,val totalDamagedParts:Int?,val coolantTemperature:Double?,val oilTemperature:Double?) {
 companion object {
  fun parse(j:JSONObject?):BeamNgDamage? {
   if(j==null)return null
   return runCatching {
    fun number(o:JSONObject,k:String,min:Double,max:Double)=o.optDouble(k,Double.NaN).takeIf{it.isFinite()&&it in min..max}
    val body=j.optJSONObject("body");val faults=j.optJSONObject("faults")
    val zones=listOf("FL","FR","ML","MR","RL","RR").mapNotNull{k->body?.let{number(it,k,0.0,1.0)}?.let{k to it}}.toMap()
    val states=buildMap<String,Boolean>{faults?.keys()?.asSequence()?.take(32)?.forEach{k->(faults.opt(k) as? Boolean)?.let{put(k,it)}}}
    val wheels=j.optJSONArray("wheels");val parts=j.optJSONArray("parts")
    BeamNgDamage(zones,states,(0 until minOf(wheels?.length()?:0,16)).mapNotNull{i->wheels?.optJSONObject(i)?.let{w->
     w.optInt("index",-1).takeIf{it in 0..15}?.let{BeamNgWheelDamage(it,w.optString("name").take(24),w.opt("broken") as? Boolean,w.opt("flat") as? Boolean,w.opt("brakeDamaged") as? Boolean,number(w,"brakeTemperature",0.0,2500.0))}}},
     (0 until minOf(parts?.length()?:0,8)).mapNotNull{i->parts?.optJSONObject(i)?.let{p->number(p,"damage",0.0,1.0)?.let{BeamNgPartDamage(p.optString("name").take(80),it)}}},j.optInt("totalDamagedParts",-1).takeIf{it in 0..100000},number(j,"coolantTemperature",0.0,2500.0),number(j,"oilTemperature",0.0,2500.0))
   }.getOrNull()
  }
 }
}
