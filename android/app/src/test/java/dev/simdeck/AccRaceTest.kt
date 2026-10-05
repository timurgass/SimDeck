package dev.simdeck

import org.json.JSONArray
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class AccRaceTest {
    private fun sample()=JSONObject("""{"track":"monza","trackLength":5793,"sessionType":10,"fresh":true,"replay":false,"bins":256,"points":[{"bin":1,"x":12,"z":34},{"bin":2,"x":15,"z":38}],"drivers":[{"index":7,"name":"Игрок Тест","shortName":"ТСТ","team":"Team","number":42,"cup":3,"position":2,"lap":4,"x":13,"z":35,"location":1,"bestLapMs":95000,"lastLapMs":96000,"gapAheadMs":1250,"gapLeaderMs":2500,"player":true,"fresh":true}]}""")
    @Test fun playerNamesTimesAndGeometrySurviveProtocol(){val r=AccRaceData.parse(sample())!!;assertEquals("Игрок Тест",r.drivers.single().name);assertTrue(r.drivers.single().player);assertEquals(1250,r.drivers.single().gapAheadMs);assertEquals("1:35.000",accLap(r.drivers.single().bestLapMs));assertEquals(2,r.points.size)}
    @Test fun badRaceDoesNotFabricateGeometry(){assertNull(AccRaceData.parse(sample().put("bins",0)));val p=sample();p.getJSONArray("points").getJSONObject(1).put("bin",1);assertNull(AccRaceData.parse(p));val d=sample();d.getJSONArray("drivers").getJSONObject(0).put("x","NaN");assertNull(AccRaceData.parse(d))}
    @Test fun absentAndStaleDataStayDistinct(){assertNull(AccRaceData.parse(null));val r=AccRaceData.parse(sample().put("fresh",false))!!;assertFalse(r.fresh);assertEquals(1,r.drivers.size);assertEquals(2,r.points.size)}
    @Test fun phoneHasRaceAndPitRemainsInAllSections(){assertTrue(phoneTabs("acc").any{it.id=="map"});assertEquals("more",phoneTabs("acc").last().id)}
}
