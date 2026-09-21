package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.util.Locale

private fun n(value: Double?, unit: String = "", decimals: Int = 0): String = value?.let { String.format(Locale.US, "%.${decimals}f", it) + unit } ?: "—"
private fun compound(value: Double?) = when(value?.toInt()) { 16, 20 -> "Soft"; 17, 21 -> "Medium"; 18, 22 -> "Hard"; 19 -> "Super Soft"; 7 -> "Intermediate"; 8, 15 -> "Wet"; null -> "—"; else -> "Состав ${value.toInt()}" }

@Composable internal fun F1Panel(panel: String, state: DeckState, model: DeckModel) {
    val data = state.telemetry?.f1.takeUnless { state.stale }
    val v = data?.values.orEmpty()
    val gameName = gameDisplayName(state.profileId, state.profileName)
    Surface(color = Color(0xFF202D35), shape = MaterialTheme.shapes.medium) {
        Column(Modifier.fillMaxWidth().padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(when(panel) { "mfdPit" -> "ПИТ-СТОП"; "mfdDamage" -> "ШИНЫ И ПОВРЕЖДЕНИЯ"; "mfdEngine" -> "ДВИГАТЕЛЬ"; "mfdTemps" -> "ТЕМПЕРАТУРЫ И ДАВЛЕНИЕ"; "map" -> "ТРАЕКТОРИЯ"; else -> "СОСТОЯНИЕ БОЛИДА" }, fontSize = 19.sp)
            if (data == null && panel != "map") Text("Нет свежих данных $gameName. На трассе: UDP ${f1UdpFormat(state.profileId)} → ПК, порт 20777.", fontSize = 15.sp, color = Color(0xFFFFCC80))
            when(panel) {
                "map" -> F1CircuitMap(state.telemetry?.f1?.race, state.stale)
                "mfdDamage", "mfdTemps" -> {
                    Text("${compound(v["compound"])} · возраст ${n(v["tyreAge"], " круг.")}", fontSize = 16.sp)
                    // Screen order: front axle, rear axle. Wire order: RL, RR, FL, FR.
                    listOf(listOf(2 to "Передняя левая", 3 to "Передняя правая"), listOf(0 to "Задняя левая", 1 to "Задняя правая")).forEach { row ->
                        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                            row.forEach { (i, name) ->
                                val w = data?.wheels?.getOrNull(i)
                                Surface(Modifier.weight(1f), color = Color(0xFF111C23), shape = MaterialTheme.shapes.small) {
                                    Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                                        Text(name, fontSize = 15.sp)
                                        Text(if (panel == "mfdDamage") "Износ ${n(w?.wear, "%", 1)}" else "${n(w?.surface, " °C")} / ${n(w?.inner, " °C")}", fontSize = 21.sp, color = if ((w?.wear ?: 0.0) >= 60) Color(0xFFFF9E80) else Color(0xFF90E0D0))
                                        Text(if (panel == "mfdDamage") "Повреждение ${n(w?.damage, "%")}" else "Поверхность / внутри", fontSize = 14.sp)
                                        Text("${n(w?.pressure, " psi", 1)} · тормоз ${n(w?.brake, " °C")}", fontSize = 14.sp)
                                    }
                                }
                            }
                        }
                    }
                    if (panel == "mfdDamage") Metrics(v, listOf("frontLeftWingDamage" to "Переднее крыло Л", "frontRightWingDamage" to "Переднее крыло П", "rearWingDamage" to "Заднее крыло", "floorDamage" to "Днище"), "%")
                }
                "mfdEngine" -> {
                    Text("Охлаждение ${n(data?.engineTemperature, " °C")}", fontSize = 24.sp)
                    Metrics(v, listOf("engineDamage" to "Повреждение двигателя", "gearboxDamage" to "Коробка", "iceWear" to "Износ ICE", "mguHWear" to "MGU-H", "mguKWear" to "MGU-K", "tcWear" to "Турбина", "esWear" to "Батарея", "ceWear" to "Электроника"), "%")
                    Text("Энергия ERS ${n(v["ersEnergy"]?.div(1000000), " МДж", 2)}", fontSize = 17.sp)
                }
                "mfdPit" -> F1Pit(data, state, model)
                else -> {
                    Text("${compound(v["compound"])} · ${n(v["tyreAge"], " круг.")}", fontSize = 21.sp)
                    Metrics(v, listOf("frontWing" to "Переднее крыло", "rearWing" to "Заднее крыло", "brakeBias" to "Баланс тормозов, %", "differential" to "Дифференциал, %", "fuelMix" to "Топливная смесь", "ersMode" to "Режим ERS"))
                    Text("Трасса ${n(v["trackTemperature"], " °C")} · воздух ${n(v["airTemperature"], " °C")}", fontSize = 16.sp)
                }
            }
            if (data != null && panel != "map") Text("— означает, что соответствующий пакет ещё не получен или устарел.", fontSize = 12.sp, color = Color(0xFFAEBFC9))
        }
    }
}

