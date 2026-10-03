package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Shared F1 24/25 layout: the approved race dashboard, backed by live data. */
@Composable internal fun F1Dashboard(state: DeckState, model: DeckModel,connection:()->Unit) {
    val design = LocalProfileDesign.current
    var destination by rememberSaveable(state.profileId) { mutableStateOf("condition") }
    val tabs = listOf("race" to "ГОНКА", "mfd" to "MFD", "condition" to "СОСТОЯНИЕ БОЛИДА", "pit" to "ПИТ-СТОП", "track" to "КАРТА", "menu" to "MENU CONTROLS") + state.controls.map { it.page }.distinct().filterNot { it in setOf("Control Scheme","MFD","Menu Controls","Трасса") }.map { "custom:$it" to it }
    fun navigate(next: String) { model.releaseAll(); destination = next }
    Column(Modifier.fillMaxSize().verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(14.dp)) {
        DeckHeader(state,tabs,destination,::navigate,connection)
        if (destination == "race") {
            BoxWithConstraints {
                if (maxWidth >= 650.dp) Row(horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                    Column(Modifier.weight(1.55f)) { F1RaceInstruments(state, model, ::navigate) }
                    Column(Modifier.weight(1f)) { F1RaceSummary(state) }
                } else Column(verticalArrangement = Arrangement.spacedBy(14.dp)) {
                    F1RaceInstruments(state, model, ::navigate)
                    F1RaceSummary(state)
                }
            }
        } else if(destination!="condition") {
            val data = state.telemetry
            Row(Modifier.fillMaxWidth().background(design.panel).padding(12.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                Text("${data?.let { Protocol.gear(it.gear, it.gearboxMode) } ?: "—"}  ·  ${data?.let { "%.0f".format(it.speedMps * 3.6) } ?: "—"} КМ/Ч", fontWeight = FontWeight.Bold)
                Text("${data?.let { "%.0f".format(it.rpm) } ?: "—"} RPM", color = design.accent, fontWeight = FontWeight.Bold)
            }
        }
        TelemetryStatus(state)
        if(destination=="condition") DesignCard {
            Text("БОЛИД · ШИНЫ И ПОВРЕЖДЕНИЯ",fontSize=18.sp,fontWeight=FontWeight.Bold)
            F1Schematic(state.telemetry?.f1)
        }
        if(destination!="condition") DesignCard {
            key(destination) {
                F1Controls(state, model,
                    initialSection = when (destination) { "mfd", "pit" -> "MFD"; "track" -> "Трасса"; "menu" -> "Menu Controls"; else -> if(destination.startsWith("custom:")) destination.removePrefix("custom:") else "Control Scheme" },
                    initialPanel = if (destination == "pit") "mfdPit" else "mfdSetup",
                    showSections = false, showRaceShortcuts = false)
            }
        }
    }
}

@Composable private fun F1RaceInstruments(state: DeckState, model: DeckModel, navigate: (String) -> Unit) {
    val design = LocalProfileDesign.current
    val data = state.telemetry
    val player = data?.f1?.race?.drivers?.firstOrNull { it.player }
    DesignCard {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text("ГОНКА · ${gameDisplayName(state.profileId, state.profileName)}", fontSize = 12.sp, fontWeight = FontWeight.Bold)
            Text(if (data == null) "НЕТ ДАННЫХ" else "КРУГ ${player?.lap ?: "—"}", fontSize = 12.sp, color = design.muted)
        }
        BoxWithConstraints {
        val narrow = maxWidth < 350.dp
        Column {
        Row(Modifier.fillMaxWidth().heightIn(min = if (narrow) 140.dp else 170.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(if (narrow) 10.dp else 18.dp)) {
            Column(Modifier.weight(.75f)) {
                Text(data?.let { Protocol.gear(it.gear, it.gearboxMode) } ?: "—", fontSize = if (narrow) 72.sp else 90.sp, lineHeight = 94.sp, fontWeight = FontWeight.Black, color = design.accent)
                F1Caption("ПЕРЕДАЧА")
            }
            Column(Modifier.weight(1f)) {
                Text(data?.let { "%.0f".format(it.speedMps * 3.6) } ?: "—", fontSize = if (narrow) 38.sp else 52.sp, fontWeight = FontWeight.Black)
                F1Caption("КМ/Ч")
            }
            if (!narrow) Column(Modifier.weight(.9f), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                F1Caption("ОБОРОТЫ")
                Text(data?.let { "%.0f".format(it.rpm) } ?: "—", fontSize = if (narrow) 18.sp else 24.sp, fontWeight = FontWeight.Bold)
                val fraction = ((data?.rpm ?: 0.0) / (data?.maxRpm ?: 15000.0)).coerceIn(0.0, 1.0)
                Canvas(Modifier.fillMaxWidth().height(35.dp)) {
                    val step = size.width / 12
                    for (i in 0 until 12) {
                        val height = size.height * (.3f + .7f * (i + 1) / 12)
                        drawRect(if (i >= fraction * 12) design.line else if (i >= 9) androidx.compose.ui.graphics.Color(0xFFFFBC55) else design.accent,
                            Offset(i * step, size.height - height), Size((step - 3.dp.toPx()).coerceAtLeast(1f), height))
                    }
                }
            }
        }
        if(narrow) Row(Modifier.fillMaxWidth().padding(top=8.dp),horizontalArrangement=Arrangement.spacedBy(10.dp),verticalAlignment=Alignment.CenterVertically) {
            F1Caption("RPM"); Text(data?.let { "%.0f".format(it.rpm) } ?: "—",fontSize=18.sp,fontWeight=FontWeight.Bold)
            val fraction=((data?.rpm ?: 0.0)/(data?.maxRpm ?: 15000.0)).coerceIn(0.0,1.0)
            Canvas(Modifier.weight(1f).height(25.dp)) { val step=size.width/12; for(i in 0..11) { val h=size.height*(.3f+.7f*(i+1)/12); drawRect(if(i>=fraction*12) design.line else if(i>=9) androidx.compose.ui.graphics.Color(0xFFFFBC55) else design.accent,Offset(i*step,size.height-h),Size((step-3.dp.toPx()).coerceAtLeast(1f),h)) } }
        }
        }
        }
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(7.dp)) {
            F1QuickAction("Радио", "radio", false, controlsAvailable(state), Modifier.weight(1f)) { model.press("radio", false) }
            F1QuickAction("Пит-стоп", "pit", true, true, Modifier.weight(1f)) { navigate("pit") }
            F1QuickAction("MFD", "mfd", false, true, Modifier.weight(1f)) { navigate("mfd") }
        }
        if (data == null) Text(telemetryHint(state.profileId), fontSize = 12.sp, color = design.muted)
    }
}

