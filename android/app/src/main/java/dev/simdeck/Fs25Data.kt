package dev.simdeck

import org.json.JSONObject
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

data class Fs25Field(val id: Int, val crop: String, val ground: String, val weeds: Int?, val lime: Int?, val fertilizer: Int?,
    val growth: Int? = null, val lastGrowth: Int? = null, val planned: String? = null,
    val stones: Int? = null, val plow: Int? = null, val roller: Int? = null, val mulch: Int? = null,
    val water: Int? = null, val sprayType: String? = null)
data class Fs25Alert(val severity: Int, val message: String, val field: Int? = null)
data class Fs25Task(val title: String, val status: Int, val detail: String?, val field: Int? = null)
internal fun fs25Int(value: JSONObject, key: String): Int? = (value.opt(key) as? Number)?.toDouble()?.let {
    if(it.isFinite() && it>=0 && it<=Int.MAX_VALUE && it%1.0==0.0) it.toInt() else null
}
data class Fs25Data(
    val saveName: String, val mapName: String, val period: String, val savedAt: String,
    val stale: Boolean, val money: Double?, val loan: Double?, val fields: List<Fs25Field>,
    val alerts: List<Fs25Alert>, val tasks: List<Fs25Task>, val planName: String?
) {
    companion object {
        fun parse(details: JSONObject?, advisor: JSONObject?): Fs25Data? {
            if (details == null) return null
            val header = details.optJSONObject("header") ?: return null
            val environment = details.optJSONObject("environment") ?: return null
            val farms = details.optJSONArray("farms")
            val farm = if (farms != null && farms.length() > 0) farms.optJSONObject(0) else null
            val rawFields = details.optJSONArray("fields")
            val fields = (0 until (rawFields?.length() ?: 0).coerceAtMost(2000)).mapNotNull { i ->
                rawFields?.optJSONObject(i)?.let { f ->
                    Fs25Field(f.optInt("id"), f.optString("fruitType"), f.optString("groundType"),
                        fs25Int(f,"weedState"), fs25Int(f,"limeLevel"), fs25Int(f,"sprayLevel"),
                        fs25Int(f,"growthState"),fs25Int(f,"lastGrowthState"),f.optString("plannedFruit").takeIf { it.isNotBlank() && it!="null" },
                        fs25Int(f,"stoneLevel"),fs25Int(f,"plowLevel"),fs25Int(f,"rollerLevel"),fs25Int(f,"stubbleShredLevel"),fs25Int(f,"waterLevel"),f.optString("sprayType"))
                }
            }
            val rawAlerts = advisor?.optJSONArray("alerts")
            val alerts = (0 until (rawAlerts?.length() ?: 0).coerceAtMost(200)).mapNotNull { i ->
                rawAlerts?.optJSONObject(i)?.let { a -> Fs25Alert(a.optInt("severity"), a.optString("message"),fs25Int(a,"field")) }
            }
            val rawTasks = advisor?.optJSONArray("tasks")
            val tasks = (0 until (rawTasks?.length() ?: 0).coerceAtMost(200)).mapNotNull { i ->
                rawTasks?.optJSONObject(i)?.let { t ->
                    Fs25Task(t.optJSONObject("task")?.optString("title") ?: "", t.optInt("status"),
                        t.optString("detail").takeIf(String::isNotBlank),t.optJSONObject("task")?.let { fs25Int(it,"field") })
                }
            }
            val saved = details.optString("timestamp")
            val localTime = runCatching {
                DateTimeFormatter.ofPattern("dd.MM.yyyy HH:mm")
                    .format(Instant.parse(saved).atZone(ZoneId.systemDefault()))
            }.getOrDefault(saved.replace('T', ' ').take(19))
            return Fs25Data(
                header.optString("savegameName"), header.optString("mapTitle"),
                environment.optJSONObject("period")?.optString("russianMonth") ?: "—", localTime,
                details.optBoolean("isStale"), farm?.optDouble("money")?.takeIf { it.isFinite() }, farm?.optDouble("loan")?.takeIf { it.isFinite() },
                fields, alerts, tasks, advisor?.optString("planName")?.takeIf(String::isNotBlank)
            )
        }
    }
}