@Composable private fun Metrics(values: Map<String, Double>, fields: List<Pair<String, String>>, unit: String = "") {
    fields.chunked(2).forEach { row -> Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
        row.forEach { (key, label) -> Text("$label: ${n(values[key], unit)}", modifier = Modifier.weight(1f), fontSize = 17.sp) }
    } }
}

private val tyreNames = listOf("Soft", "Medium", "Hard", "Inter", "Full Wet")
private val tyreLetters = listOf("S", "M", "H", "I", "W")
private val tyreColors = listOf(Color(0xFFFF646B),Color(0xFFFFD85A),Color(0xFFF2F4F5),Color(0xFF55D394),Color(0xFF53ABFF))
// Observed in the Silverstone race: Auto -> Yes -> No, clamped at either end.
private val repairNames = listOf("Авто", "Да", "Нет")

@Composable private fun TyreButton(index: Int, selected: Boolean, enabled: Boolean, modifier: Modifier = Modifier, click: () -> Unit) {
    OutlinedButton(onClick=click,enabled=enabled,modifier=modifier.height(82.dp),contentPadding=PaddingValues(4.dp),
        colors=ButtonDefaults.outlinedButtonColors(containerColor=if(selected) Color(0xFF334554) else Color.Transparent)) {
        Column(horizontalAlignment=androidx.compose.ui.Alignment.CenterHorizontally,verticalArrangement=Arrangement.spacedBy(3.dp)) {
            Box(Modifier.size(35.dp),contentAlignment=androidx.compose.ui.Alignment.Center) {
                Canvas(Modifier.fillMaxSize()) { drawCircle(tyreColors[index].copy(alpha=if(enabled) 1f else .4f),style=Stroke(4.dp.toPx())) }
                Text(tyreLetters[index],fontSize=18.sp,color=tyreColors[index])
            }
            Text(tyreNames[index],fontSize=12.sp,maxLines=1)
        }
    }
}

