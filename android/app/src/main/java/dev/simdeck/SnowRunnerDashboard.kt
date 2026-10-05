package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.*
import androidx.compose.ui.graphics.drawscope.*
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@Composable internal fun SnowRunnerDashboard(state:DeckState,model:DeckModel) {
 BoxWithConstraints {
  if(maxWidth>=650.dp) Row(horizontalArrangement=Arrangement.spacedBy(16.dp)) {
   Column(Modifier.weight(1.15f)){SnowRunnerScene(state)}
   Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(12.dp)){SnowRunnerStates(state,model);SnowRunnerCondition(state)}
  } else Column(verticalArrangement=Arrangement.spacedBy(12.dp)){SnowRunnerScene(state);SnowRunnerStates(state,model);SnowRunnerCondition(state)}
 }
 ReferenceButtons(state,model,listOf("snowQuickWinch" to "Лебёдка","snowPackCargo" to "Груз","snowMap" to "Карта","snowFunctions" to "Функции"),4)
}
@Composable internal fun SnowRunnerScene(state:DeckState) {
 val d=LocalProfileDesign.current;val t=state.telemetry;val v=t?.vehicle ?: state.lastVehicle
 var manual by rememberSaveable {mutableIntStateOf(6)}
 val wheels=v?.wheels?.takeIf{it.isNotEmpty()} ?: List(manual){i->VehicleWheel(if(i%2==0)-1.0 else 1.0,(i/2).toDouble(),null)}
 val kind=v?.kind?.takeIf{it in setOf("suv","truck","pickup","van")} ?: if(wheels.size==4)"suv" else "truck"
 DesignCard {
  Text("ТЕХНИКА И ТРАНСМИССИЯ",fontSize=20.sp,lineHeight=25.sp,fontWeight=FontWeight.Black)
  Text(v?.name ?: "Выберите схему машины",fontSize=20.sp,lineHeight=25.sp,color=d.accent,fontWeight=FontWeight.Bold)
  Text(if(v!=null)"АВТО · ${wheels.size} колёс · ${v.axleCount ?: wheels.size/2} оси" else "Ручная схема · данные машины пока не получены",color=d.muted,fontSize=12.sp)
  if(v==null)Row(Modifier.horizontalScroll(rememberScrollState()),horizontalArrangement=Arrangement.spacedBy(6.dp)){listOf(4,6,8,10).forEach{n->FilterChip(selected=manual==n,onClick={manual=n},label={Text("$n колёс")})}}
  SnowRunnerDrawing(kind,wheels,t?.actionStates?.get("snowAwd"),t?.actionStates?.get("snowDifferential"))
  Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) {
   Column {Text("СКОРОСТЬ",fontSize=11.sp,color=d.muted);Text(t?.let{"%.0f км/ч".format(it.speedMps*3.6)} ?: "—",fontSize=25.sp,fontWeight=FontWeight.Bold)}
   Column(horizontalAlignment=Alignment.End){Text("ТОПЛИВО",fontSize=11.sp,color=d.muted);Text(t?.snowRunner?.let{snow->t.fuelLiters?.let{"%.0f / %.0f л".format(it,snow.fuelCapacity)}} ?: "—",fontSize=25.sp,fontWeight=FontWeight.Bold)}
  }
  Text(if(v==null)"Нужны данные игры · нажатия доступны отдельно" else if(state.stale)"Последняя машина · обновление задержалось" else "Иллюстрация класса · число осей и параметры из игры",fontSize=11.sp,lineHeight=15.sp,color=d.muted)
 }
}
@Composable internal fun SnowRunnerStates(state:DeckState,model:DeckModel) {
 val d=LocalProfileDesign.current
 Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(10.dp)) {
  listOf("snowAwd" to "ПОЛНЫЙ ПРИВОД","snowDifferential" to "БЛОКИРОВКА").forEach { (id,label)->
   Column(Modifier.weight(1f)){DesignCard(8,12){
    val enabled=state.telemetry?.actionStates?.get(id)
    ProfileActionIcon(id,if(enabled==true)d.accent else d.muted)
    Text(label,fontWeight=FontWeight.Bold,fontSize=13.sp,lineHeight=17.sp)
    Text(when(enabled){true->"ВКЛЮЧЕНО";false->"ВЫКЛЮЧЕНО";null->"НЕИЗВЕСТНО"},fontWeight=FontWeight.Black,fontSize=17.sp,color=if(enabled==true)d.accent else d.muted)
    ReferenceButtons(state,model,listOf(id to "Переключить"),1)
   }}
  }
 }
 Text("Переключаемая блокировка работает на L. Доступность зависит от оборудования машины.",fontSize=11.sp,lineHeight=15.sp,color=d.muted)
}
@Composable internal fun SnowRunnerCondition(state:DeckState) {
 val d=LocalProfileDesign.current;val components=state.telemetry?.snowRunner?.components.orEmpty()
 DesignCard(8,12) {
  Text("ЗАПАС ПРОЧНОСТИ",fontSize=17.sp,fontWeight=FontWeight.Black)
  if(components.isEmpty())Text("Ожидание диагностики машины",color=d.muted,fontSize=13.sp)
  components.forEach { c->
   val color=damageColor(c.damageFraction*100)
   Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween){Text(c.name,fontSize=14.sp);Text("${c.remaining} / ${c.capacity}",fontWeight=FontWeight.Bold,color=color,fontSize=15.sp)}
   LinearProgressIndicator(progress={1f-c.damageFraction.toFloat()},modifier=Modifier.fillMaxWidth(),color=color,trackColor=d.panelAlt)
   Text("Повреждение %.0f%%".format(c.damageFraction*100),color=d.muted,fontSize=11.sp)
  }
 }
}

