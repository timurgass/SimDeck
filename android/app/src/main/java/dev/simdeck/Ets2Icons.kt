package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.withTransform
import androidx.compose.ui.graphics.vector.PathParser
import androidx.compose.ui.unit.dp

// Original dashboard-style outlines. Keep the matching paths in Browser/ets2-icons.js.
private val ets2Paths = mapOf(
    "etsEngine" to "M9 16 H16 V12 H23 V16 H30 L34 20 H39 V31 H34 L30 35 H16 L12 31 H9 V26 H5 V20 H9 Z M17 9 H27 M19 39 H30",
    "etsParkingBrake" to "M24 7 C14 7 7 14 7 24 C7 34 14 41 24 41 C34 41 41 34 41 24 C41 14 34 7 24 7 Z M19 33 V15 H25 C32 15 32 25 25 25 H19 M5 14 C2 20 2 28 5 34 M43 14 C46 20 46 28 43 34",
    "etsEngineBrake" to "M8 18 H27 L35 24 L27 30 H8 Z M15 18 V12 M22 18 V12 M28 32 L34 38 M33 32 L39 38",
    "etsRetarderUp" to "M9 34 C9 20 16 12 29 12 M24 8 L30 12 L24 17 M15 36 H35 M29 28 L36 21 M30 21 H36 V27",
    "etsRetarderDown" to "M9 34 C9 20 16 12 29 12 M24 8 L30 12 L24 17 M15 36 H35 M30 21 L37 28 M37 22 V28 H31",
    "etsDifferential" to "M5 14 V34 M43 14 V34 M5 24 H15 M33 24 H43 M15 19 H33 V30 H15 Z M21 19 V16 C21 11 27 11 27 16 V19 M24 23 V27",
    "etsAttachTrailer" to "M5 20 H20 V30 H5 Z M20 25 H27 M27 17 H43 V30 H27 Z M10 34 H15 M32 34 H38",
    "etsLiftAxle" to "M6 20 H35 V30 H6 Z M35 24 H42 V30 H35 M13 35 H18 M28 35 H33 M24 16 V6 M18 12 L24 6 L30 12",
    "etsHorn" to "M6 20 H14 L27 14 V34 L14 28 H6 Z M31 19 C36 22 36 26 31 29 M35 14 C44 20 44 28 35 34",
    "etsAirHorn" to "M6 20 H14 L27 14 V34 L14 28 H6 Z M31 19 C36 22 36 26 31 29 M35 14 C44 20 44 28 35 34 M8 35 H26",
    "etsLights" to "M25 14 C18 17 18 31 25 34 H31 V14 Z M17 17 L7 21 M16 24 L5 28 M17 31 L7 35",
    "etsHighBeam" to "M25 14 C18 17 18 31 25 34 H31 V14 Z M16 17 H5 M16 24 H5 M16 31 H5",
    "etsBeacon" to "M13 31 H35 M17 31 V22 C17 13 31 13 31 22 V31 M24 12 V6 M11 15 L6 11 M37 15 L42 11 M37 24 H43 M5 24 H11 M16 36 H32",
    "etsHazards" to "M24 6 L44 39 H4 Z M24 18 V28 M24 34 V35",
    "etsLeftSignal" to "M23 10 L8 24 L23 38 V30 H40 V18 H23 Z",
    "etsRightSignal" to "M25 10 L40 24 L25 38 V30 H8 V18 H25 Z",
    "etsWipers" to "M6 25 C8 8 40 8 42 25 L37 36 H11 Z M12 31 L35 19 M29 25 L36 31",
    "etsCruise" to "M9 34 C5 28 6 18 12 12 C19 5 30 5 37 12 C43 18 44 28 39 34 M13 34 H35 M24 24 L33 17 M13 20 L10 18 M24 14 V10 M35 20 L38 18",
    "etsCruiseUp" to "M8 34 C4 25 9 10 24 8 C39 10 44 25 40 34 M15 34 H32 M24 26 L33 17 M18 17 H8 M13 12 V22",
    "etsCruiseDown" to "M8 34 C4 25 9 10 24 8 C39 10 44 25 40 34 M15 34 H32 M24 26 L33 17 M8 17 H18",
    "etsMap" to "M5 12 L17 8 L30 12 L43 8 V35 L30 39 L17 35 L5 39 Z M17 8 V35 M30 12 V39 M22 25 C22 20 28 20 28 25 C28 29 25 31 25 31 C25 31 22 29 22 25 Z",
    "etsRouteAdvisor" to "M7 9 H41 V39 H7 Z M12 14 H36 V22 H12 Z M12 27 H24 M12 33 H31 M34 28 L38 32 L34 36",
    "etsMirrors" to "M6 12 H17 V35 H6 Z M31 12 H42 V35 H31 Z M19 33 C19 25 29 25 29 33 M12 17 V28 M36 17 V28",
    "etsDashboard" to "M6 12 H42 V36 H6 Z M12 29 C12 16 23 16 23 29 M25 29 C25 16 36 16 36 29 M17 29 L20 22 M30 29 L34 20",
    "etsCamera" to "M5 14 H43 V34 H5 Z M24 18 C18 18 15 24 18 29 C22 35 31 30 31 24 C31 20 28 18 24 18 Z M10 10 H18 M32 10 H40",
    "etsCabCamera" to "M5 14 H43 V34 H5 Z M10 27 C17 17 31 17 38 27 M17 27 H31 M24 27 V33",
    "etsOutsideCamera" to "M5 14 H43 V34 H5 Z M9 27 L15 20 H33 L39 27 Z M15 28 H33 M18 34 H30",
    "etsQuickSave" to "M9 7 H35 L40 12 V41 H9 Z M15 7 V19 H33 V7 M15 41 V27 H34 V41",
    "etsPause" to "M15 9 H20 V39 H15 Z M28 9 H33 V39 H28 Z"
)

@Composable
internal fun Ets2Icon(action: String, color: Color): Boolean {
    val data = ets2Paths[action] ?: return false
    val path = androidx.compose.runtime.remember(data) { PathParser().parsePathString(data).toPath() }
    Canvas(Modifier.size(31.dp)) {
        withTransform({ scale(size.width / 48f, size.height / 48f, Offset.Zero) }) {
            drawPath(path, color, style = Stroke(width = 2.7f, cap = StrokeCap.Round, join = StrokeJoin.Round))
        }
    }
    return true
}
