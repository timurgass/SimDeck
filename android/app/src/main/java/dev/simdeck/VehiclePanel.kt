package dev.simdeck

import android.graphics.BitmapFactory
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.*
import androidx.compose.ui.graphics.drawscope.*
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.*

// Both clients consume the same PNGs. These are class illustrations, not exact 3D models.
internal data class VehicleSprite(val atlas:String,val x:Int,val y:Int,val w:Int,val h:Int)
private fun farmSprite(kind:String):VehicleSprite? = when(kind) {
    "tractor" -> VehicleSprite("farm",0,130,354,320)
    "combine" -> VehicleSprite("farm",350,160,416,290)
    "truck" -> VehicleSprite("farm",772,210,368,220)
    "loader" -> VehicleSprite("farm",1145,150,391,290)
    "telehandler" -> VehicleSprite("farm",20,565,350,320)
    "forestry" -> VehicleSprite("farm",390,555,378,340)
    "sprayer" -> VehicleSprite("farm",775,615,370,265)
    "implement" -> VehicleSprite("farm",1145,700,379,190)
    "tracked" -> VehicleSprite("equipment",0,0,724,724)
    "trailer" -> VehicleSprite("equipment",724,0,724,724)
    else -> null
}
private fun roadSprite(kind:String):VehicleSprite? {
    if(kind=="gt") return VehicleSprite("gt",0,0,1024,1536)
    return when(kind) {
        "car" -> VehicleSprite("road",120,9,233,391)
        "suv" -> VehicleSprite("road",523,6,209,412)
        "pickup" -> VehicleSprite("road",905,9,223,394)
        "van" -> VehicleSprite("road",124,418,225,409)
        "bus" -> VehicleSprite("road",534,418,186,410)
        "truck" -> VehicleSprite("road",917,418,199,410)
        "formula" -> VehicleSprite("road",510,840,235,404)
        "trailer" -> VehicleSprite("road",945,840,144,401)
        else -> null
    }
}
@Composable private fun vehicleAtlas(name:String):ImageBitmap {
    val context=LocalContext.current
    return remember(name) { context.assets.open("vehicles/$name.png").use { BitmapFactory.decodeStream(it).asImageBitmap() } }
}
private fun DrawScope.sprite(bitmap:ImageBitmap,s:VehicleSprite,x:Float,y:Float,w:Float,h:Float,alpha:Float=1f) {
    drawImage(bitmap,IntOffset(s.x,s.y),IntSize(s.w,s.h),IntOffset(x.toInt(),y.toInt()),IntSize(w.toInt().coerceAtLeast(1),h.toInt().coerceAtLeast(1)),alpha=alpha,filterQuality=FilterQuality.High)
}
@Composable internal fun VehiclePanel(state:DeckState) {
    val d=LocalProfileDesign.current;val v=state.telemetry?.vehicle.takeUnless { state.stale };val farm=state.profileId=="fs25"
    DesignCard {
        Text(if(farm) "ТЕХНИКА И ОРУДИЯ" else "СОСТОЯНИЕ МАШИНЫ",fontSize=18.sp,fontWeight=FontWeight.Black)
        if(v==null || !v.controlled) {
            Text(if(v?.controlled==false) "Вы не в технике" else "Ждём сведения о технике",fontSize=20.sp)
            Text(if(state.stale) "Нет свежих данных игры" else "Для определения машины нужны данные игрового мода / плагина.",fontSize=12.sp,color=d.muted)
        } else {
            Text(v.name.ifBlank { "Неизвестная модель" },fontSize=22.sp,color=d.accent,fontWeight=FontWeight.Bold)
            Text("АВТО · ${vehicleLabels[v.kind]} · ${v.axleCount?.let { "$it оси" } ?: "Геометрия колёс неизвестна"}",fontSize=12.sp,color=d.muted)
            val linked=v.attachments.firstOrNull { it.parentId==v.id }
            if(farm) key(v.id,linked?.id) { FarmDrawing(v.kind,linked) } else {
                val trailer=v.attachments.firstOrNull { it.kind=="trailer" }.takeIf { state.profileId=="ets2" }
                if(v.wear.isEmpty()) RoadDrawing(v.kind,v.wheels,trailer) else BoxWithConstraints {
                    if(maxWidth>=390.dp) Row(horizontalArrangement=Arrangement.spacedBy(12.dp)) {
                        Column(Modifier.weight(1f)) { RoadDrawing(v.kind,v.wheels,trailer,heightOverride=300.dp) }
                        Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(8.dp)) { WearRows(v.wear) }
                    } else Column { RoadDrawing(v.kind,v.wheels,trailer);WearRows(v.wear) }
                }
            }
            Text("Иллюстрация класса · модель и состояния из игры",fontSize=11.sp,color=d.muted)
            v.attachments.forEach { a -> key(v.id,a.id) {
                HorizontalDivider(color=d.line)
                Text("↳ ${a.name.ifBlank { vehicleLabels[a.kind] ?: "Орудие" }}",fontWeight=FontWeight.Bold,fontSize=16.sp)
                if(a!=linked && farm) FarmDrawing(a.kind,null,small=true)
                Text(listOfNotNull(a.lowered?.let { if(it) "ОПУЩЕНО" else "ПОДНЯТО" },a.turnedOn?.let { if(it) "РАБОТАЕТ" else "ВЫКЛЮЧЕНО" },a.fold?.let { "Складывание ${(it*100).toInt()}%" }).joinToString(" · ").ifBlank { "Состояние не передано игрой" },fontSize=13.sp,color=d.accent)
            } }
            if(!farm && v.wear.isEmpty()) Text("Повреждения узлов: нет данных",fontSize=13.sp,color=d.muted)
        }
    }
}
@Composable private fun WearRows(wear:Map<String,Double>) {
    val names=mapOf("engine" to "Двигатель","transmission" to "Коробка","cabin" to "Кабина","chassis" to "Шасси","wheels" to "Колёса")
    wear.forEach { (key,value) ->
        val color=if(value>.3) Color(0xFFFF575D) else if(value>.05) Color(0xFFFFC449) else Color(0xFF7DDA71)
        Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) { Text(names[key] ?: key,fontSize=14.sp);Text("${(value*100).toInt()}%",color=color,fontWeight=FontWeight.Bold) }
        LinearProgressIndicator(progress={value.toFloat()},modifier=Modifier.fillMaxWidth().height(5.dp),color=color,trackColor=LocalProfileDesign.current.line)
    }
}
@Composable private fun FarmDrawing(kind:String,attachment:VehicleAttachment?,small:Boolean=false) {
    val farm=vehicleAtlas("farm");val extra=vehicleAtlas("equipment");val d=LocalProfileDesign.current
    val root=farmSprite(kind);val tool=attachment?.let { farmSprite(it.kind) }
    val lift by animateFloatAsState(if(attachment?.lowered==false) 1f else 0f,tween(450),label="implement-lift")
    if(root==null) { Text("Схема для этого класса пока не определена",color=d.muted);return }
    Canvas(Modifier.fillMaxWidth().height(if(small) 135.dp else 185.dp)) {
        val ground=size.height*.9f;val width=size.width*(if(tool!=null) .52f else .98f)
        fun image(s:VehicleSprite)=if(s.atlas=="farm") farm else extra
        val h=(width*root.h/root.w).coerceAtMost(size.height*.83f);val w=h*root.w/root.h
        val x=if(tool!=null) 0f else (size.width-w)/2
        drawLine(d.line,Offset(0f,ground),Offset(size.width,ground),1.dp.toPx());sprite(image(root),root,x,ground-h,w,h)
        if(tool!=null) {
            val tx=x+w*.93f;val tw=size.width-tx;val th=(tw*tool.h/tool.w).coerceAtMost(size.height*.7f);val pivot=Offset(tx,ground-th*.6f)
            withTransform({rotate(-lift*12f,pivot)}) { sprite(image(tool),tool,tx,ground-th-lift*size.height*.09f,tw,th) }
            drawCircle(if(attachment.lowered==null) d.muted else d.accent,3.dp.toPx(),pivot)
        }
    }
}
@Composable internal fun RoadDrawing(kind:String,wheels:List<VehicleWheel> = emptyList(),trailer:VehicleAttachment?=null,values:Map<String,Double> = emptyMap(),drive:Boolean?=null,heightOverride:Dp?=null) {
    val atlas=vehicleAtlas(if(kind=="gt") "gt" else "road");val tyre=vehicleAtlas("equipment");val d=LocalProfileDesign.current;val s=roadSprite(kind)
    if(s==null) { Text("Нет схемы этого класса · без предположений о кузове",color=d.muted);return }
    Canvas(Modifier.fillMaxWidth().height(heightOverride ?: if(trailer!=null) 410.dp else 290.dp)) {
        val cx=size.width/2;val h=if(trailer!=null) size.height*.61f else size.height;val width=h
        val bodyWidth=h*s.w/s.h
        sprite(atlas,s,cx-bodyWidth/2,0f,bodyWidth,h)
        fun wheelSet(list:List<VehicleWheel>,from:Float,to:Float,bodyWidth:Float) {
            if(list.isEmpty()) return
            val min=list.minOf { it.z };val span=(list.maxOf { it.z }-min).coerceAtLeast(1.0);val half=list.maxOf { kotlin.math.abs(it.x) }.coerceAtLeast(.5)
            list.forEach { w ->
                val wx=cx+(w.x/half*bodyWidth*.41).toFloat();val wy=from+((w.z-min)/span*(to-from)).toFloat();val tw=bodyWidth*.18f;val th=tw*1.7f
                sprite(tyre,VehicleSprite("equipment",1670,80,330,560),wx-tw/2,wy-th/2,tw,th)
                if(w.powered==true || drive==true) drawRoundRect(d.accent.copy(alpha=.8f),Offset(wx-tw*.33f,wy-th*.33f),Size(tw*.66f,th*.66f),androidx.compose.ui.geometry.CornerRadius(4f),style=Stroke(1.5.dp.toPx()))
            }
        }
        if(kind !in listOf("formula","gt")) wheelSet(wheels,h*.24f,h*.82f,width*.55f)
        if(trailer!=null) {
            val ty=h*.48f;val th=size.height-ty
            wheelSet(trailer.wheels,ty+th*.72f,ty+th*.84f,th*.55f)
            sprite(atlas,roadSprite("trailer")!!,cx-th*.18f,ty,th*.36f,th,alpha=.5f)
        }
        if(kind=="formula") listOf("frontLeftWingDamage" to -.15f,"frontRightWingDamage" to .15f).forEach { (key,side) -> values[key]?.let { damage ->
            val c=if(damage>=30) Color(0xFFFF575D) else if(damage>5) Color(0xFFFFC449) else Color(0xFF7DDA71)
            drawLine(c,Offset(cx+side*width-width*.07f,h*.055f),Offset(cx+side*width+width*.07f,h*.1f),9.dp.toPx(),StrokeCap.Round)
        } }
    }
}
@Composable internal fun SnowVehiclePanel(state:DeckState) {
    var count by rememberSaveable { mutableIntStateOf(4) };val d=LocalProfileDesign.current
    DesignCard {
        Text("ПРИВОД И БЛОКИРОВКА",fontSize=20.sp,fontWeight=FontWeight.Black)
        Text("Ручной выбор схемы · живая телеметрия пока недоступна",fontSize=12.sp,color=d.muted)
        BoxWithConstraints {
            val rows=listOf(4,6,8,10).chunked(if(maxWidth<340.dp) 2 else 4)
            Column { rows.forEach { row -> Row(horizontalArrangement=Arrangement.spacedBy(5.dp)) { row.forEach { n -> FilterChip(selected=count==n,onClick={count=n},label={Text("$n колёс",fontSize=12.sp)}) } } } }
        }
        RoadDrawing(if(count==4) "suv" else "truck",List(count) { i -> VehicleWheel(if(i%2==0) -1.0 else 1.0,(i/2).toDouble(),null) })
        Text("$count колёс · ${count/2} оси · иллюстрация",color=d.accent,fontSize=14.sp)
        Text("Полный привод: неизвестно\nБлокировка: неизвестно",fontSize=16.sp)
    }
}
