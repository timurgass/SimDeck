package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.withTransform
import androidx.compose.ui.graphics.vector.PathParser
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

internal fun profileShortName(id: String) = when(id) {
    "f1-24" -> "F1 24"; "f1-25" -> "F1 25"; "beamng-default" -> "BEAMNG"; "acc" -> "ACC"; "ams2" -> "AMS2"; "ets2" -> "ETS2"; "ats" -> "ATS"; "snowrunner" -> "SNOWRUNNER"; "fs25" -> "FS25"; else -> "ПУЛЬТ"
}

/** Real controls and real telemetry in the compositions of the approved concepts. */
@Composable internal fun ProfileDashboard(state: DeckState, model: DeckModel,connection:()->Unit) {
    val design = LocalProfileDesign.current
    var section by rememberSaveable(state.profileId) { mutableStateOf("Обзор") }
    val sections = (listOf("Обзор") + (if(state.profileId=="fs25") listOf("Поля", "Цены") else if(state.profileId=="acc") listOf("Трасса") else emptyList()) + state.controls.map { it.page }).distinct()
    val selected = section.takeIf { it in sections } ?: "Обзор"
    val compact = LocalConfiguration.current.screenWidthDp >= 650 && LocalConfiguration.current.screenHeightDp < 750
    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(if(compact) 8.dp else 14.dp)) {
        val tabs=sections.map { name->name to if(name=="Обзор") when(state.profileId){"fs25"->"Техника";"beamng-default"->"Машина";"ets2", "ats" ->"Техника и маршрут";"snowrunner"->"Трансмиссия";else->name} else name }
        DeckHeader(state,tabs,selected,{ model.releaseAll();section=it },connection)
        TelemetryStatus(state)
        if(state.profileId=="fs25") Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(8.dp)) {
            for((id,label) in listOf("fs25Back" to "← Назад в игре","fs25Pause" to "Пауза времени")) state.controls.firstOrNull { it.id==id }?.let { a ->
                Control(label,a.key,a.id,false,state,model,Modifier.weight(1f),heightDp=78,tile=true)
            }
        }
        if(selected=="Цены" && state.profileId=="fs25") { Fs25PricesPanel(state); return@Column }
        if(selected=="Поля" && state.profileId=="fs25") { Fs25Fields(state); return@Column }
        if(selected=="Трасса" && state.profileId=="acc") { AccRaceScreen(state); return@Column }
        if(selected!="Обзор") { if(state.profileId=="fs25" && selected=="Хозяйство") Fs25Overview(state);DesignCard { Controls(state,model,selected,showPages=false) }; return@Column }
        if(state.profileId in setOf("fs25","ets2","ats","beamng-default","snowrunner")) { ReferenceDashboard(state,model);return@Column }
        ProfileBanner(state)
        BoxWithConstraints {
            val wide = maxWidth >= 650.dp
            val left: @Composable () -> Unit = {
                if(state.profileId in setOf("fs25","ets2","ats","beamng-default")) VehiclePanel(state)
                if(state.profileId=="snowrunner") SnowVehiclePanel(state,model)
                if(state.profileId=="ams2") RacingClassPanel()
                ProfileReadout(state, model)
            }
            val right: @Composable () -> Unit = { ProfileQuickPanel(state, model) }
            if (wide) Row(horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                Column(Modifier.weight(if(state.profileId == "snowrunner") .9f else 1.3f),verticalArrangement=Arrangement.spacedBy(12.dp)) { left() }
                Column(Modifier.weight(if(state.profileId == "snowrunner") 1.3f else 1f)) { right() }
            } else Column(verticalArrangement = Arrangement.spacedBy(12.dp)) { left(); right() }
        }
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text(when(state.inputAvailability) { "ready" -> "● ВВОД ГОТОВ"; "unfocused" -> "Открой окно игры на ПК"; "disabled" -> "Разреши ввод в Companion"; else -> state.status }, color = design.muted, fontSize = 12.sp, modifier = Modifier.weight(1f))

        }
        if(state.command.isNotBlank()) Text(state.command, color = design.accent, fontSize = 12.sp)

        if(state.profileId == "fs25") Fs25Overview(state)
    }
}

