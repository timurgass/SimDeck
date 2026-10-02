package dev.simdeck

import org.json.JSONObject

internal val vehicleLabels = mapOf("unknown" to "Общая схема", "car" to "Легковая", "suv" to "Внедорожник", "pickup" to "Пикап", "van" to "Фургон", "bus" to "Автобус", "truck" to "Грузовик / тягач", "trailer" to "Прицеп", "tractor" to "Трактор", "combine" to "Комбайн", "loader" to "Погрузчик", "telehandler" to "Телескопический погрузчик", "forestry" to "Лесная техника", "sprayer" to "Опрыскиватель", "tracked" to "Гусеничная техника", "implement" to "Орудие", "header" to "Жатка", "cultivator" to "Культиватор", "plow" to "Плуг", "seeder" to "Сеялка") + fs25EquipmentLabels
data class VehicleWheel(val x: Double, val z: Double, val powered: Boolean?)
data class VehicleAttachment(val id: String, val parentId: String, val name: String, val kind: String, val wheels: List<VehicleWheel>, val lowered: Boolean?, val turnedOn: Boolean?, val fold: Double?, val mount: String = "unknown")
data class VehicleInfo(val id: String, val name: String, val kind: String, val wheels: List<VehicleWheel>, val attachments: List<VehicleAttachment>, val controlled: Boolean, val wear: Map<String, Double>) {
    val axleCount: Int? get() { if(wheels.isEmpty()) return null; val axles=mutableListOf<Double>(); wheels.map { it.z }.sorted().forEach { if(axles.isEmpty() || it-axles.last()>.35) axles.add(it) }; return axles.size }
    companion object {
        fun parse(json: JSONObject?): VehicleInfo? {
            if(json == null) return null
            return runCatching {
                fun text(j: JSONObject, key: String) = j.optString(key, "").filterNot { it.isISOControl() }.take(160)
                fun kind(j: JSONObject) = text(j,"kind").takeIf { it in vehicleLabels } ?: "unknown"
                fun bool(j: JSONObject, key: String) = j.opt(key) as? Boolean
                fun wheels(j: JSONObject): List<VehicleWheel> {
                    val a=j.optJSONArray("wheels") ?: return emptyList(); require(a.length()<=32)
                    return (0 until a.length()).map { val w=a.getJSONObject(it); val x=w.getDouble("x"); val z=w.getDouble("z"); require(x.isFinite() && z.isFinite() && kotlin.math.abs(x)<=100 && kotlin.math.abs(z)<=100); VehicleWheel(x,z,bool(w,"powered")) }
                }
                val id=text(json,"id"); val seen=mutableSetOf(id); val a=json.optJSONArray("attachments"); require((a?.length() ?: 0)<=32)
                val attachments=(0 until (a?.length() ?: 0)).map {
                    val j=a!!.getJSONObject(it); val child=text(j,"id"); val parent=text(j,"parentId"); require(child.isNotBlank() && parent in seen && seen.add(child))
                    VehicleAttachment(child,parent,text(j,"name"),kind(j),wheels(j),bool(j,"lowered"),bool(j,"turnedOn"),j.optDouble("fold",Double.NaN).takeIf { f -> f.isFinite() && f in 0.0..1.0 }, text(j,"mount").takeIf { it in listOf("front","rear") } ?: "unknown")
                }
                val wear=mutableMapOf<String,Double>(); val w=json.optJSONObject("wear")
                for(key in listOf("engine","transmission","cabin","chassis","wheels")) w?.optDouble(key,Double.NaN)?.takeIf { it.isFinite() && it in 0.0..1.0 }?.let { wear[key]=it }
                VehicleInfo(id,text(json,"name"),kind(json),wheels(json),attachments,bool(json,"controlled") ?: true,wear)
            }.getOrNull()
        }
    }
}