@Composable private fun F1Pit(data: F1Data?, state: DeckState, model: DeckModel) {
    val v=data?.values.orEmpty()
    val ready=pitReady(state)
    val enabled=ready && state.pitCursor!=null && !state.menuBusy
    val f1Tyres=v["compound"]?.toInt() in setOf(16,17,18,7,8)
    var syncing by remember { mutableStateOf(false) }
    var row by remember { mutableStateOf<Int?>(null) }
    var tyre by remember { mutableStateOf<Int?>(null) }
    var repair by remember { mutableStateOf<Int?>(null) }
    Text("Подготовка следующего пит-стопа · доступна на трассе",fontSize=16.sp)
    Text("Сейчас: ${compound(v["compound"])} · ${n(v["tyreAge"], " круг.")}",fontSize=18.sp)
    OutlinedButton(onClick={model.press("mfdPit",false)},enabled=state.connected && !state.menuBusy,modifier=Modifier.fillMaxWidth()) { Text("Открыть пит-меню в игре · F2",fontSize=16.sp) }
    OutlinedButton(onClick={row=null;tyre=null;repair=null;syncing=true},enabled=ready && !state.menuBusy,modifier=Modifier.fillMaxWidth()) {
        Text(if(state.pitCursor==null) "Сверить исходные значения с MFD" else "Повторно сверить с MFD",fontSize=16.sp)
    }
    if(!ready) Text("Откройте игровую страницу пит-стопа в гонке. При потере связи команды временно отключены.",fontSize=14.sp,color=Color(0xFFFFCC80))
    if(state.pitCursor==null) Text("Перед первым выбором укажите выделенную строку и значения из игры. После изменения геймпадом сверяйте их заново.",fontSize=14.sp)
    Text("1. Угол переднего крыла",fontSize=18.sp)
    Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(8.dp),verticalAlignment=androidx.compose.ui.Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text("На пит-стопе: ${n(v["nextFrontWing"])}",fontSize=22.sp,color=Color(0xFF90E0D0))
            Text("Сейчас на машине: ${n(v["frontWing"])}",fontSize=14.sp)
        }
        OutlinedButton(onClick={model.pitAdjust(0,false)},enabled=enabled,modifier=Modifier.width(72.dp).height(52.dp)) { Text("−",fontSize=25.sp) }
        OutlinedButton(onClick={model.pitAdjust(0,true)},enabled=enabled,modifier=Modifier.width(72.dp).height(52.dp)) { Text("+",fontSize=25.sp) }
    }
    Text("2. Замена переднего крыла",fontSize=18.sp)
    Text((if(state.pitSent) "Отправлен выбор: " else "Указано при сверке: ")+(state.pitRepair?.let { repairNames[it] } ?: "—"),fontSize=14.sp)
    Row(horizontalArrangement=Arrangement.spacedBy(8.dp)) {
        listOf(1,2,0).forEach { i -> FilterChip(selected=state.pitRepair==i,onClick={model.pitSelect(1,i)},enabled=enabled,label={Text(repairNames[i],fontSize=18.sp)},modifier=Modifier.weight(1f)) }
    }
    Text("3. Следующие шины",fontSize=18.sp)
    Text((if(state.pitSent) "Отправлен выбор: " else "Указано при сверке: ")+(state.pitTyre?.let { tyreNames[it] } ?: "—"),fontSize=14.sp)
    Row(horizontalArrangement=Arrangement.spacedBy(6.dp)) {
        tyreNames.indices.forEach { i -> TyreButton(i,state.pitTyre==i,enabled && f1Tyres,Modifier.weight(1f)) { model.pitSelect(2,i) } }
    }
    if(!f1Tyres) Text("Пять составов доступны для болидов F1. Для F2 набор шин отличается.",fontSize=13.sp)
    Text("Шины и ремонт по UDP не подтверждаются: подсветка показывает ваш выбор с планшета. Сверяйте результат в MFD. Новая резина и крыло будут установлены на следующем пит-стопе.",fontSize=13.sp,color=Color(0xFFAEBFC9))
    if(syncing) AlertDialog(onDismissRequest={syncing=false},title={Text("Сверка с игровым MFD")},text={
        Column(verticalArrangement=Arrangement.spacedBy(8.dp)) {
            Text("Это исходные показания, а не команды. Посмотрите значения в игре.",fontSize=14.sp)
            Text("Какая строка выделена?",fontSize=16.sp)
            Row(horizontalArrangement=Arrangement.spacedBy(4.dp)) {
                listOf("Крыло","Ремонт","Шины").forEachIndexed { i,name -> FilterChip(selected=row==i,onClick={row=i},label={Text(name)}) }
            }
            Text("Следующие шины в MFD (не текущие на машине)",fontSize=14.sp)
            tyreNames.indices.toList().chunked(3).forEach { items -> Row(horizontalArrangement=Arrangement.spacedBy(4.dp)) {
                items.forEach { i -> FilterChip(selected=tyre==i,onClick={tyre=i},label={Text(tyreNames[i])}) }
            } }
            Text("Repair Wing Damage / замена крыла",fontSize=14.sp)
            Row(horizontalArrangement=Arrangement.spacedBy(4.dp)) {
                repairNames.forEachIndexed { i,name -> FilterChip(selected=repair==i,onClick={repair=i},label={Text(name)}) }
            }
        }
    },confirmButton={TextButton(enabled=ready && row!=null && tyre!=null && repair!=null,onClick={model.syncPit(row!!,tyre!!,repair!!);syncing=false}){Text("Значения совпадают")}},dismissButton={TextButton(onClick={syncing=false}){Text("Отмена")}})
}
