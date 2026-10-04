package dev.simdeck

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.horizontalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Live counterparts of the approved compositions; missing sources remain explicitly unknown. */
@Composable internal fun ReferenceDashboard(state:DeckState,model:DeckModel) {
    val d=LocalProfileDesign.current
    val v=(state.telemetry?.vehicle ?: state.lastVehicle)?.takeIf { it.controlled }
    val farm=state.profileId=="fs25"
    val compact=LocalConfiguration.current.screenWidthDp>=650 && LocalConfiguration.current.screenHeightDp<750
    if(isScsTruck(state.profileId)) {
        TruckNavigator(state,model)
        ReferenceButtons(state,model,listOf("etsCruiseDown" to "Круиз −","etsCruise" to "Круиз","etsCruiseUp" to "Круиз +","etsMap" to "Карта в игре"),4)
        var details by rememberSaveable { mutableStateOf(false) }
        OutlinedButton(onClick={details=!details},modifier=Modifier.fillMaxWidth()) { Text(if(details) "Скрыть состояние грузовика" else "Состояние грузовика и прицепа") }
        if(details && v!=null) DesignCard {Text(v.name,fontWeight=FontWeight.Bold,color=d.accent);DamageScheme(v)}
        return
    }
    val left:@Composable ()->Unit={
        when(state.profileId) {
            "snowrunner" -> SnowDiagram()
            else -> DesignCard(if(compact) 7 else 12,if(compact) 12 else 17) {
                Text(when(state.profileId){"fs25"->"ТЕХНИКА И ОРУДИЯ";"ets2", "ats" ->"СОСТОЯНИЕ ГРУЗОВИКА";else->"ПОВРЕЖДЕНИЯ МАШИНЫ"},fontSize=if(compact) 20.sp else 22.sp,lineHeight=if(compact) 24.sp else 26.sp,fontWeight=FontWeight.Black)
                Text(v?.name ?: "Ждём сведения о технике",fontSize=if(compact) 17.sp else 20.sp,lineHeight=if(compact) 21.sp else 24.sp,color=d.accent,fontWeight=FontWeight.Bold)
                Text(v?.let { "${vehicleLabels[it.kind]} · ${it.axleCount?.let { n->"$n оси" } ?: "колёса неизвестны"}" } ?: "Нужны данные мода / плагина",fontSize=12.sp,lineHeight=16.sp,color=d.muted)
                if(farm) {
                    val classes=listOf("tractor" to "Трактор","combine" to "Комбайн","loader" to "Погрузчик")
                    Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(8.dp)) {
                        (classes+listOf("other" to "Прочая техника")).forEach { (kind,name)->
                            val active=if(kind=="other") v!=null && classes.none { it.first==v.kind } else v?.kind==kind
                            Text(name,modifier=Modifier.background(if(active) d.panelAlt else d.panel).border(1.dp,if(active) d.accent else d.line).padding(if(compact) 7.dp else 10.dp),color=if(active) d.accent else d.muted,fontSize=13.sp,lineHeight=17.sp)
                        }
                    }
                    val linked=v?.attachments?.firstOrNull { it.parentId==v.id && (it.mount!="unknown" || it.kind=="header") }
                    if(v!=null) key(v.id,linked?.id) { FarmDrawing(v.kind,linked,heightOverride=if(compact) 220.dp else 300.dp) }
                    if(linked!=null) Text("${vehicleLabels[linked.kind] ?: linked.name} · ${when(linked.lowered){true->"ОПУЩЕНО";false->"ПОДНЯТО";null->"положение неизвестно"}}",fontSize=16.sp,lineHeight=20.sp,color=d.accent)
                    if(v?.attachments.isNullOrEmpty()) Text("Подключённых орудий нет",color=d.muted,fontSize=13.sp,lineHeight=17.sp)
                    v?.attachments?.filter { it.id!=linked?.id }?.forEach { a->Text("${a.name} · ${vehicleLabels[a.kind]}",color=d.muted,fontSize=13.sp,lineHeight=17.sp);FarmDrawing(a.kind,null,small=true) }
                } else if(v!=null) {
                    if(isScsTruck(state.profileId)) DamageScheme(v) else RoadDrawing(v.kind,v.wheels,heightOverride=430.dp)
                }
                Text(if(state.stale) "Последние показания · обновление задержалось" else "Состояние из игры",fontSize=12.sp,lineHeight=16.sp,color=d.muted)
            }
        }
    }
    val right:@Composable ()->Unit={
        when(state.profileId) {
            "fs25" -> DesignCard(if(compact) 4 else 12,if(compact) 12 else 17) {
                Text(v?.name ?: "СОСТОЯНИЕ ТЕХНИКИ",fontSize=22.sp,lineHeight=26.sp,fontWeight=FontWeight.Bold)
                listOf("front" to "ПЕРЕДНЯЯ СЦЕПКА","rear" to "ЗАДНЯЯ СЦЕПКА").forEach { (mount,label)->
                    Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) { Text(label,fontSize=10.sp,lineHeight=14.sp,color=d.muted);Text(v?.attachments?.filter { it.mount==mount }?.joinToString { it.name }?.ifBlank { "Свободна" } ?: "—",fontSize=14.sp,lineHeight=18.sp,modifier=Modifier.weight(1f).padding(start=12.dp)) }

                }
                HorizontalDivider(color=d.line)
                ReferenceStateRow("Положение орудия",state.telemetry?.actionStates?.get("fs25Lower"),"ОПУЩЕНО","ПОДНЯТО","fs25Lower",true)
                ReferenceStateRow("Рабочий режим",state.telemetry?.actionStates?.get("fs25TurnOn"),"РАБОТАЕТ","ВЫКЛЮЧЕНО","fs25TurnOn",true)
                val fold=v?.attachments?.firstOrNull { it.fold!=null }?.fold
                Row(verticalAlignment=Alignment.CenterVertically,horizontalArrangement=Arrangement.spacedBy(12.dp)) { Fs25Icon("fs25Fold",d.accent);Column {Text("Положение складывания",fontSize=14.sp,lineHeight=18.sp,color=d.muted);Text(fold?.let { "%.0f%%".format(it*100) } ?: "НЕТ ДАННЫХ",fontSize=19.sp,lineHeight=23.sp,color=d.accent)} }
                ReferenceButtons(state,model,listOf("fs25Lower" to "Поднять / опустить","fs25TurnOn" to "Включить / выключить","fs25Fold" to "Сложить / разложить"),1)
            }
            "ets2", "ats" -> DesignCard {
                Text("НАВИГАТОР",fontSize=23.sp,lineHeight=27.sp,fontWeight=FontWeight.Black)
                Text("Маршрут из игры",fontSize=16.sp,lineHeight=20.sp,color=d.muted)
                val n=state.telemetry?.ets2Navigation
                Text("${n?.remainingKm?.let { "%.0f км".format(it) } ?: "— км"} · ${n?.remainingMinutes?.let { "%.0f мин".format(it) } ?: "— мин"}",fontSize=25.sp,lineHeight=29.sp,fontWeight=FontWeight.Bold)
                Ets2RoadMap(state)
                Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) { Column {Text("СКОРОСТЬ",fontSize=11.sp,lineHeight=15.sp,color=d.muted);Text(state.telemetry?.let { "%.0f км/ч".format(it.speedMps*3.6) } ?: "—",fontSize=26.sp,lineHeight=30.sp,fontWeight=FontWeight.Bold)};Column {Text("ОГРАНИЧЕНИЕ",fontSize=11.sp,lineHeight=15.sp,color=d.muted);Text(n?.speedLimitKmh?.let { "%.0f км/ч".format(it) } ?: "—",fontSize=26.sp,lineHeight=30.sp,fontWeight=FontWeight.Bold)} }
                ReferenceButtons(state,model,listOf("etsCruiseDown" to "Круиз −","etsCruise" to "Круиз","etsCruiseUp" to "Круиз +","etsLights" to "Свет"),2)
            }
            "beamng-default" -> DesignCard {
                listOf(Triple("Кузов","cabin","lights"),Triple("Радиатор","radiator","ignition"),Triple("Передняя подвеска","suspension","fourWheelDrive"),Triple("Двигатель","engine","ignition"),Triple("Трансмиссия","transmission","fourWheelDrive")).forEach { (name,key,icon)->
                    Row(verticalAlignment=Alignment.CenterVertically,horizontalArrangement=Arrangement.spacedBy(12.dp)) { ProfileActionIcon(icon,damageColor(v?.wear?.get(key)?.times(100)));Column(Modifier.weight(1f)) {Text(name,fontSize=19.sp,lineHeight=23.sp,fontWeight=FontWeight.Bold);Text(v?.wear?.get(key)?.let { "Урон %.0f%%".format(it*100) } ?: "Нет данных узла",fontSize=14.sp,lineHeight=18.sp,color=d.muted)} };HorizontalDivider(color=d.line)
                }
                Text("Состояния узлов требуют расширения мода BeamNG. Серый цвет означает отсутствие данных.",fontSize=12.sp,lineHeight=16.sp,color=d.muted)
            }
            "snowrunner" -> SnowStates(state,model)
        }
    }
    BoxWithConstraints {
        if(maxWidth>=650.dp) Row(horizontalArrangement=Arrangement.spacedBy(20.dp)) { Column(Modifier.weight(if(farm) 1.8f else 1.1f)) {left()};Column(Modifier.weight(1f)) {right()} }
        else Column(verticalArrangement=Arrangement.spacedBy(14.dp)) {left();right()}
    }
    if(state.profileId=="beamng-default") ReferenceButtons(state,model,listOf("ignition" to "Зажигание","lights" to "Фары","hazards" to "Аварийка","recoverRoad" to "Восстановить"),4)
    if(state.profileId=="snowrunner") ReferenceButtons(state,model,listOf("snowQuickWinch" to "Лебёдка","snowPackCargo" to "Груз","snowMap" to "Карта","snowRecover" to "Эвакуация"),4)
}

