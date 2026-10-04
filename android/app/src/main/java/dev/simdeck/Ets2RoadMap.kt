package dev.simdeck

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.withTransform
import androidx.compose.ui.unit.dp
import org.json.JSONObject
import kotlin.math.*

data class EtsRoad(val points:FloatArray,val width:Float)
data class EtsCity(val name:String,val x:Float,val z:Float)
data class EtsMap(val x:Double,val z:Double,val span:Double,val roads:List<EtsRoad>,val cities:List<EtsCity>) {
    companion object {
        fun parse(j:JSONObject):EtsMap {
            val x=j.getDouble("x");val z=j.getDouble("z");val span=j.getDouble("span")
            require(x.isFinite() && z.isFinite() && span in 800.0..16000.0)
            val array=j.getJSONArray("roads"); require(array.length()<=8000)
            val roads=(0 until array.length()).map { i->
                val r=array.getJSONObject(i);val p=r.getJSONArray("p");require(p.length() in 4..128 && p.length()%2==0)
                EtsRoad(FloatArray(p.length()) { k->p.getDouble(k).also { require(it.isFinite() && abs(it)<=1000000) }.toFloat() },r.optDouble("w",10.0).coerceIn(4.0,80.0).toFloat())
            }
            val towns=j.getJSONArray("cities");require(towns.length()<=5000)
            val cities=(0 until towns.length()).map { i->val c=towns.getJSONObject(i);EtsCity(c.getString("name").take(100),c.getDouble("x").toFloat(),c.getDouble("z").toFloat()) }
            return EtsMap(x,z,span,roads,cities)
        }
    }
}

@Composable internal fun Ets2RoadMap(state:DeckState) {
    val design=LocalProfileDesign.current
    val map=state.etsMap;val n=state.telemetry?.ets2Navigation
    val roadPaths=remember(map) { map?.roads?.map { road->
        Path().apply { road.points.indices.step(2).forEach { i->if(i==0)moveTo(road.points[i],road.points[i+1]) else lineTo(road.points[i],road.points[i+1]) } } to road.width
    } ?: emptyList() }
    var span by rememberSaveable { mutableDoubleStateOf(1600.0) }
    Column {
        Canvas(Modifier.fillMaxWidth().height(260.dp).background(design.background).clipToBounds()) {
            val x=n?.worldX ?: map?.x ?: 0.0;val z=n?.worldZ ?: map?.z ?: 0.0
            val scale=size.width/span
            fun point(a:Double,b:Double)=Offset((size.width/2+(a-x)*scale).toFloat(),(size.height/2+(b-z)*scale).toFloat())
            withTransform({ translate((size.width/2-x*scale).toFloat(),(size.height/2-z*scale).toFloat());scale(scale.toFloat(),scale.toFloat(),pivot=Offset.Zero) }) {
                for((path,width) in roadPaths) drawPath(path,Color(0xff526575),style=Stroke(max(4f/scale.toFloat(),width)+2f/scale.toFloat(),cap=StrokeCap.Round))
                for((path,width) in roadPaths) drawPath(path,Color(0xffa8bac6),style=Stroke(max(2f/scale.toFloat(),width),cap=StrokeCap.Round))
            }
            if(n?.worldX!=null && n.worldZ!=null) {
                val angle=-(n.heading ?: 0.0)*2*PI
                fun arrow(a:Double,b:Double)=Offset((size.width/2+a*cos(angle)-b*sin(angle)).toFloat(),(size.height/2+a*sin(angle)+b*cos(angle)).toFloat())
                val a=arrow(0.0,-14.0);val b=arrow(-10.0,11.0);val c=arrow(10.0,11.0)
                val path=Path().apply { moveTo(a.x,a.y);lineTo(b.x,b.y);lineTo(c.x,c.y);close() }
                drawCircle(Color(0xfff7b52c).copy(alpha=.2f),25f)
                drawPath(path,Color(0xffffc449));drawPath(path,Color.White,style=Stroke(2f))
            }
        }
        Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween) {
            TextButton(onClick={span=max(800.0,span/2)}) { Text("＋") }
            Text(map?.cities?.minByOrNull { c->hypot(c.x-(n?.worldX?:map.x),c.z-(n?.worldZ?:map.z)) }?.name ?: "Север ↑",color=design.muted)
            TextButton(onClick={span=min(3200.0,span*2)}) { Text("−") }
        }
        Text(if(map==null)state.etsMapStatus else "Дороги из ${profileShortName(state.profileId)} · позиция грузовика · без линии GPS-маршрута",color=design.muted)
    }
}
