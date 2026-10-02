package dev.simdeck

import org.junit.Assert.*
import org.junit.Test

class FarmGeometryTest {
    @Test fun everyAttachmentKeepsItsAspectAndFitsWithTheMachine() {
        for(width in listOf(280f,700f)) for(root in fs25EquipmentSprites.values) for(tool in fs25EquipmentSprites.values) for(front in listOf(false,true)) {
            val g=farmGeometry(width,185f,root.w.toFloat()/root.h,tool.w.toFloat()/tool.h,front)
            val t=g.tool!!
            assertEquals(root.w.toFloat()/root.h,g.root.w/g.root.h,.00001f)
            assertEquals(tool.w.toFloat()/tool.h,t.w/t.h,.00001f)
            for(r in listOf(g.root,t)) {
                assertTrue(r.x>=-.001f && r.x+r.w<=width+.001f)
                assertTrue(r.y>=0f && r.y+r.h<=185f)
            }
            assertTrue(t.h<g.root.h)
            assertTrue(if(front) t.x<g.root.x else t.x>g.root.x)
        }
    }
    @Test fun detachedMachineDoesNotReserveAnAttachmentSlot() {
        val g=farmGeometry(320f,230f,1.5f,null,false)
        assertNull(g.tool)
        assertEquals(160f,g.root.x+g.root.w/2,.001f)
    }
}
