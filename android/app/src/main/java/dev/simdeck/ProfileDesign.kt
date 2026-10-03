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
    "f1-24", "f1-25" -> ProfileDesign(Color(0xFF0B1519), Color(0xFF101E23), Color(0xFF1B2C33), Color(0xFFE63537), Color(0xFFABBDC7), Color(0xFF324B56), 6, "RACE CONTROL")
    "beamng-default" -> ProfileDesign(Color(0xFF0B1519), Color(0xFF101E23), Color(0xFF1B2C33), Color(0xFFF5842D), Color(0xFFABBDC7), Color(0xFF324B56), 6, "VEHICLE CONTROL UNIT")
    "acc" -> ProfileDesign(Color(0xFF0B1519), Color(0xFF101E23), Color(0xFF1B2C33), Color(0xFFFFAE3B), Color(0xFFABBDC7), Color(0xFF324B56), 6, "GT COCKPIT")
    "ams2" -> ProfileDesign(Color(0xFF0B1519), Color(0xFF101E23), Color(0xFF1B2C33), Color(0xFFB8E44B), Color(0xFFABBDC7), Color(0xFF324B56), 6, "COCKPIT CONTROL")
    "ets2" -> ProfileDesign(Color(0xFF0B1519), Color(0xFF101E23), Color(0xFF1B2C33), Color(0xFFDDB66A), Color(0xFFABBDC7), Color(0xFF324B56), 6, "LONG HAUL DASHBOARD")
    "snowrunner" -> ProfileDesign(Color(0xFF0B1519), Color(0xFF101E23), Color(0xFF1B2C33), Color(0xFFB8E44B), Color(0xFFABBDC7), Color(0xFF324B56), 6, "FIELD OPERATIONS")
    "fs25" -> ProfileDesign(Color(0xFF0B1519), Color(0xFF101E23), Color(0xFF1B2C33), Color(0xFF9CDA65), Color(0xFFABBDC7), Color(0xFF324B56), 6, "FARM OPERATIONS")
    else -> ProfileDesign(Color(0xFF0B1115), Color(0xFF182026), Color(0xFF23343A), Color(0xFFF2B84B), Color(0xFF8D9FA8), Color(0xFF304049), 16, "YOUR RIG. ONE TOUCH.")
}

internal val LocalProfileDesign = compositionLocalOf { profileDesign("") }

@Composable
internal fun DesignCard(spacingDp: Int = 12, paddingDp: Int = 17, content: @Composable ColumnScope.() -> Unit) {
    val design = LocalProfileDesign.current
    Card(
        colors = CardDefaults.cardColors(containerColor = design.panel),
        shape = RoundedCornerShape(design.radius.dp),
        modifier = Modifier.fillMaxWidth().border(1.dp, design.line, RoundedCornerShape(design.radius.dp))
    ) { Column(Modifier.fillMaxWidth().padding(paddingDp.dp), verticalArrangement = Arrangement.spacedBy(spacingDp.dp), content = content) }
}
