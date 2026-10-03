package dev.simdeck

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.util.Locale

internal fun damageColor(value:Double?)=when { value==null -> Color(0xFF9CAAB0); value>30 -> Color(0xFFFF575D); value>5 -> Color(0xFFFFC449);else -> Color(0xFF7DDA71) }
internal fun f1FaultLabel(value:Double?)=when(value) { 0.0 -> "ИСПРАВЕН";1.0 -> "НЕИСПРАВЕН";else -> "НЕТ ДАННЫХ" }
@Composable internal fun F1Schematic(data:F1Data?) {
    val d=LocalProfileDesign.current;val v=data?.values.orEmpty()
    val compact=LocalConfiguration.current.screenWidthDp>=650 && LocalConfiguration.current.screenHeightDp<750
    fun n(value:Double?,unit:String="",dec:Int=0)=value?.let { String.format(Locale.US,"%.${dec}f",it)+unit } ?: "—"
    val wheel:@Composable (Int,String)->Unit = { index,name ->
        val w=data?.wheels?.getOrNull(index)
        DesignCard(if(compact) 4 else 10,if(compact) 10 else 17) {
            Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween,verticalAlignment=Alignment.CenterVertically) {
                Text(name,fontSize=13.sp,lineHeight=17.sp,fontWeight=FontWeight.Bold,modifier=Modifier.weight(1f))
                val compound=when(v["compound"]?.toInt()) {16,20 -> "S";17,21 -> "M";18,22 -> "H";7 -> "I";8,15 -> "W";else -> "—"}
                val color=when(compound) {"S" -> Color(0xFFFF575D);"M" -> Color(0xFFFFC449);"H" -> Color.White;"I" -> Color(0xFF7DDA71);else -> Color(0xFF61B8FF)}
                Box(Modifier.size(if(compact) 23.dp else 30.dp).border(2.dp,color,CircleShape),contentAlignment=Alignment.Center) {Text(compound,color=color,fontWeight=FontWeight.Bold)}
            }
            HorizontalDivider(color=d.line)
            listOf(Triple("Износ",w?.wear,"%"),Triple("Поверхность",w?.surface," °C"),Triple("Внутри",w?.inner," °C"),Triple("Давление",w?.pressure," PSI")).forEach { (label,value,unit) ->
                val color=if(unit=="%") when {value==null->d.muted;value>50->Color(0xFFFF575D);value>25->Color(0xFFFFC449);else->Color(0xFF7DDA71)} else when {value==null->d.muted;value>105->Color(0xFFFF575D);value>90->Color(0xFFFF9F32);value>=80->Color(0xFFFFD945);else->Color(0xFF61B8FF)}
                val bar:@Composable (Modifier)->Unit={ modifier->if(unit!=" PSI" && value!=null) LinearProgressIndicator(progress={(value/(if(unit=="%") 100 else 140)).toFloat().coerceIn(0f,1f)},color=color,trackColor=d.line,modifier=modifier.height(8.dp)) else Spacer(modifier.height(8.dp)) }
                BoxWithConstraints {
                    if(maxWidth>=220.dp) Row(Modifier.fillMaxWidth(),verticalAlignment=Alignment.CenterVertically,horizontalArrangement=Arrangement.spacedBy(8.dp)) {
                        Text(label,fontSize=13.sp,lineHeight=17.sp,color=d.muted,modifier=Modifier.weight(1.2f))
                        Text(n(value,unit,if(unit==" PSI") 1 else 0),fontSize=if(compact) 17.sp else 20.sp,lineHeight=if(compact) 21.sp else 24.sp,fontWeight=FontWeight.Bold,color=if(unit==" °C") color else Color.White,modifier=Modifier.weight(1f))
                        bar(Modifier.weight(.9f))
                    } else Column(verticalArrangement=Arrangement.spacedBy(4.dp)) {
                        Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) {Text(label,fontSize=12.sp,lineHeight=16.sp,color=d.muted);Text(n(value,unit,if(unit==" PSI") 1 else 0),fontSize=17.sp,lineHeight=21.sp,fontWeight=FontWeight.Bold,color=if(unit==" °C") color else Color.White)}
                        if(unit!=" PSI" && value!=null) bar(Modifier.fillMaxWidth())
                    }
                }
                HorizontalDivider(color=d.line)
            }
            Text("Тормоз ${n(w?.brake," °C")} · урон ${n(w?.damage,"%")}",fontSize=11.sp,lineHeight=15.sp,color=d.muted)
        }
    }
    Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.spacedBy(10.dp)) {
        listOf("frontLeftWingDamage" to "ЛЕВОЕ ПЕРЕДНЕЕ КРЫЛО","frontRightWingDamage" to "ПРАВОЕ ПЕРЕДНЕЕ КРЫЛО").forEach { (key,label) ->
            Column(Modifier.weight(1f).background(d.panelAlt).padding(if(compact) 8.dp else 12.dp)) { Text(label,fontSize=11.sp,lineHeight=15.sp,color=d.muted);Text(n(v[key],"%"),fontSize=if(compact) 19.sp else 25.sp,fontWeight=FontWeight.Black,color=damageColor(v[key])) }
        }
    }
    BoxWithConstraints {
        if(maxWidth>=650.dp) Row(horizontalArrangement=Arrangement.spacedBy(16.dp),verticalAlignment=Alignment.CenterVertically) {
            Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(if(compact) 12.dp else 20.dp)) { wheel(2,"ПЕРЕДНЯЯ ЛЕВАЯ");wheel(0,"ЗАДНЯЯ ЛЕВАЯ") }
            Column(Modifier.weight(1f)) { RoadDrawing("formula",values=v,heightOverride=if(compact) 345.dp else 490.dp) }
            Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(if(compact) 12.dp else 20.dp)) { wheel(3,"ПЕРЕДНЯЯ ПРАВАЯ");wheel(1,"ЗАДНЯЯ ПРАВАЯ") }
        } else Column(verticalArrangement=Arrangement.spacedBy(12.dp)) {
            RoadDrawing("formula",values=v,heightOverride=340.dp)
            listOf(listOf(2 to "ПЕРЕДНЯЯ ЛЕВАЯ",3 to "ПЕРЕДНЯЯ ПРАВАЯ"),listOf(0 to "ЗАДНЯЯ ЛЕВАЯ",1 to "ЗАДНЯЯ ПРАВАЯ")).forEach { row -> Row(horizontalArrangement=Arrangement.spacedBy(9.dp)) { row.forEach { (i,name) -> Column(Modifier.weight(1f)) { wheel(i,name) } } } }
        }
    }
    BoxWithConstraints {
        val items=listOf("rearWingDamage" to "ЗАДНЕЕ КРЫЛО","floorDamage" to "ДНИЩЕ","sidepodDamage" to "БОКОВИНЫ · ОБЩИЙ УРОН","drsFault" to "СОСТОЯНИЕ DRS")
        val columns=if(maxWidth>=650.dp) 4 else 2
        Column(verticalArrangement=Arrangement.spacedBy(9.dp)) { items.chunked(columns).forEach { row -> Row(horizontalArrangement=Arrangement.spacedBy(9.dp)) { row.forEach { (key,label) -> Column(Modifier.weight(1f).background(d.panelAlt).padding(if(compact) 8.dp else 12.dp)) { Text(label,fontSize=10.sp,lineHeight=14.sp,color=d.muted);Text(if(key=="drsFault") f1FaultLabel(v[key]) else n(v[key],"%"),fontSize=16.sp,lineHeight=20.sp,fontWeight=FontWeight.Bold,color=if(key=="drsFault") if(v[key]==1.0) Color(0xFFFF575D) else if(v[key]==0.0) Color(0xFF7DDA71) else d.muted else damageColor(v[key])) } } } } }
    }
}
