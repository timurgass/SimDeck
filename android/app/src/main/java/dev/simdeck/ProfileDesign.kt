package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.runtime.Composable
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.material3.Text

internal data class ProfileDesign(
    val background: Color,
    val panel: Color,
    val panelAlt: Color,
    val accent: Color,
    val muted: Color,
    val line: Color,
    val radius: Int,
    val tag: String
)

internal fun profileDesign(id: String): ProfileDesign = when (id) {
    "f1-24" -> ProfileDesign(Color(0xFF17191D), Color(0xFF202226), Color(0xFF2B2526), Color(0xFFE63537), Color(0xFFB7AFB0), Color(0xFF484044), 5, "RACE CONTROL")
    "f1-25" -> ProfileDesign(Color(0xFF08151B), Color(0xFF0E242D), Color(0xFF17333D), Color(0xFF21C6D7), Color(0xFF8DB0BA), Color(0xFF31515C), 20, "PRECISION DISPLAY")
    "beamng-default" -> ProfileDesign(Color(0xFF181C1E), Color(0xFF242A2E), Color(0xFF30383C), Color(0xFFF5842D), Color(0xFFAEBBBD), Color(0xFF435054), 8, "VEHICLE CONTROL UNIT")
    "acc" -> ProfileDesign(Color(0xFF111314), Color(0xFF1D2020), Color(0xFF2A3030), Color(0xFFFFAE3B), Color(0xFFA9B9B2), Color(0xFF3A4843), 5, "GT COCKPIT")
    "ams2" -> ProfileDesign(Color(0xFF0C1C24), Color(0xFF132E37), Color(0xFF1C4248), Color(0xFFB8E44B), Color(0xFFA6C4C0), Color(0xFF356061), 18, "COCKPIT CONTROL")
    "ets2" -> ProfileDesign(Color(0xFF1B2325), Color(0xFF273133), Color(0xFF334144), Color(0xFFDDB66A), Color(0xFFB0BEB7), Color(0xFF4C5F59), 20, "LONG HAUL DASHBOARD")
    "snowrunner" -> ProfileDesign(Color(0xFF1A221E), Color(0xFF27322B), Color(0xFF364239), Color(0xFFD8A961), Color(0xFFAFBDAD), Color(0xFF4C5B4B), 7, "FIELD OPERATIONS")
    "fs25" -> ProfileDesign(Color(0xFF17241D), Color(0xFF223428), Color(0xFF314837), Color(0xFF76BB59), Color(0xFFA6BEA8), Color(0xFF49654C), 17, "FARM OPERATIONS")
    else -> ProfileDesign(Color(0xFF0B1115), Color(0xFF182026), Color(0xFF23343A), Color(0xFFF2B84B), Color(0xFF8D9FA8), Color(0xFF304049), 16, "YOUR RIG. ONE TOUCH.")
}

internal val LocalProfileDesign = compositionLocalOf { profileDesign("") }

@Composable
private fun DesignCard(content: @Composable ColumnScope.() -> Unit) {
    val design = LocalProfileDesign.current
    Card(
        colors = CardDefaults.cardColors(containerColor = design.panel),
        shape = RoundedCornerShape(design.radius.dp),
        modifier = Modifier.fillMaxWidth().border(1.dp, design.line, RoundedCornerShape(design.radius.dp))
    ) { Column(Modifier.fillMaxWidth().padding(17.dp), verticalArrangement = Arrangement.spacedBy(12.dp), content = content) }
}

@Composable
private fun DesignLabel(text: String) {
    Text(text, fontSize = 11.sp, letterSpacing = 1.sp, fontWeight = FontWeight.Bold, color = LocalProfileDesign.current.muted)
}

@Composable
private fun DesignValue(label: String, value: String, unit: String = "", modifier: Modifier = Modifier) {
    val design = LocalProfileDesign.current
    Column(modifier) {
        DesignLabel(label)
        Row(verticalAlignment = Alignment.Bottom) {
            Text(value, fontSize = 27.sp, fontWeight = FontWeight.Bold, color = design.accent)
            if (unit.isNotBlank()) Text(" $unit", fontSize = 11.sp, color = design.muted, modifier = Modifier.padding(bottom = 5.dp))
        }
    }
}