@Composable private fun ReferenceStateRow(label:String,value:Boolean?,on:String,off:String,icon:String,farm:Boolean=false) {
    val d=LocalProfileDesign.current
    Row(Modifier.fillMaxWidth().padding(vertical=3.dp),verticalAlignment=Alignment.CenterVertically,horizontalArrangement=Arrangement.spacedBy(12.dp)) {
        if(farm) Fs25Icon(icon,d.accent) else ProfileActionIcon(icon,d.accent)
        Column(Modifier.weight(1f)) { Text(label,fontSize=12.sp,lineHeight=16.sp,color=d.muted);Text(when(value){true->on;false->off;null->"НЕТ ДАННЫХ"},fontSize=16.sp,lineHeight=20.sp,fontWeight=FontWeight.Bold,color=if(value==null)d.muted else d.accent) }
    }
}
@Composable private fun DamageScheme(v:VehicleInfo) {
    val d=LocalProfileDesign.current
    BoxWithConstraints {
        val content:@Composable ()->Unit={ RoadDrawing(v.kind,v.wheels,v.attachments.firstOrNull { it.kind=="trailer" },heightOverride=410.dp,wear=v.wear) }
        if(maxWidth>=380.dp) Row(horizontalArrangement=Arrangement.spacedBy(9.dp),verticalAlignment=Alignment.CenterVertically) {
            Column(Modifier.weight(.8f),verticalArrangement=Arrangement.spacedBy(28.dp)) { listOf("engine" to "Двигатель","transmission" to "Коробка","wheels" to "Колёса").forEach { (key,label)->DamageLabel(label,v.wear[key]) } }
            Column(Modifier.weight(1.3f)) {content()}
            Column(Modifier.weight(.8f),verticalArrangement=Arrangement.spacedBy(28.dp)) { listOf("cabin" to "Кабина","chassis" to "Шасси").forEach { (key,label)->DamageLabel(label,v.wear[key]) };Text("Прицеп\nДанные урона не переданы",fontSize=12.sp,lineHeight=16.sp,color=d.muted) }
        } else {content();v.wear.forEach { (key,value)->DamageLabel(mapOf("engine" to "Двигатель","transmission" to "Коробка","wheels" to "Колёса","cabin" to "Кабина","chassis" to "Шасси")[key] ?: key,value) } }
    }
    Text("● Исправно 0–5%    ● Износ 6–30%    ● Повреждено >30%",fontSize=11.sp,lineHeight=15.sp,color=d.muted)
}
@Composable private fun DamageLabel(label:String,value:Double?) { Column {Text(label,fontSize=13.sp,lineHeight=17.sp);Text(value?.let { "%.0f%%".format(it*100) } ?: "—",fontSize=24.sp,lineHeight=28.sp,fontWeight=FontWeight.Bold,color=damageColor(value?.times(100)))} }
@Composable private fun SnowDiagram() {
    val d=LocalProfileDesign.current;var count by rememberSaveable { mutableIntStateOf(6) }
    DesignCard {Text("ПРИВОД И БЛОКИРОВКА",fontSize=22.sp,lineHeight=26.sp,fontWeight=FontWeight.Black);Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(6.dp)) {listOf(4,6,8,10).forEach { n->FilterChip(selected=count==n,onClick={count=n},label={Text("$n колёс")}) }};Text("Ручной выбор схемы",fontSize=12.sp,lineHeight=16.sp,color=d.muted);RoadDrawing(if(count==4) "suv" else "truck",List(count){i->VehicleWheel(if(i%2==0)-1.0 else 1.0,(i/2).toDouble(),null)},heightOverride=440.dp);Text("Колёса и приводы показаны без подтверждённых состояний.",fontSize=12.sp,lineHeight=16.sp,color=d.muted)}
}
@Composable internal fun ReferenceButtons(state:DeckState,model:DeckModel,entries:List<Pair<String,String>>,columns:Int) {
    val compact=LocalConfiguration.current.screenWidthDp>=650 && LocalConfiguration.current.screenHeightDp<750
    BoxWithConstraints {
        val count=if(maxWidth<400.dp) minOf(columns,2) else columns
        Column(verticalArrangement=Arrangement.spacedBy(if(compact) 8.dp else 10.dp)) {entries.chunked(count).forEach {row->Row(horizontalArrangement=Arrangement.spacedBy(10.dp)){row.forEach { (id,label)->val a=state.controls.firstOrNull{it.id==id};Column(Modifier.weight(1f)){if(a!=null)Control(label,a.key,id,a.gesture=="hold",state,model,Modifier.fillMaxWidth(),if(columns==1) {if(compact) 52 else 60} else 96,tile=true)} };repeat(count-row.size){Spacer(Modifier.weight(1f))}}} }
    }
}

