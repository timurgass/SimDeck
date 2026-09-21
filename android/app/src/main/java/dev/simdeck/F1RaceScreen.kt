package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.nativeCanvas
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.util.Locale

private object CircuitAssets { var all: Map<Int,Circuit>? = null }
@Composable private fun circuit(id: Int?): Circuit? {
    val context=LocalContext.current
    val all=remember { CircuitAssets.all ?: Circuit.parseAll(context.assets.open("f1-circuits.json").bufferedReader().use { it.readText() }).also { CircuitAssets.all=it } }
    return all[id]
}
private fun teamColor(team: Int): Color = Color(when(team) {
    0 -> 0xFF27F4D2; 1 -> 0xFFE95A62; 2 -> 0xFF7298FF; 3 -> 0xFF64C4FF; 4 -> 0xFF2AB6A0
    5 -> 0xFFFF87BC; 6 -> 0xFF9DA5FF; 7 -> 0xFFDBDEE2; 8 -> 0xFFFFA23D; 9 -> 0xFF75EE58
    else -> longArrayOf(0xFF74CCE0,0xFFFFC869,0xFFB7A3ED,0xFFB6D881)[Math.floorMod(team,4)]
})

@Composable internal fun F1CircuitMap(race: RaceData?, stale: Boolean, compact: Boolean = false) {
    val track=circuit(race?.trackId)
    if (track == null) {
        Text(if(race == null || race.trackId < 0) "Ожидание названия трассы от игры" else "Для этой конфигурации схема пока не добавлена", fontSize = 15.sp)
        return
    }
    val live=!stale && race?.fresh == true
    Text(track.name, fontSize = if(compact) 16.sp else 23.sp, fontWeight = FontWeight.Bold)
    val markers=if(live) race!!.drivers.filter { it.onTrack } else emptyList()
    Canvas(Modifier.fillMaxWidth().height(if(compact) 85.dp else 195.dp)) {
        val points=track.points
        val minX=points.minOf { it.x }; val maxX=points.maxOf { it.x }; val minY=points.minOf { it.y }; val maxY=points.maxOf { it.y }
        val margin=if(compact) 12.dp.toPx() else 28.dp.toPx()
        val rotate=(maxY-minY)>(maxX-minX) && size.width>size.height
        val extentX=if(rotate) maxY-minY else maxX-minX; val extentY=if(rotate) maxX-minX else maxY-minY
        val scale=minOf((size.width-margin*2)/extentX.coerceAtLeast(1f),(size.height-margin*2)/extentY.coerceAtLeast(1f))
        fun project(p: MapPoint): Offset {
            val x=p.x-(minX+maxX)/2; val y=p.y-(minY+maxY)/2
            return Offset(size.width/2+(if(rotate) y else x)*scale,size.height/2+(if(rotate) -x else y)*scale)
        }
        val path=Path(); points.forEachIndexed { i,p -> val o=project(p); if(i==0)path.moveTo(o.x,o.y) else path.lineTo(o.x,o.y) }
        drawPath(path,Color(0xFF304653),style=Stroke(if(compact) 6.dp.toPx() else 10.dp.toPx()))
        drawPath(path,Color(0xFFA5B9C5),style=Stroke(if(compact) 2.dp.toPx() else 3.dp.toPx()))
        val start=project(points.first()); drawRect(Color.White,start-Offset(3.dp.toPx(),3.dp.toPx()),androidx.compose.ui.geometry.Size(6.dp.toPx(),6.dp.toPx()))
        val occupied=mutableListOf<android.graphics.RectF>()
        // Player last: its white ring remains visible even in a tight pack.
        markers.sortedBy { it.player }.forEach { driver ->
            val p=track.at(driver.distance,race!!.trackLength) ?: return@forEach; val o=project(p)
            val radius=if(compact) 3.dp.toPx() else 5.dp.toPx()
            drawCircle(if(driver.player) Color.White else Color(0xFF0B1115),radius+2.dp.toPx(),o)
            drawCircle(teamColor(driver.team),radius,o)
            if(!compact) {
                val label="${driver.position.takeIf { it>0 } ?: "—"} ${driver.shortName}"
                val paint=android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG).apply { textSize=11.sp.toPx(); color=android.graphics.Color.WHITE; typeface=android.graphics.Typeface.DEFAULT_BOLD }
                val w=paint.measureText(label)+8.dp.toPx(); val h=17.dp.toPx()
                val rect=(0..7).map { slot ->
                    val x=(o.x+9.dp.toPx()).coerceIn(0f,(size.width-w).coerceAtLeast(0f))
                    val y=(o.y+(slot-3)*h).coerceIn(0f,(size.height-h).coerceAtLeast(0f))
                    android.graphics.RectF(x,y,x+w,y+h)
                }.firstOrNull { r -> occupied.none { android.graphics.RectF.intersects(it,r) } }
                if(rect!=null) {
                    occupied.add(rect); val bg=android.graphics.Paint().apply { color=0xEE102029.toInt() }
                    drawContext.canvas.nativeCanvas.drawRoundRect(rect,4f,4f,bg)
                    drawContext.canvas.nativeCanvas.drawText(label,rect.left+4.dp.toPx(),rect.bottom-4.dp.toPx(),paint)
                }
            }
        }
    }
    Text(if(!live) "Схема готова · ждём данные пилотов" else if(markers.isEmpty()) "Пилоты в боксах или нет активных машин" else if(compact) "${markers.size} на трассе · вы с белым ободком" else "Вы — белый ободок · цвет команды", fontSize = 12.sp, color = Color(0xFFAEBFC9))
    if(!compact) Text("Положение по дистанции круга, приблизительно. Пит-лейн не показан отдельно.",fontSize=12.sp,color=Color(0xFFAEBFC9))
}

