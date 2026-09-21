package dev.simdeck
import org.json.JSONObject
import org.junit.Assert.*
import org.junit.Test

class ProtocolTest {
    @Test fun transportUsesApplicationHeartbeatWithoutCompetingPingDeadline() {
        val client = PinnedTls.client("a".repeat(64))
        assertEquals(0, client.pingIntervalMillis)
        client.dispatcher.executorService.shutdown()
    }
    @Test fun fullF1CatalogHasNoTruncationAndPreservesNavigationGroups() {
        val json = javaClass.classLoader!!.getResource("f1-preset.json")!!.readText()
        val controls = Protocol.controls(JSONObject(json))
        assertEquals(69, controls.size)
        assertEquals(setOf("Control Scheme", "MFD", "Menu Controls"), controls.map { it.page }.toSet())
        assertEquals("B", controls.single { it.id == "mfd" }.key)
        assertEquals("P", controls.single { it.id == "pitLimiter" }.key)
        assertEquals("Обзор", controls.single { it.id == "lookLeft" }.group)
        assertEquals("hold", controls.single { it.id == "lookLeft" }.gesture)
        assertFalse(controls.any { it.id in listOf("drs", "ers") || "NumPad" in it.key })
    }
    @Test(expected = IllegalArgumentException::class) fun oversizedProfilesRejected() {
        val list = org.json.JSONArray()
        repeat(97) { list.put(JSONObject().put("id", "action$it").put("page", "P").put("label", "L").put("description", "").put("key", "F1").put("gesture", "press")) }
        Protocol.controls(JSONObject().put("controls", list))
    }
    @Test fun f1TelemetryDoesNotWaitForFuelStatusPacket() {
        val t = Protocol.telemetry(JSONObject("""{"data":{"speedMps":75,"rpm":11500,"gear":7,"fuelFraction":null,"actionStates":{"drs":true,"ers":false}}}"""))!!
        assertNull(t.fuelFraction); assertEquals("7", Protocol.gear(t.gear)); assertEquals(true, Protocol.feedback("drs", t, false).active)
        assertEquals(false, Protocol.feedback("ers", t, false).active)
        assertNull(Protocol.feedback("drs", t, true).active)
    }
    @Test fun customButtonKeepsItsPageKeyAndHoldGesture() {
        val a = Protocol.controls(JSONObject("""{"controls":[{"id":"custom-1","page":"Мои кнопки","label":"ТЕСТ","description":"Удерживать","key":"Ctrl+F12","gesture":"hold"}]}"""))[0]
        assertEquals("Мои кнопки", a.page); assertEquals("Ctrl+F12", a.key); assertEquals("hold", a.gesture)
    }
    @Test fun feedbackTracksLiveHeadlightChanges() {
        val t = Telemetry(0.0, 900.0, 0, .5, 7000.0, headlights = 1)
        assertEquals(1, Protocol.feedback("lights", t, false).headlights)
        assertEquals("Ближний свет", Protocol.feedback("lights", t, false).description)
        assertEquals(2, Protocol.feedback("lights", t.copy(headlights = 2), false).headlights)
        assertEquals(false, Protocol.feedback("lights", t.copy(headlights = 0), false).active)
    }
    @Test fun knownToggleStaysLatchedUntilTelemetryTurnsItOff() {
        val t = Telemetry(0.0, 900.0, 0, .5, null, actionStates = mapOf("hazards" to true, "fogLights" to false))
        assertEquals(true, Protocol.feedback("hazards", t, false).active)
        assertEquals(false, Protocol.feedback("fogLights", t, false).active)
        assertNull(Protocol.feedback("fourWheelDrive", t, false).active)
        assertEquals(false, Protocol.feedback("hazards", t.copy(actionStates = mapOf("hazards" to false)), false).active)
    }
    @Test fun staleAndLegacyDataClearConfirmedAppearance() {
        val t = Telemetry(0.0, 900.0, 0, .5, null, headlights = 2, actionStates = mapOf("hazards" to true))
        assertNull(Protocol.feedback("lights", t, true).headlights)
        assertNull(Protocol.feedback("hazards", t, true).active)
        assertNull(Protocol.feedback("hazards", t.copy(actionStates = emptyMap()), false).active)
    }
    @Test fun statePayloadDoesNotConvertUnknownToFalse() {
        val t = Protocol.telemetry(JSONObject("""{"data":{"speedMps":0,"rpm":1000,"gear":0,"fuelFraction":1,"headlights":2,"actionStates":{"hazards":true,"fogLights":false,"range":null}}}"""))!!
        assertEquals(2, t.headlights); assertEquals(true, t.actionStates["hazards"]); assertEquals(false, t.actionStates["fogLights"]); assertFalse(t.actionStates.containsKey("range"))
    }
    @Test fun profileUsesCompanionCatalogAndGestures() {
        val a = Protocol.controls(JSONObject("""{"controls":[{"id":"esc","page":"Системы","label":"ESC / TCS","description":"Следующий режим","key":"Ctrl+Q","gesture":"press"},{"id":"recover","page":"Езда","label":"Возврат","description":"Удерживайте","key":"Insert","gesture":"hold"}]}"""))
        assertEquals(2, a.size); assertEquals("Ctrl+Q", a[0].key); assertEquals("hold", a[1].gesture)
    }
    @Test(expected = IllegalArgumentException::class) fun duplicateProfileActionsRejected() {
        val root = JSONObject("""{"controls":[{"id":"esc","page":"A","label":"ESC","description":"Mode","key":"Q","gesture":"press"}]}""")
        root.getJSONArray("controls").put(root.getJSONArray("controls").getJSONObject(0))
        Protocol.controls(root)
    }
    @Test fun olderCompanionCanUseBasicFallback() { assertTrue(Protocol.controls(JSONObject("{}")).isEmpty()) }
    @Test fun arcadeHasOnlyDriveNeutralReverse() {
        for (g in 1..24) assertEquals("D", Protocol.gear(g, "arcade"))
        assertEquals("N", Protocol.gear(0, "arcade")); assertEquals("R", Protocol.gear(-2, "arcade"))
    }
    @Test fun realisticShowsActualGearWithoutFixedGearCount() {
        for (g in 1..24) assertEquals(g.toString(), Protocol.gear(g, "realistic"))
        assertEquals("N", Protocol.gear(0, "realistic")); assertEquals("R", Protocol.gear(-1, "realistic"))
    }
    @Test fun gearboxModeComesFromEachSnapshot() {
        val root = JSONObject("""{"data":{"speedMps":12,"rpm":2345,"gear":8,"fuelFraction":0.5,"maxRpm":7000,"gearboxMode":"arcade","maxGear":8}}""")
        val arcade = Protocol.telemetry(root)!!
        assertEquals("D", Protocol.gear(arcade.gear, arcade.gearboxMode)); assertEquals(8, arcade.maxGear)
        root.getJSONObject("data").put("gearboxMode", "realistic")
        val realistic = Protocol.telemetry(root)!!
        assertEquals("8", Protocol.gear(realistic.gear, realistic.gearboxMode))
    }
    @Test fun sharedCSharpKotlinFixture() {
        val raw = javaClass.classLoader!!.getResourceAsStream("telemetry-v1.json")!!.bufferedReader().use { it.readText() }
        val t = Protocol.telemetry(JSONObject(raw))!!
        assertEquals(40.0, t.speedMps, .00001); assertEquals(4, t.gear); assertEquals(6234.0, t.rpm, .00001); assertNull(t.maxRpm)
    }
    @Test fun missingDataIsNotZero() { assertNull(Protocol.telemetry(JSONObject("{\"data\":null}"))) }
    @Test fun outGaugeFuelStaysFraction() {
        val t = Protocol.telemetry(JSONObject("""{"data":{"speedMps":10.0,"rpm":1200,"gear":-1,"fuelFraction":0.5,"maxRpm":null}}"""))!!
        assertEquals(0.5, t.fuelFraction!!, .0001); assertNull(t.maxRpm); assertEquals("R", Protocol.gear(t.gear))
    }
    @Test fun invalidFrameRejected() { assertNull(Protocol.telemetry(JSONObject("""{"data":{"speedMps":-1,"rpm":1200,"gear":1,"fuelFraction":0.5}}"""))) }
    @Test fun neutralIsNotReverse() { assertEquals("N", Protocol.gear(0)); assertEquals("1", Protocol.gear(1)) }
}
