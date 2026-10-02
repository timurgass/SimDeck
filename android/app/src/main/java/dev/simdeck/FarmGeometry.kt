package dev.simdeck

internal data class FarmRect(val x:Float,val y:Float,val w:Float,val h:Float)
internal data class FarmGeometry(val root:FarmRect,val tool:FarmRect?,val pivotX:Float,val pivotY:Float)

// Fit the whole hitch scene with ONE scale; never clamp height independently of width.
internal fun farmGeometry(width:Float,height:Float,rootAspect:Float,toolAspect:Float?,front:Boolean):FarmGeometry {
    val ground=height*.9f
    val rh=height*.74f;val rw=rh*rootAspect
    val th=if(toolAspect!=null) height*.48f else 0f;val tw=th*(toolAspect ?: 0f)
    val overlap=if(toolAspect!=null) minOf(rw,tw)*.08f else 0f
    val total=rw+tw-overlap;val scale=minOf(1f,width*.96f/total)
    val start=(width-total*scale)/2f
    val rx=start+if(front && toolAspect!=null) (tw-overlap)*scale else 0f
    val tx=if(front) start else start+(rw-overlap)*scale
    val root=FarmRect(rx,ground-rh*scale,rw*scale,rh*scale)
    val tool=toolAspect?.let { FarmRect(tx,ground-th*scale,tw*scale,th*scale) }
    return FarmGeometry(root,tool,if(front) tx+tw*scale else tx,ground-th*scale*.45f)
}