@Composable
private fun RpmStrip(data: Telemetry?, blocks: Int = 12) {
    val design = LocalProfileDesign.current
    val filled = ((data?.rpm ?: 0.0) / (data?.maxRpm ?: 15000.0) * blocks).toInt().coerceIn(0, blocks)
    Canvas(Modifier.fillMaxWidth().height(12.dp)) {
        val step = size.width / blocks
        for (i in 0 until blocks) {
            drawRoundRect(
                color = if (i >= filled) design.panelAlt else if (i >= blocks - 2) Color(0xFFFF7770) else design.accent,
                topLeft = Offset(i * step + 2.dp.toPx(), 0f),
                size = androidx.compose.ui.geometry.Size((step - 4.dp.toPx()).coerceAtLeast(1f), size.height)
            )
        }
    }
}

@Composable
private fun DataStatus(state: DeckState) {
    val design = LocalProfileDesign.current
    Text(if (state.demo) "● ДЕМО" else if (state.stale) "● НЕТ ДАННЫХ" else "● LIVE", color = if (state.stale) design.muted else design.accent, fontSize = 11.sp, fontWeight = FontWeight.Bold)
}

@Composable
private fun InstrumentHeading(state: DeckState, title: String) {
    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
        DesignLabel(title)
        DataStatus(state)
    }
}

@Composable
private fun TelemetryHint(state: DeckState) {
    if (state.stale && !state.demo) Text(telemetryHint(state.profileId), color = LocalProfileDesign.current.accent, fontSize = 11.sp)
}

