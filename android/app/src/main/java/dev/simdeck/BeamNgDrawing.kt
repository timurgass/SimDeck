package dev.simdeck

import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.vector.PathParser
import androidx.compose.ui.platform.LocalContext
import org.json.JSONObject

internal data class BeamNgZone(val key:String,val path:Path)
internal data class BeamNgDrawing(val zones:List<BeamNgZone>,val front:Float,val rear:Float,val side:Float,val width:Float,val height:Float)
@Composable internal fun rememberBeamNgDrawing(kind:String):BeamNgDrawing? {
 val context=LocalContext.current
 return remember(kind){
  val drawing=JSONObject(context.assets.open("beamng-damage-ui.json").bufferedReader().use{it.readText()}).getJSONObject("drawing").optJSONObject(kind)
  drawing?.let{val zones=it.getJSONArray("zones");val w=it.getJSONObject("wheel");BeamNgDrawing(List(zones.length()){i->val z=zones.getJSONObject(i);BeamNgZone(z.getString("key"),PathParser().parsePathString(z.getString("path")).toPath())},w.getDouble("front").toFloat(),w.getDouble("rear").toFloat(),w.getDouble("side").toFloat(),w.getDouble("width").toFloat(),w.getDouble("height").toFloat())}
 }
}
