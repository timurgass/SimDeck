package dev.simdeck

import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.vector.PathParser
import androidx.compose.ui.platform.LocalContext
import org.json.JSONObject

internal data class F1DamageZone(val key:String,val path:Path)
internal fun f1DamageLevel(value:Double?):Int? = value?.takeIf { it.isFinite() && it in 0.0..100.0 }?.let { if(it>30) 2 else if(it>5) 1 else 0 }
internal fun f1ZoneDamage(key:String,values:Map<String,Double>):Double? = if(key=="drsFault") when(values[key]) {1.0->100.0;0.0->values["rearWingDamage"];else->null} else values[key]

@Composable internal fun rememberF1DamageZones():List<F1DamageZone> {
    val context=LocalContext.current
    return remember {
        val source=JSONObject(context.assets.open("f1-damage-zones.json").bufferedReader().use { it.readText() })
        val zones=source.getJSONArray("zones")
        List(zones.length()) { index->val zone=zones.getJSONObject(index);F1DamageZone(zone.getString("key"),PathParser().parsePathString(zone.getString("path")).toPath()) }
    }
}