@Composable
internal fun ProfileInstruments(state: DeckState, data: Telemetry?) {
    val design = LocalProfileDesign.current
    val gear = data?.let { Protocol.gear(it.gear, it.gearboxMode) } ?: "—"
    val speed = data?.let { "%.0f".format(it.speedMps * 3.6) } ?: "—"
    val rpm = data?.let { "%.0f".format(it.rpm) } ?: "—"
    val fuel = data?.fuelFraction?.let { "%.0f".format(it * 100) } ?: "—"
    when (state.profileId) {
        "f1-24", "f1-25" -> DesignCard {
            InstrumentHeading(state, if (state.profileId == "f1-24") "ГОНКА · F1 24" else "LIVE DATA · F1 25")
            if (state.profileId == "f1-25") {
                Box(Modifier.fillMaxWidth().background(design.panelAlt, RoundedCornerShape(18.dp)).padding(15.dp), contentAlignment = Alignment.Center) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Text(gear, fontSize = 88.sp, lineHeight = 92.sp, color = design.accent, fontWeight = FontWeight.Black)
                        Text("$speed КМ/Ч", fontSize = 27.sp, fontWeight = FontWeight.Bold)
                    }
                }
                DesignValue("RPM", rpm)
            } else {
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                    Column { Text(gear, fontSize = 85.sp, lineHeight = 88.sp, color = design.accent, fontWeight = FontWeight.Black); DesignLabel("ПЕРЕДАЧА") }
                    Column(horizontalAlignment = Alignment.End) { Text(speed, fontSize = 48.sp, fontWeight = FontWeight.Black); DesignLabel("КМ/Ч") }
                    Column(horizontalAlignment = Alignment.End) { Text(rpm, fontSize = 20.sp, fontWeight = FontWeight.Bold); DesignLabel("RPM") }
                }
            }
            RpmStrip(data)
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                Text("Топливо $fuel%", color = design.muted, fontSize = 12.sp)
                val race = data?.f1?.race
                val player = race?.drivers?.firstOrNull { it.player }
                if (race?.fresh == true && player != null) Text("ПОЗ ${player.position} · КР ${player.lap}", color = design.accent, fontSize = 12.sp)
            }
            HorizontalDivider(color = design.line)
            F1CircuitMap(state.telemetry?.f1?.race, state.stale, compact = true)
            TelemetryHint(state)
        }
        "beamng-default" -> DesignCard {
            InstrumentHeading(state, "VEHICLE CONTROL · BEAMNG")
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                Column { Text(speed, fontSize = 70.sp, lineHeight = 75.sp, fontWeight = FontWeight.Black); DesignLabel("КМ/Ч") }
                Column(horizontalAlignment = Alignment.End) { Text(gear, fontSize = 70.sp, lineHeight = 75.sp, color = design.accent, fontWeight = FontWeight.Black); DesignLabel("ПЕРЕДАЧА") }
            }
            RpmStrip(data)
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) { DesignValue("ОБОРОТЫ", rpm, "RPM"); DesignValue("ТОПЛИВО", fuel, "%") }
            HorizontalDivider(color = design.line)
            Text("КОРОБКА: " + when (data?.gearboxMode) { "arcade" -> "АРКАДА · D / N / R"; "realistic" -> "РЕАЛИЗМ${data?.maxGear?.let { " · $it ПЕРЕДАЧ" } ?: ""}"; else -> "—" }, fontSize = 12.sp, color = design.muted)
            Text("СВЕТ: " + when (data?.headlights) { 0 -> "ВЫКЛ"; 1 -> "БЛИЖНИЙ"; 2 -> "ДАЛЬНИЙ"; else -> "—" }, fontSize = 12.sp, color = design.muted)
            TelemetryHint(state)
        }
        "acc" -> {
            DesignCard {
                InstrumentHeading(state, "GT COCKPIT · ACC")
                DesignValue("ДВИГАТЕЛЬ", rpm, "RPM")
                RpmStrip(data, 10)
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    DesignValue("ПЕРЕДАЧА", gear)
                    DesignValue("СКОРОСТЬ", speed, "КМ/Ч")
                }
                TelemetryHint(state)
            }
            Spacer(Modifier.height(12.dp))
            DesignCard { DesignLabel("СОСТОЯНИЕ ШИН И ТОРМОЗОВ"); AccWheels(data?.acc) }
        }
        "ams2" -> DesignCard {
            InstrumentHeading(state, "COCKPIT CONTROL · AMS2")
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                DesignValue("СКОРОСТЬ", speed, "КМ/Ч")
                DesignValue("ПЕРЕДАЧА", gear)
            }
            DesignValue("ОБОРОТЫ", rpm, "RPM")
            RpmStrip(data, 10)
            TelemetryHint(state)
        }
        "ets2" -> DesignCard {
            InstrumentHeading(state, "LONG HAUL · ETS2")
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                DesignValue("СКОРОСТЬ", speed, "КМ/Ч")
                DesignValue("ПЕРЕДАЧА", gear)
            }
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                DesignValue("ТОПЛИВО", fuel, "%")
                Text("КРУИЗ: " + when (data?.actionStates?.get("etsCruise")) { true -> "ВКЛ"; false -> "ВЫКЛ"; null -> "—" }, color = design.accent, fontSize = 13.sp)
            }
            HorizontalDivider(color = design.line)
            Ets2Route(data?.ets2Navigation)
            TelemetryHint(state)
        }
        "snowrunner" -> DesignCard {
            InstrumentHeading(state, "FIELD OPERATIONS · SNOWRUNNER")
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                DesignValue("КОРОБКА", gear)
                DesignValue("СКОРОСТЬ", speed, "КМ/Ч")
            }
            DesignValue("ТОПЛИВО", fuel, "%")
            HorizontalDivider(color = design.line)
            Text("Полный привод · блокировка · лебёдка — в панели действий ниже", fontSize = 12.sp, color = design.muted)
            TelemetryHint(state)
        }
        else -> DesignCard {
            InstrumentHeading(state, state.profileName.uppercase())
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) { DesignValue("СКОРОСТЬ", speed, "КМ/Ч"); DesignValue("ПЕРЕДАЧА", gear) }
            DesignValue("ОБОРОТЫ", rpm, "RPM")
            TelemetryHint(state)
        }
    }
}