@Composable private fun Caption(value: String) { Text(value, color = LocalProfileDesign.current.muted, fontSize = 10.sp, fontWeight = FontWeight.Bold, letterSpacing = .8.sp) }
@Composable private fun Value(value: String, size: Int = 52, accent: Boolean = false) { Text(value, fontSize = size.sp, lineHeight = (size+5).sp, fontWeight = FontWeight.Black, color = if(accent) LocalProfileDesign.current.accent else Color(0xFFF5F7F5)) }
@Composable private fun StateRow(label: String, value: String) {
    val d = LocalProfileDesign.current
    Row(Modifier.fillMaxWidth().padding(vertical = 8.dp), horizontalArrangement = Arrangement.SpaceBetween) { Text(label, fontSize = 12.sp); Text(value, fontSize = 12.sp, color = d.accent, fontWeight = FontWeight.Bold) }
    HorizontalDivider(color = d.line)
}
private fun switchValue(state: DeckState, id: String, on: String = "ВКЛ", off: String = "ВЫКЛ") = when(state.telemetry?.actionStates?.get(id)) { true -> on; false -> off; null -> "—" }

@Composable private fun ProfileBanner(state: DeckState) {
    if(state.profileId in setOf("beamng-default","acc","fs25","snowrunner")) return
    val d = LocalProfileDesign.current
    val data = state.telemetry
    val fs = state.telemetry?.fs25
    val content: @Composable () -> Unit = {
        when(state.profileId) {
            "ets2", "ats" -> Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                Ets2Icon("etsMap", d.accent)
                Column(Modifier.weight(1f)) { Caption("МАРШРУТ ИЗ ИГРЫ"); Text("${data?.ets2Navigation?.remainingKm?.let { "%.0f км".format(it) } ?: "— км"}  ·  ${data?.ets2Navigation?.remainingMinutes?.let { "%.0f мин".format(it) } ?: "— мин"}", fontSize = 20.sp, fontWeight = FontWeight.Bold) }
                Column { Caption("ЛИМИТ"); Text(data?.ets2Navigation?.speedLimitKmh?.let { "%.0f км/ч".format(it) } ?: "—", color = d.accent) }
            }
            "ams2", "snowrunner", "fs25" -> Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                Column(Modifier.weight(1f)) {
                    Caption(if(state.profileId == "fs25") fs?.period?.ifBlank { "ХОЗЯЙСТВО" } ?: "ХОЗЯЙСТВО" else profileShortName(state.profileId))
                    Text(when(state.profileId) { "ams2" -> "Гонка под контролем."; "snowrunner" -> "Любая дорога\nначинается здесь."; else -> "Хороший день\nдля работы." }, fontSize = 26.sp, lineHeight = 30.sp, fontWeight = FontWeight.Bold)
                    Text(when(state.profileId) { "ams2" -> "Важные действия под рукой"; "snowrunner" -> "Трансмиссия · лебёдка · груз"; else -> fs?.saveName ?: "Сохраните ферму в игре" }, color = d.muted, fontSize = 11.sp)
                }
                if(state.profileId == "snowrunner") Compass(d.accent, d.line)
                if(state.profileId == "fs25") Fs25Icon("fs25Seeds", d.accent)
            }
            else -> Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) { Caption(if(state.profileId == "acc") "GT COCKPIT · ACC" else "VEHICLE CONTROL UNIT"); Text(if(state.demo) "● ДЕМО" else if(state.stale) "● НЕТ ДАННЫХ" else "● LIVE", color = d.accent, fontSize = 11.sp) }
        }
    }
    val gradient = when(state.profileId) { "ams2" -> Color(0xFF1D5759); "ets2", "ats" -> Color(0xFF24434A); "snowrunner" -> Color(0xFF344A3C); "fs25" -> Color(0xFF315B3A); else -> d.panel }
    Column(Modifier.fillMaxWidth().background(Brush.horizontalGradient(listOf(gradient,d.background)), RoundedCornerShape(d.radius.dp)).padding(if(state.profileId in setOf("ams2","snowrunner","fs25")) 22.dp else 14.dp)) { content() }
}

