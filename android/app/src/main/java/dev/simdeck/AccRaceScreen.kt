package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.nativeCanvas
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.util.Locale
import kotlin.math.hypot

private val accCups=listOf("Pro","Pro-Am","Am","Silver","National")
private fun accColor(cup:Int)=Color(listOf(0xFFA9C6EC,0xFF62D0A3,0xFFF3B16D,0xFFCCD4E3,0xFFBF9EE6).getOrElse(cup){0xFF899AA8})
internal fun accLap(ms:Int?)=ms?.takeIf{it>0}?.let{String.format(Locale.US,"%d:%06.3f",it/60000,(it%60000)/1000.0)}?:"—"
@Composable internal fun AccRaceScreen(state:DeckState) {
    val race=state.telemetry?.acc?.race
    var cup by rememberSaveable {mutableStateOf(-1)}
    var selected by rememberSaveable {mutableStateOf<Int?>(null)}
    val d=LocalProfileDesign.current
    val live=!state.stale&&race?.fresh==true&&!race.replay
    val drivers=race?.drivers?.filter{cup<0||it.cup==cup}.orEmpty()
    DesignCard {
        Text("КАРТА И ПИЛОТЫ",fontWeight=FontWeight.Bold,fontSize=20.sp)
        Text((race?.track?.takeIf{it.isNotBlank()}?:"Ожидание трассы")+" · "+if(live) "LIVE" else if(race?.replay==true) "Повтор" else "Последние / нет данных",color=d.muted,fontSize=13.sp)
        Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(8.dp)) {
            (listOf(-1 to "Все кубки")+accCups.mapIndexed{i,name->i to name}).forEach{(id,name)->FilterChip(selected=cup==id,onClick={cup=id},label={Text(name)})}
        }
        BoxWithConstraints {
            val wide=maxWidth>=650.dp
            val map:@Composable ()->Unit={AccCircuit(race,drivers,live);val coverage=if(race!=null)race.points.size*100/race.bins else 0
                Text(if(coverage>=95) "Контур собран по координатам игры" else "Контур изучается: $coverage%. Машинам нужно проехать круг; пропуски не соединяются.",color=d.muted,fontSize=12.sp)}
            val list:@Composable ()->Unit={
                if(drivers.isEmpty())Text("Ожидание участников. Включите Broadcasting ACC и начните заезд с соперниками.",color=d.muted)
                drivers.forEach{car->
                    OutlinedButton(onClick={selected=if(selected==car.index)null else car.index},modifier=Modifier.fillMaxWidth().heightIn(min=70.dp),contentPadding=PaddingValues(10.dp)) {
                        Text(car.position.takeIf{it>0}?.toString()?:"—",modifier=Modifier.width(30.dp),fontWeight=FontWeight.Bold)
                        Column(Modifier.weight(1f)) {
                            Text(car.name+if(car.player) " · ВЫ" else "",color=if(car.player)Color.White else accColor(car.cup),fontSize=13.sp,fontWeight=FontWeight.Bold)
                            Text("#${car.number} · ${accCups.getOrElse(car.cup){"Кубок —"}} · "+if(car.location==2) "В боксах" else "Круг ${car.lap+1}",color=d.muted,fontSize=10.sp)
                        }
                        val gap=if(car.position==1) "Лидер" else if(race?.sessionType!=10) accLap(car.bestLapMs) else car.gapAheadMs?.let{String.format(Locale.US,"≈ %.1f с",it/1000.0)}?:"—"
                        Text(gap,fontSize=11.sp,modifier=Modifier.widthIn(min=55.dp))
                    }
                    if(selected==car.index)Text("${car.team.ifBlank{"Команда —"}} · Лучший ${accLap(car.bestLapMs)} · Последний ${accLap(car.lastLapMs)}",color=d.muted,fontSize=12.sp)
                }
            }
            if(wide)Row(horizontalArrangement=Arrangement.spacedBy(18.dp)) {Column(Modifier.weight(1.1f),verticalArrangement=Arrangement.spacedBy(8.dp)){map()};Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(7.dp)){list()}}
            else Column(verticalArrangement=Arrangement.spacedBy(12.dp)){map();list()}
        }
        Text("ВЫ — белый ободок. Цвет — категория кубка. Интервалы ≈ рассчитаны по времени прохождения одной точки; на старте и в боксах могут отсутствовать.",color=d.muted,fontSize=12.sp)
    }
}
@Composable private fun AccCircuit(race:AccRaceData?,drivers:List<AccDriverData>,live:Boolean) {
    Canvas(Modifier.fillMaxWidth().height(280.dp).background(Color(0xFF11191B))) {
        val points=race?.points.orEmpty()
        val markers=drivers.filter{it.location>0}
        val all=points.map{it.x to it.z}+markers.map{it.x to it.z}
        if(all.isEmpty())return@Canvas
        val minX=all.minOf{it.first};val maxX=all.maxOf{it.first};val minZ=all.minOf{it.second};val maxZ=all.maxOf{it.second}
        val margin=24.dp.toPx();val scale=minOf((size.width-2*margin)/(maxX-minX).coerceAtLeast(100.0),(size.height-2*margin)/(maxZ-minZ).coerceAtLeast(100.0))
        fun project(x:Double,z:Double)=Offset((size.width/2+(x-(minX+maxX)/2)*scale).toFloat(),(size.height/2-(z-(minZ+maxZ)/2)*scale).toFloat())
        fun linked(a:AccMapPoint,b:AccMapPoint,step:Int)=step<=4&&hypot(a.x-b.x,a.z-b.z)<maxOf(80.0,(race?.trackLength?:0)/40.0)
        val path=Path();points.forEachIndexed{i,p->val o=project(p.x,p.z);val a=points.getOrNull(i-1);if(a!=null&&linked(a,p,p.bin-a.bin))path.lineTo(o.x,o.y)else path.moveTo(o.x,o.y)}
        if(points.size>1&&linked(points.last(),points.first(),(race?.bins?:256)-points.last().bin+points.first().bin)){val o=project(points.first().x,points.first().z);path.lineTo(o.x,o.y)}
        drawPath(path,Color(0xFF34444C),style=Stroke(10.dp.toPx()));drawPath(path,Color(0xFFABB6BA),style=Stroke(3.dp.toPx()))
        val occupied=mutableListOf<android.graphics.RectF>()
        markers.sortedBy{it.player}.forEach{car->val o=project(car.x,car.z);val alpha=if(live&&car.fresh)1f else .45f
            drawCircle(if(car.player)Color.White.copy(alpha=alpha)else Color(0xFF101820),7.dp.toPx(),o);drawCircle(accColor(car.cup).copy(alpha=alpha),4.dp.toPx(),o)
            val label="${car.position.takeIf{it>0}?:"—"} ${car.shortName.ifBlank{car.name.substringAfterLast(' ').take(3).uppercase()}}"
            val paint=android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG).apply{color=android.graphics.Color.WHITE;textSize=11.sp.toPx();this.alpha=(alpha*255).toInt()}
            val w=paint.measureText(label)+8.dp.toPx();val h=16.dp.toPx()
            val rect=(0..5).map{slot->val x=(o.x+8.dp.toPx()).coerceIn(0f,(size.width-w).coerceAtLeast(0f));val y=(o.y+(slot-2)*h).coerceIn(0f,(size.height-h).coerceAtLeast(0f));android.graphics.RectF(x,y,x+w,y+h)}.firstOrNull{r->occupied.none{android.graphics.RectF.intersects(it,r)}}
            if(rect!=null){occupied.add(rect);drawContext.canvas.nativeCanvas.drawRect(rect,android.graphics.Paint().apply{color=0xDD10191B.toInt()});drawContext.canvas.nativeCanvas.drawText(label,rect.left+4.dp.toPx(),rect.bottom-3.dp.toPx(),paint)}
        }
    }
}