@Composable private fun F1Caption(text: String) { Text(text, fontSize = 10.sp, letterSpacing = 1.sp, fontWeight = FontWeight.Bold, color = LocalProfileDesign.current.muted) }

@Composable private fun F1QuickAction(label: String, icon: String, hot: Boolean, enabled: Boolean, modifier: Modifier, onClick: () -> Unit) {
    val design = LocalProfileDesign.current
    Button(onClick = onClick, enabled = enabled, modifier = modifier.height(58.dp), shape = RoundedCornerShape(3.dp),
        contentPadding = PaddingValues(8.dp), colors = ButtonDefaults.buttonColors(containerColor = if (hot) design.accent else design.panelAlt, contentColor = androidx.compose.ui.graphics.Color.White)) {
        Canvas(Modifier.size(17.dp)) {
            val color = androidx.compose.ui.graphics.Color.White
            when (icon) {
                "radio" -> { drawRect(color, Offset(0f, size.height * .35f), Size(size.width, size.height * .6f), style = Stroke(1.5.dp.toPx())); drawLine(color, Offset(size.width * .15f, size.height * .3f), Offset(size.width * .8f, 0f), 1.5.dp.toPx()); drawCircle(color, size.width * .12f, Offset(size.width * .25f, size.height * .65f)); drawLine(color, Offset(size.width * .5f, size.height * .6f), Offset(size.width * .85f, size.height * .6f), 1.5.dp.toPx()) }
                "pit" -> { drawLine(color, Offset(size.width * .15f, 0f), Offset(size.width * .15f, size.height), 1.5.dp.toPx()); val p = Path().apply { moveTo(size.width * .2f, 0f); lineTo(size.width, size.height * .25f); lineTo(size.width * .2f, size.height * .5f); close() }; drawPath(p, color) }
                else -> { drawRect(color, style = Stroke(1.5.dp.toPx())); drawLine(color, Offset(0f, size.height * .3f), Offset(size.width, size.height * .3f), 1.5.dp.toPx()) }
            }
        }
        Spacer(Modifier.width(6.dp)); Text(label, fontSize = 12.sp, fontWeight = FontWeight.Bold, maxLines = 1)
    }
}

@Composable private fun F1RaceSummary(state: DeckState) {
    val design = LocalProfileDesign.current
    val data = state.telemetry
    val race = data?.f1?.race
    val drivers = race?.drivers.orEmpty()
    val index = drivers.indexOfFirst { it.player }
    val neighbours = if (race?.fresh == true && index >= 0) listOf(drivers.getOrNull(index - 1), drivers[index], drivers.getOrNull(index + 1)) else listOf(null, null, null)
    DesignCard(spacingDp = 8) {
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) { F1Caption("ТРАССА"); F1Caption("ПОРЯДОК") }
        F1CircuitMap(state.telemetry?.f1?.race, state.stale, compact = true, heightDp = 105)
        Row(horizontalArrangement = Arrangement.spacedBy(5.dp)) {
            neighbours.forEach { driver ->
                Column(Modifier.weight(1f).background(if (driver?.player == true) design.panelAlt else design.background).padding(8.dp)) {
                    Text(driver?.position?.takeIf { it > 0 }?.toString()?.padStart(2, '0') ?: "—", fontWeight = FontWeight.Bold, color = if (driver?.player == true) design.accent else design.muted)
                    Text(if (driver?.player == true) "ВЫ" else driver?.shortName ?: "—", fontSize = 12.sp, fontWeight = FontWeight.Bold)
                }
            }
        }
        HorizontalDivider(color = design.line)
        val compound = when (data?.f1?.values?.get("compound")?.toInt()) { 16,20 -> "S"; 17,21 -> "M"; 18,22 -> "H"; 7 -> "I"; 8,15 -> "W"; else -> "—" }
        val wear = data?.f1?.wheels?.mapNotNull { it.wear }?.takeIf { it.size == 4 }?.average()
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Column { F1Caption("ШИНЫ"); Text(compound, fontWeight = FontWeight.Bold) }
            Column { F1Caption("ИЗНОС"); Text(wear?.let { "%.0f%%".format(it) } ?: "—", fontWeight = FontWeight.Bold) }
            Column { F1Caption("ТОПЛИВО"); Text(data?.fuelFraction?.let { "%.0f%%".format(it * 100) } ?: "—", fontWeight = FontWeight.Bold) }
        }
    }
}