@Composable private fun Compass(accent: Color, line: Color) {
    Canvas(Modifier.size(80.dp)) { drawCircle(line, style = Stroke(2.dp.toPx())); val p = Path().apply { moveTo(size.width*.5f,size.height*.17f); lineTo(size.width*.72f,size.height*.77f); lineTo(size.width*.5f,size.height*.65f); lineTo(size.width*.28f,size.height*.77f); close() }; drawPath(p,accent) }
}

@Composable private fun RpmBlocks(data: Telemetry?, max: Double) {
    val d = LocalProfileDesign.current
    Canvas(Modifier.fillMaxWidth().height(14.dp)) { val step=size.width/10; for(i in 0..9) drawRect(if(i < (data?.rpm ?: 0.0)/max*10) d.accent else d.panelAlt, Offset(step*i,0f), androidx.compose.ui.geometry.Size((step-4.dp.toPx()).coerceAtLeast(1f),size.height)) }
}

@Composable private fun ProfileReadout(state: DeckState, model: DeckModel) {
    val d = LocalProfileDesign.current
    val data = state.telemetry
    val speed = data?.let { "%.0f".format(it.speedMps*3.6) } ?: "—"
    val gear = data?.let { Protocol.gear(it.gear,it.gearboxMode) } ?: "—"
    val rpm = data?.let { "%.0f".format(it.rpm) } ?: "—"
    val fuel = data?.fuelFraction?.let { "%.0f%%".format(it*100) } ?: "—"
    if(isScsTruck(state.profileId)) {
        Row(horizontalArrangement = Arrangement.spacedBy(12.dp), modifier = Modifier.height(IntrinsicSize.Min)) {
            Column(Modifier.weight(.9f).fillMaxHeight().background(d.panel,RoundedCornerShape(100.dp,100.dp,16.dp,16.dp)).border(1.dp,d.line,RoundedCornerShape(100.dp,100.dp,16.dp,16.dp)).padding(18.dp), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) { Caption("СКОРОСТЬ"); Value(speed,64,true); Caption("КМ/Ч") }
            Column(Modifier.weight(1.1f)) { DesignCard(8) { Caption("КРУИЗ-КОНТРОЛЬ"); Value(switchValue(state,"etsCruise"),30)
                QuickGrid(state,model,listOf("etsCruiseDown" to "−","etsCruise" to "КРУИЗ","etsCruiseUp" to "+"),columns=3,height=52)
                StateRow("Передача",gear); StateRow("Топливо",fuel)
            } }
        }
        return
    }
    DesignCard {
        when(state.profileId) {
            "beamng-default" -> {
                Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) { Caption("МАШИНА · " + when(data?.gearboxMode) { "arcade" -> "АРКАДА"; "realistic" -> "РЕАЛИЗМ"; else -> "—" }); LiveTag(state) }
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                    BoxWithConstraints(Modifier.weight(1.2f), contentAlignment = Alignment.Center) {
                        val diameter = minOf(maxWidth,205.dp)
                        Box(Modifier.size(diameter), contentAlignment = Alignment.Center) {
                        Canvas(Modifier.fillMaxSize()) { val pad=8.dp.toPx(); val sz=androidx.compose.ui.geometry.Size(size.width-pad*2,size.height-pad*2); drawArc(d.panelAlt,135f,270f,false,Offset(pad,pad),sz,style=Stroke(12.dp.toPx())); drawArc(d.accent,135f,270f*((data?.speedMps ?: 0.0)*3.6/240).coerceIn(0.0,1.0).toFloat(),false,Offset(pad,pad),sz,style=Stroke(12.dp.toPx())) }
                        Column(horizontalAlignment=Alignment.CenterHorizontally) { Value(speed,52); Caption("КМ/Ч") }
                        }
                    }
                    Column(Modifier.weight(.7f),horizontalAlignment=Alignment.CenterHorizontally) { Value(gear,76,true); Caption("ПЕРЕДАЧА"); Text("$rpm RPM",color=d.muted,fontSize=12.sp) }
                }
                StateRow("Двигатель",switchValue(state,"ignition","Работает","Выключен")); StateRow("Топливо",fuel)
            }
            "acc" -> { Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) { Caption("ОБОРОТЫ ДВИГАТЕЛЯ"); LiveTag(state) }; Value(rpm,34); RpmBlocks(data,data?.maxRpm ?: 8000.0); Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween,verticalAlignment=Alignment.CenterVertically) { Value(gear,90,true); Column { Value(speed,54); Caption("КМ/Ч") } }
                BoxWithConstraints { QuickGrid(state,model,listOf("accPitLimiter" to "ЛИМИТЕР","accHeadlights" to "ФАРЫ","accWipers" to "ДВОРНИКИ","accRainLight" to "ДОЖД. СВЕТ"),columns=if(maxWidth>=400.dp) 4 else 2,height=105) }
            }
            "ams2" -> { Caption("ПРИБОРЫ"); StateRow("Скорость","$speed КМ/Ч"); StateRow("Передача",gear); StateRow("Обороты","$rpm RPM"); RpmBlocks(data,data?.maxRpm ?: 8000.0) }
            "snowrunner" -> { Caption("ТРАНСМИССИЯ"); Caption("ТЕКУЩАЯ ПЕРЕДАЧА"); Value(gear,63,true); StateRow("Топливо",fuel); LinearProgressIndicator(progress={data?.fuelFraction?.toFloat() ?: 0f},modifier=Modifier.fillMaxWidth(),color=d.accent,trackColor=d.panelAlt); Text("Профиль кнопок. Живая телеметрия SnowRunner пока недоступна.",color=d.muted,fontSize=11.sp) }
            "fs25" -> {
                Row(verticalAlignment=Alignment.CenterVertically,horizontalArrangement=Arrangement.spacedBy(12.dp)) { Fs25Icon("fs25Motor",d.accent); Column { Caption("ВЫБРАННАЯ ТЕХНИКА"); Text((data?.vehicle ?: state.lastVehicle)?.takeIf { it.controlled }?.name?.ifBlank { "Неизвестная модель" } ?: "Ждём технику",fontSize=19.sp,fontWeight=FontWeight.Bold); Text("Состояние из игрового мода",fontSize=11.sp,color=d.muted) } }
                StateRow("Положение орудия",switchValue(state,"fs25Lower","Опущено","Поднято")); StateRow("Рабочий режим",switchValue(state,"fs25TurnOn","Работает","Выключен")); StateRow("Двигатель",switchValue(state,"fs25Motor","Запущен","Остановлен"))
                val tasks=state.telemetry?.fs25?.tasks.orEmpty(); val done=tasks.count { it.status==1 }
                Caption("ЗАДАЧИ ПЛАНА · ${if(tasks.isEmpty()) "—" else "$done / ${tasks.size}"}")
                LinearProgressIndicator(progress={if(tasks.isEmpty()) 0f else done.toFloat()/tasks.size},modifier=Modifier.fillMaxWidth(),color=d.accent,trackColor=d.panelAlt)
            }
        }
        if(state.stale && state.profileId !in setOf("snowrunner","fs25")) Text(telemetryHint(state.profileId),color=d.muted,fontSize=11.sp)
    }
}

