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
    "f1-24", "f1-25" -> ProfileDesign(Color(0xFF17191D), Color(0xFF202226), Color(0xFF2B2526), Color(0xFFE63537), Color(0xFFB7AFB0), Color(0xFF484044), 5, "RACE CONTROL")
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
internal fun DesignCard(spacingDp: Int = 12, content: @Composable ColumnScope.() -> Unit) {
    val design = LocalProfileDesign.current
    Card(
        colors = CardDefaults.cardColors(containerColor = design.panel),
        shape = RoundedCornerShape(design.radius.dp),
        modifier = Modifier.fillMaxWidth().border(1.dp, design.line, RoundedCornerShape(design.radius.dp))
    ) { Column(Modifier.fillMaxWidth().padding(17.dp), verticalArrangement = Arrangement.spacedBy(spacingDp.dp), content = content) }
}
