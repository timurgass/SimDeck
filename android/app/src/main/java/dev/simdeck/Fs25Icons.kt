package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.withTransform
import androidx.compose.ui.graphics.vector.PathParser
import androidx.compose.ui.unit.dp

// Keep the matching action map and line paths in Browser/fs25-icons.js.
private val symbols = mapOf(
    "back" to "M20 8 L6 24 L20 40 M6 24 H42",
    "implement" to "M6 33 H42 M11 32 L16 19 H32 L37 32 M18 19 V13 H30 V19 M24 13 V7 M17 38 H31",
    "lower" to "M8 12 H40 M15 12 V29 H33 V12 M24 20 V40 M17 33 L24 40 L31 33",
    "power" to "M24 5 V25 M15 10 C4 20 12 40 24 41 C36 40 44 20 33 10",
    "fold" to "M6 35 L19 22 L29 22 L42 35 M6 13 L19 26 M42 13 L29 26 M20 17 H28 M20 31 H28",
    "width" to "M7 22 H41 M7 22 L14 15 M7 22 L14 29 M41 22 L34 15 M41 22 L34 29 M19 12 V32 H29 V12",
    "mode" to "M8 14 H40 M8 24 H40 M8 34 H40 M17 10 V18 M30 20 V28 M23 30 V38",
    "seed" to "M9 12 H39 L34 25 H14 Z M17 25 V31 M24 25 V34 M31 25 V31 M15 39 C12 35 18 33 19 39 M29 39 C26 35 32 33 33 39",
    "crop" to "M24 41 V18 M24 27 C17 27 13 22 13 16 C19 16 24 19 24 27 M24 23 C31 23 36 17 36 11 C28 11 24 15 24 23 M17 41 H31",
    "spray" to "M9 13 H28 V32 H9 Z M14 13 V8 H23 V13 M28 19 H35 L40 15 M35 23 L42 23 M35 27 L40 31 M15 38 H31",
    "pipe" to "M6 35 V23 H21 V17 H37 V10 H43 M17 35 V23 M31 17 V29 M27 35 H35",
    "unload" to "M8 10 H35 V28 H8 Z M35 19 H43 M24 25 V40 M17 33 L24 40 L31 33",
    "cover" to "M7 24 H41 L37 36 H11 Z M7 20 C14 10 34 10 41 20 M13 12 L17 7 M35 12 L31 7",
    "chopper" to "M24 7 V41 M7 24 H41 M12 12 L36 36 M36 12 L12 36 M24 17 C31 17 31 31 24 31 C17 31 17 17 24 17 Z",
    "direction" to "M6 16 H37 M30 9 L37 16 L30 23 M42 32 H11 M18 25 L11 32 L18 39",
    "gear" to "M12 9 V37 M24 9 V37 M36 9 V37 M12 23 H36 M9 9 H15 M21 9 H27 M33 9 H39 M9 37 H15 M21 37 H27 M33 37 H39",
    "helper" to "M24 8 C28 8 31 11 31 15 C31 19 28 22 24 22 C20 22 17 19 17 15 C17 11 20 8 24 8 Z M11 40 C11 31 16 27 24 27 C32 27 37 31 37 40 M5 23 L12 23 M36 23 L43 23",
    "clock" to "M24 6 C14 6 6 14 6 24 C6 34 14 42 24 42 C34 42 42 34 42 24 C42 14 34 6 24 6 Z M24 13 V24 L32 29",
    "radio" to "M7 19 H41 V37 H7 Z M12 19 L33 10 M15 27 H26 M15 32 H26 M34 25 C30 25 30 33 34 33 C38 33 38 25 34 25 Z",
    "store" to "M7 16 H41 L38 38 H10 Z M11 16 L15 8 H33 L37 16 M18 24 V38 M30 24 V38 M18 24 H30"
)

private val kinds = mapOf(
    "fs25Lower" to "lower", "fs25LowerAll" to "lower", "fs25TurnOn" to "power", "fs25TurnOnAll" to "power",
    "fs25Fold" to "fold", "fs25WorkWidth" to "width", "fs25WorkMode" to "mode", "fs25Attach" to "hitch",
    "fs25NextImplement" to "implement", "fs25PrevImplement" to "implement", "fs25Extra2" to "implement",
    "fs25Extra3" to "implement", "fs25Extra4" to "implement", "fs25Seeds" to "crop", "fs25SeedsBack" to "crop",
    "fs25DoubleSpray" to "spray", "fs25Pipe" to "pipe", "fs25Unload" to "unload", "fs25UnloadHere" to "unload", "fs25TipSide" to "unload",
    "fs25Cover" to "cover", "fs25Chopper" to "chopper", "fs25Motor" to "engine", "fs25Direction" to "direction",
    "fs25Cruise" to "cruise", "fs25GearUp" to "gear", "fs25GearDown" to "gear", "fs25GroupUp" to "gear",
    "fs25GroupDown" to "gear", "fs25Lights" to "lights", "fs25HighBeam" to "high", "fs25WorkLightFront" to "lights",
    "fs25WorkLightBack" to "lights", "fs25Beacon" to "beacon", "fs25TurnLeft" to "left", "fs25TurnRight" to "right",
    "fs25Hazard" to "hazard", "fs25Horn" to "horn", "fs25Camera" to "camera", "fs25Axle" to "axle",
    "fs25Helper" to "helper", "fs25NextVehicle" to "direction", "fs25PrevVehicle" to "direction",
    "fs25Enter" to "helper", "fs25Seat" to "helper", "fs25Menu" to "mode", "fs25Back" to "back", "fs25Store" to "store",
    "fs25Map" to "map", "fs25Construction" to "store", "fs25Help" to "mode", "fs25Pause" to "pause",
    "fs25TimeUp" to "clock", "fs25TimeDown" to "clock", "fs25Radio" to "radio"
)

private val ets2Fallback = mapOf(
    "hitch" to "etsAttachTrailer", "engine" to "etsEngine", "cruise" to "etsCruise",
    "lights" to "etsLights", "high" to "etsHighBeam", "beacon" to "etsBeacon",
    "left" to "etsLeftSignal", "right" to "etsRightSignal", "hazard" to "etsHazards",
    "horn" to "etsHorn", "camera" to "etsCamera", "axle" to "etsLiftAxle",
    "map" to "etsMap", "pause" to "etsPause"
)

@Composable
internal fun Fs25Icon(action: String, color: Color): Boolean {
    val kind = kinds[action] ?: return false
    val data = symbols[kind]
    if (data == null) return Ets2Icon(ets2Fallback[kind] ?: return false, color)
    val path = remember(data) { PathParser().parsePathString(data).toPath() }
    Canvas(Modifier.size(31.dp)) {
        withTransform({ scale(size.width / 48f, size.height / 48f, Offset.Zero) }) {
            drawPath(path, color, style = Stroke(width = 2.7f, cap = StrokeCap.Round, join = StrokeJoin.Round))
        }
    }
    return true
}
