package dev.simdeck

import org.json.JSONObject

data class SnowRunnerComponent(val id:String,val name:String,val damage:Int,val capacity:Int) {
 val remaining get()=capacity-damage
 val damageFraction get()=damage.toDouble()/capacity
}
data class SnowRunnerData(val build:String,val fuelCapacity:Double,val components:List<SnowRunnerComponent>,val gearLabel:String?,val awdAvailable:Boolean?,val differentialAvailable:Boolean?,val availableGears:List<String>?=null) {
 companion object {
  fun parse(root:JSONObject?):SnowRunnerData? {
   if(root==null)return null
   val capacity=root.optDouble("fuelCapacity",Double.NaN)
   if(!capacity.isFinite() || capacity<=0 || capacity>10000)return null
   val components=root.optJSONArray("components") ?: return null
   if(components.length()>8)return null
   val items=(0 until components.length()).mapNotNull { i->
    val c=components.optJSONObject(i) ?: return@mapNotNull null
    val damage=c.optInt("damage",-1);val maximum=c.optInt("capacity",-1)
    if(maximum !in 1..100000 || damage !in 0..maximum)return@mapNotNull null
    SnowRunnerComponent(c.optString("id").take(32),c.optString("name").take(80),damage,maximum)
   }
   val rawGears=root.optJSONArray("availableGears")
   val gearList=rawGears?.takeIf{it.length() in 3..7}?.let{a->(0 until a.length()).map{a.optString(it)}}
   val gears=gearList?.takeIf{it.distinct().size==it.size && it.all{g->g in setOf("A","N","R","H","L","L+","L-")} && it.containsAll(listOf("A","N","R"))}
   return SnowRunnerData(root.optString("build").take(80),capacity,items,root.optString("gearLabel").takeIf{!root.isNull("gearLabel") && it.isNotBlank()}?.take(12),root.opt("awdAvailable") as? Boolean,root.opt("differentialAvailable") as? Boolean,gears)
  }
 }
}
