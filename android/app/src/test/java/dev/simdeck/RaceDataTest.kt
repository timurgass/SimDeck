package dev.simdeck
import org.junit.Assert.*
import org.junit.Test
import org.json.JSONObject

class RaceDataTest {
    @Test fun distanceWrapsAndScalePreservesLapProgress() {
        val c=Circuit(1,"Square",listOf(MapPoint(0f,0f),MapPoint(100f,0f),MapPoint(100f,100f),MapPoint(0f,100f),MapPoint(0f,0f)))
        assertEquals(MapPoint(100f,0f),c.at(1250.0,5000));assertEquals(c.at(3750.0,5000),c.at(-1250.0,5000))
        assertEquals(c.at(0.0,5000),c.at(5000.0,5000));assertNull(c.at(100.0,0));assertNull(c.at(Double.NaN,5000))
    }
    @Test fun allBundledCircuitsAreClosedAndFinite() {
        val all=Circuit.parseAll(javaClass.classLoader!!.getResource("f1-circuits.json")!!.readText())
        assertEquals(28,all.size);assertTrue(all.containsKey(27));assertTrue(all.containsKey(32))
        all.values.forEach { c -> assertTrue(c.points.size>20);assertEquals(c.points.first(),c.points.last());assertTrue(c.points.all { it.x.isFinite()&&it.y.isFinite() });assertNotNull(c.at(100.0,5000)) }
    }
    @Test fun raceNamesOrderingAndMissingDataAreSafe() {
        val r=RaceData.parse(JSONObject("""{"trackId":27,"trackLength":4909,"fresh":false,"drivers":[{"index":1,"name":"Max Verstappen","position":2,"distance":200,"result":2,"driverStatus":4},{"index":0,"name":"Игрок Тест","position":1,"distance":-50,"player":true}]}"""))!!
        assertFalse(r.fresh);assertTrue(r.drivers[0].player);assertEquals("VER",r.drivers[1].shortName);assertTrue(r.drivers[1].onTrack)
        assertNull(RaceData.parse(null)); assertFalse(r.drivers[0].onTrack)
    }
}