@Composable private fun LiveTag(state: DeckState) { Text(if(state.demo) "● ДЕМО" else if(state.stale) { if(state.telemetry!=null) "● ПОСЛЕДНИЕ ДАННЫЕ" else "● ОЖИДАНИЕ" } else "● LIVE",fontSize=10.sp,color=LocalProfileDesign.current.accent) }

@Composable private fun ProfileQuickPanel(state: DeckState, model: DeckModel) {
    if(state.profileId=="acc") {
        val d=LocalProfileDesign.current; val acc=state.telemetry?.acc
        DesignCard { Caption("ШИНЫ И ТОРМОЗА")
            RoadDrawing("gt",heightOverride=220.dp)
            listOf(0,1,2,3).chunked(2).forEach { row -> Row(horizontalArrangement=Arrangement.spacedBy(9.dp)) {
                row.forEach { i -> val w=acc?.wheels?.getOrNull(i)
                    Column(Modifier.weight(1f).background(d.panelAlt).drawBehind { drawRect(d.accent, size=androidx.compose.ui.geometry.Size(4.dp.toPx(),size.height)) }.padding(12.dp)) {
                        Caption(listOf("ПЕРЕДНЯЯ ЛЕВАЯ","ПЕРЕДНЯЯ ПРАВАЯ","ЗАДНЯЯ ЛЕВАЯ","ЗАДНЯЯ ПРАВАЯ")[i])
                        Text(w?.let { "%.0f °C".format(it.coreTemperature) } ?: "—",fontSize=30.sp,fontWeight=FontWeight.Bold)
                        Text(w?.let { "%.1f PSI · тормоз %.0f °C".format(it.pressure,it.brakeTemperature) } ?: "— PSI · тормоз —",fontSize=10.sp,color=d.muted)
                    }
                }
            } }
            Text(acc?.let { "Двигатель %.0f °C · трасса %.0f °C".format(it.waterTemperature,it.roadTemperature) } ?: "Двигатель — · трасса —",fontSize=11.sp,color=d.muted)
            QuickGrid(state,model,listOf("accRequestPit" to "ПИТ-СТРАТЕГИЯ"),columns=1,height=85)
        }; return
    }
    val (title, entries) = when(state.profileId) {
        "beamng-default" -> "БЫСТРОЕ УПРАВЛЕНИЕ" to listOf("ignition" to "ЗАЖИГАНИЕ","lights" to "ФАРЫ","esc" to "ESC / TCS","fourWheelDrive" to "ПРИВОД","hazards" to "АВАРИЙКА","recoverRoad" to "ВЕРНУТЬ НА ДОРОГУ")
        "ams2" -> "БЫСТРЫЕ ДЕЙСТВИЯ" to listOf("amsIcmCycle" to "ICM","amsRequestPit" to "ПИТ-СТОП","amsPitLimiter" to "ЛИМИТЕР","amsCamera" to "КАМЕРА")
        "ets2", "ats" -> "КАБИНА И ТРАНСМИССИЯ" to listOf("etsLights" to "ФАРЫ","etsHighBeam" to "ДАЛЬНИЙ","etsDifferential" to "БЛОКИРОВКА","etsParkingBrake" to "РУЧНИК","etsHorn" to "СИГНАЛ","etsCamera" to "КАМЕРА")
        "snowrunner" -> "ПОЛЕВЫЕ ДЕЙСТВИЯ" to listOf("snowQuickWinch" to "ЛЕБЁДКА","snowPackCargo" to "ГРУЗ","snowMap" to "КАРТА","snowRecover" to "ЭВАКУАЦИЯ")
        "fs25" -> "РАБОТА С ОРУДИЕМ" to listOf("fs25Lower" to "ОПУСТИТЬ / ПОДНЯТЬ","fs25TurnOn" to "ВКЛ / ВЫКЛ","fs25Attach" to "ПРИЦЕПИТЬ / ОТЦЕПИТЬ","fs25Fold" to "СЛОЖИТЬ / РАЗЛОЖИТЬ")
        else -> "ДЕЙСТВИЯ" to emptyList()
    }
    DesignCard { Caption(title); QuickGrid(state,model,entries,columns=if(state.profileId=="snowrunner") 3 else 2,height=when(state.profileId) { "beamng-default","ets2", "ats" -> 98; "snowrunner" -> 112; else -> 124 }) }
}