@Composable internal fun F1RaceScreen(state: DeckState) {
    val race=state.telemetry?.f1?.race
    val live=!state.stale && race?.fresh == true
    val rows=race?.drivers.orEmpty()
    Column(verticalArrangement=Arrangement.spacedBy(12.dp)) {
        Row(horizontalArrangement=Arrangement.spacedBy(12.dp)) {
            Surface(Modifier.weight(1.15f),color=Color(0xFF18252D),shape=MaterialTheme.shapes.medium) {
                Column(Modifier.padding(12.dp),verticalArrangement=Arrangement.spacedBy(8.dp)) { F1CircuitMap(race,state.stale) }
            }
            Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(6.dp)) {
                Text("ПОРЯДОК ПИЛОТОВ",fontSize=17.sp,fontWeight=FontWeight.Bold)
                Text(if(live) "Позиции из игры · ${rows.size} машин" else "Ожидание свежих позиций",fontSize=12.sp,color=Color(0xFFAEBFC9))
                Column(Modifier.heightIn(max=250.dp).verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(4.dp)) {
                    if(rows.isEmpty()) Text("Список появится после пакета участников. В гонке на время полноценной стартовой решётки нет.",fontSize=14.sp)
                    rows.forEach { d ->
                        val status=when { !live -> "—"; d.result==7 || d.result==4 -> "DNF"; d.result==5 -> "DSQ"; d.result==3 -> "ФИНИШ"; d.pit>0 -> "PIT"; else -> "Кр. ${d.lap}" }
                        Row(Modifier.fillMaxWidth().background(if(d.player) Color(0xFF304453) else Color(0xFF152028)).padding(7.dp),horizontalArrangement=Arrangement.spacedBy(6.dp)) {
                            Text(if(live) d.position.takeIf { it>0 }?.toString() ?: "—" else "—",fontSize=17.sp,modifier=Modifier.width(23.dp))
                            Column(Modifier.weight(1f)) {
                                Text((if(d.player) "ВЫ · " else "")+d.name,fontSize=14.sp,color=teamColor(d.team),maxLines=1)
                                Text("${d.shortName} · #${d.number} · $status",fontSize=11.sp,color=Color(0xFFAEBFC9))
                            }
                        }
                    }
                }
            }
        }
        val playerIndex=rows.indexOfFirst { it.player }
        if(live && playerIndex>=0) {
            val me=rows[playerIndex]; val ahead=rows.getOrNull(playerIndex-1); val behind=rows.getOrNull(playerIndex+1)
            Text("Впереди по позиции: ${ahead?.name ?: "вы лидер"}   ·   Позади: ${behind?.name ?: "—"}",fontSize=16.sp)
            if(race!!.sessionType in 15..17 && me.position>1 && me.gapAheadMs>0)
                Text("До машины впереди: ${String.format(Locale.US,"%.3f",me.gapAheadMs/1000.0)} с",fontSize=15.sp)
        }
        Text("Схемы: f1-circuits · © Tomislav Bacinger · MIT",fontSize=11.sp,color=Color(0xFF8D9FA8))
    }
}
