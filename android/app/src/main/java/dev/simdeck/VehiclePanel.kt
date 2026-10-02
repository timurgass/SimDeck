package dev.simdeck

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.*
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/** Class silhouettes; wheel geometry and equipment states always come from the game. */
@Composable internal fun VehiclePanel(state: DeckState) {
    val d=LocalProfileDesign.current
    val v=state.telemetry?.vehicle.takeUnless { state.stale }
    DesignCard {
        Text("ТЕХНИКА · АВТОМАТИЧЕСКИЙ ВЫБОР",fontSize=11.sp,color=d.accent,fontWeight=FontWeight.Bold)
        if(v == null || !v.controlled) {
            Text(if(v?.controlled == false) "Вы не в технике" else "Ждём сведения о технике",fontSize=20.sp)
            Text(if(state.stale) "Нет свежих данных игры" else "FS25 / BeamNG: нужен обновлённый мод. ETS2: данные из телеметрического плагина.",fontSize=12.sp,color=d.muted)
        } else {
            Text(v.name.ifBlank { "Неизвестная модель" },fontSize=22.sp,fontWeight=FontWeight.Bold)
            Text("${vehicleLabels[v.kind]} · ${v.axleCount?.let { "$it оси" } ?: "Колёса не переданы"}",fontSize=12.sp,color=d.muted)
            val trailer=v.attachments.firstOrNull { it.kind == "trailer" }.takeIf { state.profileId == "ets2" }
            VehicleDrawing(v.kind,v.wheels,trailer?.wheels)
            Text("Схема класса техники · светлые колёса — ведущие",fontSize=11.sp,color=d.muted)
            for(a in v.attachments) {
                val lowered by animateFloatAsState(if(a.lowered == true) 1f else 0f,label="equipment-height")
                Text("↳ ${a.name.ifBlank { vehicleLabels[a.kind] ?: "Орудие" }}",fontWeight=FontWeight.Bold,fontSize=15.sp)
                if(trailer?.id != a.id) VehicleDrawing(a.kind,a.wheels,lowered=lowered,small=true)
                Text(listOfNotNull(a.lowered?.let { if(it) "Опущено" else "Поднято" },a.turnedOn?.let { if(it) "Работает" else "Выключено" },a.fold?.let { "Положение складывания: ${ (it*100).toInt() }%" }).joinToString(" · ").ifBlank { "Состояние не передано игрой" },fontSize=12.sp,color=d.accent)
            }
            if(v.wear.isNotEmpty()) {
                val names=mapOf("engine" to "Двигатель","transmission" to "Коробка","cabin" to "Кабина","chassis" to "Шасси","wheels" to "Колёса")
                Text("ИЗНОС / ПОВРЕЖДЕНИЯ",fontSize=11.sp,color=d.muted)
                v.wear.forEach { (key,value) -> Text("${names[key]}: ${"%.0f".format(value*100)}%",fontSize=13.sp,color=if(value>.2) Color(0xFFFFAB70) else d.accent) }
            }
        }
    }
}

@Composable private fun VehicleDrawing(kind: String, wheels: List<VehicleWheel>, trailer: List<VehicleWheel>? = null, lowered: Float = 0f, small: Boolean = false) {
    val d=LocalProfileDesign.current
    Canvas(Modifier.fillMaxWidth().height(if(small) 105.dp else 235.dp)) {
        val scale=size.height/235f; val cx=size.width/2; val top=20*scale; val bottom=size.height-20*scale
        fun rect(x:Float,y:Float,w:Float,h:Float,color:Color,outline:Boolean=false) {
            if(outline) drawRect(color,Offset(x,y),Size(w,h),style=Stroke(2*scale)) else drawRect(color,Offset(x,y),Size(w,h))
        }
        val bw=(if(kind in listOf("bus","van","truck","trailer")) 72f else 92f)*scale
        rect(cx-bw/2,top,bw,bottom-top,d.accent.copy(alpha=.13f))
        rect(cx-bw/2,top,bw,bottom-top,d.accent,true)
        when(kind) {
            "unknown" -> drawLine(d.muted,Offset(cx-bw/2,top),Offset(cx+bw/2,bottom),2*scale)
            "truck","tractor","loader","telehandler","forestry" -> {
                rect(cx-bw/2-9*scale,top+22*scale,bw+18*scale,55*scale,d.panelAlt)
                rect(cx-bw/2-9*scale,top+22*scale,bw+18*scale,55*scale,d.accent,true)
                if(kind in listOf("loader","telehandler","forestry")) drawLine(d.accent,Offset(cx,top+35*scale),Offset(cx-30*scale,top-12*scale),9*scale)
            }
            "combine" -> rect(cx-80*scale,top,160*scale,20*scale,d.accent)
            "sprayer" -> drawLine(d.accent,Offset(cx-110*scale,top+90*scale),Offset(cx+110*scale,top+90*scale),5*scale)
            "tracked" -> { rect(cx-bw/2-20*scale,top+25*scale,16*scale,130*scale,d.muted); rect(cx+bw/2+4*scale,top+25*scale,16*scale,130*scale,d.muted) }
            "implement" -> rect(cx-80*scale,top+80*scale+lowered*25*scale,160*scale,35*scale,d.accent)
            else -> rect(cx-bw/2+8*scale,top+35*scale,bw-16*scale,40*scale,d.panelAlt)
        }
        fun wheelSet(list:List<VehicleWheel>,from:Float,to:Float) {
            if(list.isEmpty()) return
            val min=list.minOf { it.z }; val max=list.maxOf { it.z }; val span=(max-min).coerceAtLeast(1.0)
            val half=list.maxOf { kotlin.math.abs(it.x) }.coerceAtLeast(.5)
            for(w in list) { val x=cx+(w.x/half*(bw/2+9*scale)).toFloat(); val y=from+((w.z-min)/span*(to-from)).toFloat(); rect(x-7*scale,y-12*scale,14*scale,24*scale,d.background); rect(x-7*scale,y-12*scale,14*scale,24*scale,if(w.powered==true) d.accent else d.muted,true) }
        }
        wheelSet(wheels,top+30*scale,bottom-30*scale)
        if(trailer != null) {
            // Transparent body overlaps the tractor's rear wheels, preserving their visibility.
            rect(cx-52*scale,top+82*scale,104*scale,bottom-top-72*scale,d.accent.copy(alpha=.07f))
            rect(cx-52*scale,top+82*scale,104*scale,bottom-top-72*scale,d.accent,true)
            wheelSet(trailer,top+155*scale,bottom)
        }
    }
}
