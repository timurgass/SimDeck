package dev.simdeck

import org.json.JSONObject
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

data class Fs25Field(val id: Int, val crop: String, val ground: String, val weeds: Int, val lime: Int, val fertilizer: Int)
data class Fs25Alert(val severity: Int, val message: String)
data class Fs25Task(val title: String, val status: Int, val detail: String?)
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
                        f.optInt("weedState"), f.optInt("limeLevel"), f.optInt("sprayLevel"))
                }
            }
            val rawAlerts = advisor?.optJSONArray("alerts")
            val alerts = (0 until (rawAlerts?.length() ?: 0).coerceAtMost(200)).mapNotNull { i ->
                rawAlerts?.optJSONObject(i)?.let { a -> Fs25Alert(a.optInt("severity"), a.optString("message")) }
            }
            val rawTasks = advisor?.optJSONArray("tasks")
            val tasks = (0 until (rawTasks?.length() ?: 0).coerceAtMost(200)).mapNotNull { i ->
                rawTasks?.optJSONObject(i)?.let { t ->
                    Fs25Task(t.optJSONObject("task")?.optString("title") ?: "", t.optInt("status"),
                        t.optString("detail").takeIf(String::isNotBlank))
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
                details.optBoolean("isStale"), farm?.optDouble("money"), farm?.optDouble("loan"),
                fields, alerts, tasks, advisor?.optString("planName")?.takeIf(String::isNotBlank)
            )
        }
    }
}
