package dev.simdeck

import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import org.json.JSONObject

internal class Fs25FieldLabels(private val labels: JSONObject) {
    fun crop(id: String) = labels.optJSONObject("crops")?.optJSONArray(id)?.optString(0) ?: id.ifBlank { "Культура не указана" }
    fun color(id: String): Color = runCatching { Color(android.graphics.Color.parseColor(labels.optJSONObject("crops")?.optJSONArray(id)?.optString(1) ?: "#A7B2A0")) }.getOrDefault(Color(0xFFA7B2A0))
    fun ground(id: String) = labels.optJSONObject("ground")?.optString(id)?.takeIf { it.isNotBlank() } ?: id.ifBlank { "Не указано" }
}

internal fun filteredFs25Fields(fields: List<Fs25Field>, query: String, crop: String?, labels: Fs25FieldLabels) =
    fields.filter { it.id > 0 }.distinctBy { it.id }.sortedBy { it.id }.filter {
        (crop == null || it.crop == crop) && (query.isBlank() || "${it.id} ${it.crop} ${labels.crop(it.crop)}".contains(query.trim(),ignoreCase=true))
    }

@Composable internal fun Fs25Fields(state: DeckState) {
    val d=LocalProfileDesign.current
    val context=LocalContext.current
    val labels=remember { Fs25FieldLabels(JSONObject(context.assets.open("fs25-field-ui.json").bufferedReader().use { it.readText() })) }
    val data=state.telemetry?.fs25
    var selectedId by rememberSaveable(data?.mapName) { mutableStateOf<Int?>(null) }
    var query by rememberSaveable(data?.mapName) { mutableStateOf("") }
    var crop by rememberSaveable(data?.mapName) { mutableStateOf<String?>(null) }
    val fields=filteredFs25Fields(data?.fields.orEmpty(),query,crop,labels)
    val selected=fields.firstOrNull { it.id==selectedId } ?: fields.firstOrNull()
    val overview:@Composable ()->Unit={ DesignCard {
        Text("ПОЛЯ",fontSize=22.sp,lineHeight=26.sp,fontWeight=FontWeight.Bold,color=d.accent)
        Text("${data?.saveName ?: "Сохранение не найдено"} · ${data?.mapName.orEmpty()}",color=d.muted)
        Text("Из сохранения: ${data?.savedAt ?: "—"}" + if(data?.stale==true) " · данные устарели" else "",fontSize=12.sp,lineHeight=16.sp,color=if(data?.stale==true) Color(0xFFFFC449) else d.muted)
        Text("Это сетка по номерам, не географическая карта. Цвет обозначает культуру. Принадлежность и площадь участков пока неизвестны. Новые состояния появятся после сохранения FS25.",fontSize=12.sp,lineHeight=16.sp,color=d.muted)
        OutlinedTextField(value=query,onValueChange={query=it},label={Text("Номер поля или культура")},singleLine=true,modifier=Modifier.fillMaxWidth())
        Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(6.dp)) {
            FilterChip(selected=crop==null,onClick={crop=null},label={Text("Все")})
            data?.fields.orEmpty().map { it.crop }.distinct().sorted().forEach { name ->
                FilterChip(selected=crop==name,onClick={crop=name},label={Text(labels.crop(name))})
            }
        }
        if(fields.isEmpty()) Text(if(data?.fields.isNullOrEmpty()) "В сохранении нет записей fields.xml для отображения. Отсутствие записей не означает, что на карте нет полей." else "По этому фильтру полей нет.",color=d.muted)
        BoxWithConstraints {
            val columns=if(maxWidth>=650.dp) 6 else 3
            Column(verticalArrangement=Arrangement.spacedBy(8.dp)) {
                fields.chunked(columns).forEach { row ->
                    Row(horizontalArrangement=Arrangement.spacedBy(8.dp)) {
                        row.forEach { f ->
                            OutlinedButton(onClick={selectedId=f.id},modifier=Modifier.weight(1f).heightIn(min=80.dp),contentPadding=PaddingValues(8.dp),colors=ButtonDefaults.outlinedButtonColors(containerColor=if(f.id==selected?.id) d.panelAlt else d.panel,contentColor=labels.color(f.crop))) {
                                Column { Text("№${f.id}",fontWeight=FontWeight.Bold,fontSize=18.sp,lineHeight=22.sp); Text(labels.crop(f.crop),fontSize=11.sp,lineHeight=15.sp) }
                            }
                        }
                        repeat(columns-row.size) { Spacer(Modifier.weight(1f)) }
                    }
                }
            }
        }
    }
    }
    val detail:@Composable ()->Unit={ selected?.let { f ->
        DesignCard {
            Text("ПОЛЕ №${f.id} · ${labels.crop(f.crop)}",fontSize=21.sp,lineHeight=25.sp,fontWeight=FontWeight.Bold,color=labels.color(f.crop))
            Text(labels.ground(f.ground),fontSize=16.sp,lineHeight=20.sp)
            Text("Рост: ${f.growth?.let { "стадия $it" } ?: "—"} · предыдущая стадия ${f.lastGrowth ?: "—"}",color=d.muted)
            Text("Планируемая культура: ${f.planned?.let { labels.crop(it) } ?: "—"}",color=d.muted,fontSize=12.sp,lineHeight=16.sp)
            Fs25FieldLevel("Сорняки",f.weeds,9)
            Fs25FieldLevel("Известь",f.lime,3)
            Fs25FieldLevel("Удобрение",f.fertilizer,3)
            Fs25FieldLevel("Вспашка",f.plow,1)
            Fs25FieldLevel("Прикатывание",f.roller,1)
            Fs25FieldLevel("Мульчирование",f.mulch,1)
            Text("Камни: ${f.stones ?: "—"} · вода: ${f.water ?: "—"} · тип удобрения: ${f.sprayType?.takeIf { it.isNotBlank() } ?: "—"}",fontSize=12.sp,lineHeight=16.sp,color=d.muted)
            val alerts=data?.alerts.orEmpty().filter { it.field==f.id }
            Text("СОВЕТНИК",fontWeight=FontWeight.Bold)
            if(alerts.isEmpty()) Text("Советник не передал предупреждений для этого поля.",color=d.muted,fontSize=12.sp,lineHeight=16.sp)
            alerts.forEach { Text(it.message,color=if(it.severity>=2) Color(0xFFFF7770) else d.accent,fontSize=14.sp,lineHeight=18.sp) }
            val tasks=data?.tasks.orEmpty().filter { it.field==f.id }
            tasks.forEach { Text("${listOf("К исполнению","Выполнено","Вне сезона","Нет поля").getOrElse(it.status) { "Неизвестный статус" }} · ${it.title}",fontSize=14.sp,lineHeight=18.sp) }
        }
    }
    }
    BoxWithConstraints {
        if(maxWidth>=650.dp) Row(horizontalArrangement=Arrangement.spacedBy(16.dp)) {Column(Modifier.weight(1.5f)) {overview()};Column(Modifier.weight(1f)) {detail()} }
        else Column(verticalArrangement=Arrangement.spacedBy(14.dp)) {overview();detail()}
    }
}

@Composable private fun Fs25FieldLevel(label: String, value: Int?, maximum: Int) {
    val d=LocalProfileDesign.current
    Column(Modifier.fillMaxWidth().background(d.panelAlt).padding(10.dp)) {
        Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) { Text(label,fontSize=14.sp,lineHeight=18.sp);Text(value?.let { "$it/$maximum" } ?: "—",fontWeight=FontWeight.Bold) }
        value?.let { LinearProgressIndicator(progress={ (it.toFloat()/maximum).coerceIn(0f,1f) },color=d.accent,trackColor=d.line,modifier=Modifier.fillMaxWidth().padding(top=7.dp)) }
    }
}
