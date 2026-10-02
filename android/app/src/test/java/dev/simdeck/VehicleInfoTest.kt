package dev.simdeck

import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class VehicleInfoTest {
    @Test fun everyEquipmentClassSurvivesWireParsing() {
        fs25EquipmentLabels.forEach { (kind,label) ->
            val v=VehicleInfo.parse(JSONObject().put("id","root").put("kind",kind))!!
            assertEquals(kind,v.kind)
            assertTrue(label.isNotBlank())
            assertNotNull(fs25EquipmentSprites[kind])
        }
    }
    @Test fun cutterMountAndDetachAreDistinctFromGenericEquipment() {
        val v=VehicleInfo.parse(JSONObject("""{"id":"combine","name":"MF8570","kind":"combine","attachments":[{"id":"cutter","parentId":"combine","kind":"header","mount":"front"}]}"""))!!
        assertEquals("header",v.attachments.single().kind)
        assertEquals("front",v.attachments.single().mount)
        assertTrue(VehicleInfo.parse(JSONObject("""{"id":"combine","kind":"combine","attachments":[]}"""))!!.attachments.isEmpty())
    }

    @Test fun equipmentHierarchyAndActualAxles() {
        val v=VehicleInfo.parse(JSONObject("""{"id":"root","name":"Tractor","kind":"tractor","controlled":true,"wheels":[{"x":-1,"z":-2},{"x":1,"z":-2},{"x":-1,"z":2},{"x":1,"z":2}],"attachments":[{"id":"1","parentId":"root","kind":"implement","lowered":true,"turnedOn":false,"fold":0.5},{"id":"2","parentId":"1","kind":"trailer"}]}"""))!!
        assertEquals(2,v.axleCount);assertEquals(2,v.attachments.size);assertEquals(true,v.attachments[0].lowered);assertEquals(false,v.attachments[0].turnedOn)
    }
    @Test fun exitAndUnknownRemainExplicit() {
        assertFalse(VehicleInfo.parse(JSONObject("""{"controlled":false}"""))!!.controlled)
        val v=VehicleInfo.parse(JSONObject("""{"id":"custom","kind":"dragon"}"""))!!
        assertEquals("unknown",v.kind);assertNull(v.axleCount);assertTrue(v.wheels.isEmpty())
    }
    @Test fun invalidGeometryAndCycleAreRejected() {
        assertNull(VehicleInfo.parse(JSONObject("""{"wheels":[{"x":101,"z":0}]}""")))
        assertNull(VehicleInfo.parse(JSONObject("""{"id":"a","attachments":[{"id":"a","parentId":"a"}]}""")))
    }
}