@Composable internal fun TelemetryStatus(state: DeckState) {
    val d=LocalProfileDesign.current
    val text=when {
        !state.connected -> "Связь с ПК потеряна · последние показания сохранены"
        state.profileId in setOf("ams2") -> "Профиль управления · живая телеметрия этой игры пока не подключена"
        state.demo -> "Демонстрационные данные · игровой ввод выключен"
        state.stale && state.telemetry!=null -> "Обновление задержалось · показаны последние данные. Обычные кнопки доступны при активной игре."
        state.stale -> "Ожидание данных игры · обычные кнопки доступны при активной игре"
        else -> "● ЖИВЫЕ ДАННЫЕ · управление подключено отдельно"
    }
    Text(text,fontSize=12.sp,lineHeight=16.sp,color=if(state.stale && state.profileId !in setOf("ams2")) Color(0xFFFFC449) else d.muted,
        minLines=if(LocalConfiguration.current.screenWidthDp>=650) 1 else 2,
        maxLines=if(LocalConfiguration.current.screenWidthDp>=650) 1 else 2,modifier=Modifier.fillMaxWidth())
}

@Composable private fun RacingClassPanel() {
    var kind by rememberSaveable { mutableStateOf("formula") }
    DesignCard {
        Caption("АВТОМОБИЛЬ · ВЫБОР КЛАССА")
        Row(horizontalArrangement=Arrangement.spacedBy(6.dp)) {
            listOf("formula" to "Формула","gt" to "GT","car" to "Кузов").forEach { (id,label) ->
                FilterChip(selected=kind==id,onClick={kind=id},label={Text(label)})
            }
        }
        RoadDrawing(kind,heightOverride=240.dp)
        Text("Ручная схема класса · модель и состояния AMS2 пока не передаются",fontSize=11.sp,color=LocalProfileDesign.current.muted)
    }
}

