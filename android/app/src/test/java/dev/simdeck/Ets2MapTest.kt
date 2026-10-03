package dev.simdeck
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class Ets2MapTest {
    @Test fun signedGeometryAndCitiesArePreserved() {
        val map=EtsMap.parse(JSONObject("""{"x":-10,"z":20,"span":6400,"roads":[{"p":[-10,20,0,30],"w":12}],"cities":[{"name":"Town","x":0,"z":0}]}"""))
        assertEquals(-10.0,map.x,0.0)
        assertArrayEquals(floatArrayOf(-10f,20f,0f,30f),map.roads.single().points,0f)
        assertEquals("Town",map.cities.single().name)
    }
    @Test fun corruptCoordinatesAndOddPointArraysAreRejected() {
        for(p in listOf("[0,0,1]","[0,0,1000001,1]")) {
            try { EtsMap.parse(JSONObject("""{"x":0,"z":0,"span":1600,"roads":[{"p":$p}],"cities":[]}"""));fail("Invalid road accepted") }
            catch(_:IllegalArgumentException) { }
        }
    }
}
