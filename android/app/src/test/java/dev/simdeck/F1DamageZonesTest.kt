package dev.simdeck

import org.junit.Assert.*
import org.junit.Test

class F1DamageZonesTest {
    @Test fun unknownAndInvalidDamageNeverPaintHealthy() {
        for(value in listOf(null,Double.NaN,Double.POSITIVE_INFINITY,-1.0,101.0)) assertNull(f1DamageLevel(value))
        assertEquals(0,f1DamageLevel(0.0));assertEquals(0,f1DamageLevel(5.0))
        assertEquals(1,f1DamageLevel(5.1));assertEquals(1,f1DamageLevel(30.0));assertEquals(2,f1DamageLevel(30.1))
    }
    @Test fun drsFlapCombinesWingDamageWithBinaryFaultWithoutInventingFaultSeverity() {
        assertEquals(20.0,f1ZoneDamage("drsFault",mapOf("drsFault" to 0.0,"rearWingDamage" to 20.0))!!,0.0)
        assertEquals(100.0,f1ZoneDamage("drsFault",mapOf("drsFault" to 1.0,"rearWingDamage" to 0.0))!!,0.0)
        assertNull(f1ZoneDamage("drsFault",mapOf("drsFault" to 2.0)))
        assertNull(f1ZoneDamage("drsFault",emptyMap()))
    }
}