@Composable private fun QuickGrid(state: DeckState, model: DeckModel, entries: List<Pair<String,String>>, columns: Int = 2, height: Int = 110) {
    val available=entries.mapNotNull { (id,label) -> state.controls.firstOrNull { it.id==id }?.let { it to label } }
    available.chunked(columns).forEach { row -> Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(8.dp)) { row.forEach { (a,label) -> key(state.profileId,a.id,a.gesture) { Control(label,if(a.id=="ignition") { if(state.ignitionReady) "Удерживать: запуск" else "Нажать: зажигание" } else if(a.gesture=="hold") "Удерживать · ${a.key}" else a.key,a.id,a.gesture=="hold",state,model,Modifier.weight(1f),height,tile=true,primary=a.id=="accRequestPit") } }; repeat(columns-row.size) { Spacer(Modifier.weight(1f)) } } }
}

@Composable internal fun ProfileActionIcon(id: String, color: Color) {
    val symbol = when {
        id=="radio" || id=="pushToTalk" -> "M19 6 H29 V27 C29 35 19 35 19 27 Z M12 24 V28 C12 44 36 44 36 28 V24 M24 40 V46 M18 46 H30"
        id=="menuBack" -> "M21 8 L7 24 L21 40 M7 24 H31 C42 24 42 39 31 39"
        id=="menu" -> "M8 12 H40 M8 24 H40 M8 36 H40"
        id.contains("Winch") -> "M8 8H40V38H8Z M12 16H36 M12 22H36 M12 28H36 M24 28V42 C24 47 33 47 33 40"
        id=="recoverRoad" || id=="snowRecover" -> "M11 18 C14 7 35 7 39 23 M39 23V11 M39 23H27 M37 31 C33 42 12 42 8 26 M8 26V38 M8 26H20"
        else -> null
    }
    if(symbol!=null) {
        val path=remember(symbol) { PathParser().parsePathString(symbol).toPath() }
        Canvas(Modifier.size(31.dp)) { withTransform({scale(size.width/48f,size.height/48f,Offset.Zero)}) { drawPath(path,color,style=Stroke(2.7f)) } }
        return
    }
    val equivalent = when {
        id=="ignition" || id.contains("Ignition") || id.contains("Starter") || id.endsWith("Engine") -> "etsEngine"
        id=="lights" || id.contains("Headlights") || id.contains("Flash") || id.contains("RainLight") -> "etsLights"
        id=="fourWheelDrive" || id=="differentials" || id=="esc" || id.contains("Differential") || id.endsWith("Awd") -> "etsDifferential"
        id=="hazards" -> "etsHazards"
        id.contains("Wipers") -> "etsWipers"
        id.contains("Camera") || id=="camera" -> "etsCamera"
        id.contains("Horn") || id=="horn" -> "etsHorn"
        id.contains("ParkingBrake") -> "etsParkingBrake"
        id.contains("Map") || id=="map" || id.contains("Winch") || id=="recoverRoad" || id=="snowRecover" -> "etsMap"
        id.contains("Pit") || id.contains("Icm") || id.contains("Cargo") -> "etsRouteAdvisor"
        id=="pitLimiter" || id=="overtake" -> "etsCruise"
        id=="pause" -> "etsPause"
        else -> "etsDashboard"
    }
    Ets2Icon(equivalent,color)
}