// Replace the stock sprite's tyres with one set using actual axle spacing. No duplicated boxes.
@Composable internal fun SnowRunnerDrawing(kind:String,wheels:List<VehicleWheel>,awd:Boolean?,diff:Boolean?) {
 val atlas=vehicleAtlas("road");val d=LocalProfileDesign.current
 val s=when(kind){"suv"->VehicleSprite("road",523,6,209,412);"pickup"->VehicleSprite("road",905,9,223,394);"van"->VehicleSprite("road",124,418,225,409);else->VehicleSprite("road",917,418,199,410)}
 Canvas(Modifier.fillMaxWidth().heightIn(min=200.dp,max=340.dp).height(310.dp)) {
  val h=minOf(size.height,size.width*s.h/s.w);val bw=h*s.w/s.h;val left=(size.width-bw)/2;val cx=size.width/2
  val clip=Path().apply{addRect(androidx.compose.ui.geometry.Rect(left,0f,left+bw,h*.18f));addRect(androidx.compose.ui.geometry.Rect(left+bw*.17f,0f,left+bw*.83f,h));addRect(androidx.compose.ui.geometry.Rect(left,h*.91f,left+bw,h))}
  clipPath(clip){sprite(atlas,s,left,0f,bw,h)}
  val color=if(awd==true)d.accent else d.muted.copy(alpha=.65f)
  drawLine(color,Offset(cx,h*.2f),Offset(cx,h*.84f),2.dp.toPx())
  val min=wheels.minOfOrNull{it.z} ?: 0.0;val span=((wheels.maxOfOrNull{it.z} ?: 1.0)-min).coerceAtLeast(1.0)
  val axles=wheels.map{it.z}.distinct().sorted();val gap=axles.zipWithNext().minOfOrNull{(a,b)->(b-a)/span*.65} ?: .18
  val th=h*gap.toFloat().coerceIn(.075f,.15f);val tw=bw*.14f
  wheels.forEach { w->
   val wx=cx+if(w.x<0)-bw*.43f else bw*.43f;val y=h*(.2f+((w.z-min)/span*.64).toFloat())
   drawLine(color,Offset(cx,y),Offset(wx,y),1.5.dp.toPx())
   drawRoundRect(Color(0xFF10181B),Offset(wx-tw/2,y-th/2),Size(tw,th),androidx.compose.ui.geometry.CornerRadius(tw*.3f))
   drawRoundRect(color,Offset(wx-tw/2,y-th/2),Size(tw,th),androidx.compose.ui.geometry.CornerRadius(tw*.3f),style=Stroke(1.dp.toPx()))
   for(j in 1..5)drawLine(Color(0xFF34434B),Offset(wx-tw*.3f,y-th/2+th*j/6),Offset(wx+tw*.3f,y-th/2+th*j/6),1.dp.toPx())
  }
  if(diff==true)axles.forEach{z->val y=h*(.2f+((z-min)/span*.64).toFloat());drawCircle(d.accent,4.dp.toPx(),Offset(cx,y))}
 }
}