@Composable private fun SnowStates(state:DeckState,model:DeckModel) {
    val d=LocalProfileDesign.current
    val card:@Composable (String,String)->Unit={id,label->DesignCard {
        ProfileActionIcon(id,d.muted)
        Text(label,fontSize=17.sp,lineHeight=22.sp,fontWeight=FontWeight.Bold)
        Text("СОСТОЯНИЕ НЕИЗВЕСТНО",fontSize=13.sp,lineHeight=17.sp,color=d.muted)
        ReferenceButtons(state,model,listOf(id to if(id=="snowAwd") "Переключить AWD" else "Переключить блокировку"),1)
    }}
    Column(verticalArrangement=Arrangement.spacedBy(12.dp)) {
        BoxWithConstraints {if(maxWidth>=360.dp) Row(horizontalArrangement=Arrangement.spacedBy(12.dp)) {Column(Modifier.weight(1f)){card("snowAwd","ПОЛНЫЙ ПРИВОД")};Column(Modifier.weight(1f)){card("snowDifferential","БЛОКИРОВКА ДИФФЕРЕНЦИАЛА")}}
            else Column(verticalArrangement=Arrangement.spacedBy(12.dp)){card("snowAwd","ПОЛНЫЙ ПРИВОД");card("snowDifferential","БЛОКИРОВКА ДИФФЕРЕНЦИАЛА")}}
        Text("Игра пока не передаёт телеметрию. Нажатие не подтверждает состояние трансмиссии.",fontSize=12.sp,lineHeight=16.sp,color=d.muted)
    }
}
