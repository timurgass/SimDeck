package dev.simdeck

import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class F1DataTest {
    @Test fun absentAuxiliaryDoesNotBecomeZero() {
        val f = F1Data.parse(JSONObject("""{"wheels":[{},{},{},{}],"values":{},"trail":[],"position":null}"""))!!
        assertNull(f.wheels[2].wear); assertNull(f.position); assertNull(f.engineTemperature)
    }
    @Test fun valuesAndCoordinatesParseFromWire() {
        val f = F1Data.parse(JSONObject("""{"wheels":[{},{},{"wear":24.5,"surface":92,"inner":99,"pressure":22.7},{}],"values":{"nextFrontWing":26},"trail":[{"x":100,"y":200}],"position":{"x":103,"y":208},"engineTemperature":105}"""))!!
        assertEquals(24.5,f.wheels[2].wear!!,0.01); assertEquals(26.0,f.values["nextFrontWing"]!!,0.01)
        assertEquals(MapPoint(103f,208f),f.position); assertEquals(1,f.trail.size)
    }
    @Test fun incompleteWheelArrayRejected() { assertNull(F1Data.parse(JSONObject("""{"wheels":[{},{}]}"""))) }
    @Test fun mfdPageIsOptionalAndBounded() {
        fun page(value: String) = F1Data.parse(JSONObject("""{"wheels":[{},{},{},{}],"mfdPanelIndex":$value}"""))!!.mfdPanelIndex
        assertEquals(1,page("1")); assertNull(page("null")); assertNull(page("255"))
    }
}
