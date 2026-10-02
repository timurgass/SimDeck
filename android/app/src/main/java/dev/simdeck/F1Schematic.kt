package dev.simdeck

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.util.Locale

@Composable internal fun F1Schematic(data:F1Data?) {
    val d=LocalProfileDesign.current;val v=data?.values.orEmpty()
    fun n(value:Double?,unit:String="")=value?.let { String.format(Locale.US,"%.1f",it)+unit } ?: "—"
    val wheel:@Composable (Int,String)->Unit = { index,name ->
        val w=data?.wheels?.getOrNull(index)
        DesignCard(7) {
            Text(name,fontSize=14.sp,fontWeight=FontWeight.Bold)
            listOf(Triple("Износ",w?.wear,"%"),Triple("Поверхность",w?.surface," °C"),Triple("Внутри",w?.inner," °C"),Triple("Давление",w?.pressure," PSI")).forEach { (label,value,unit) ->
                Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) { Text(label,fontSize=12.sp,color=d.muted);Text(n(value,unit),fontSize=16.sp,fontWeight=FontWeight.Bold) }
                if(unit!=" PSI") LinearProgressIndicator(progress={((value ?: 0.0)/(if(unit=="%") 100 else 140)).toFloat().coerceIn(0f,1f)},color=if(unit=="%") Color(0xFF7DDA71) else Color(0xFFFFC449),trackColor=d.line,modifier=Modifier.fillMaxWidth().height(4.dp))
            }
            Text("Тормоз ${n(w?.brake," °C")} · урон ${n(w?.damage,"%")}",fontSize=11.sp,color=d.muted)
        }
    }
    BoxWithConstraints {
        if(maxWidth>=650.dp) Row(horizontalArrangement=Arrangement.spacedBy(12.dp)) {
            Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(12.dp)) { wheel(2,"ПЕРЕДНЯЯ ЛЕВАЯ");wheel(0,"ЗАДНЯЯ ЛЕВАЯ") }
            Column(Modifier.weight(.85f)) { RoadDrawing("formula",values=v) }
            Column(Modifier.weight(1f),verticalArrangement=Arrangement.spacedBy(12.dp)) { wheel(3,"ПЕРЕДНЯЯ ПРАВАЯ");wheel(1,"ЗАДНЯЯ ПРАВАЯ") }
        } else Column(verticalArrangement=Arrangement.spacedBy(12.dp)) {
            RoadDrawing("formula",values=v)
            listOf(listOf(2 to "ПЕРЕДНЯЯ ЛЕВАЯ",3 to "ПЕРЕДНЯЯ ПРАВАЯ"),listOf(0 to "ЗАДНЯЯ ЛЕВАЯ",1 to "ЗАДНЯЯ ПРАВАЯ")).forEach { row -> Row(horizontalArrangement=Arrangement.spacedBy(9.dp)) { row.forEach { (i,name) -> Column(Modifier.weight(1f)) { wheel(i,name) } } } }
        }
    }
    listOf("frontLeftWingDamage" to "Переднее крыло Л","frontRightWingDamage" to "Переднее крыло П","rearWingDamage" to "Заднее крыло","floorDamage" to "Днище").forEach { (key,label) ->
        Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) { Text(label,fontSize=14.sp);Text(n(v[key],"%"),color=if((v[key] ?: 0.0)>30) Color(0xFFFF575D) else d.accent,fontWeight=FontWeight.Bold) }
    }
}
