package dev.simdeck

import org.junit.Assert.*
import org.junit.Test

class ControlAvailabilityTest {
    @Test fun keyboardActionsRemainAvailableWithoutGameTelemetryInEveryProfile() {
        for(id in listOf("f1-24","f1-25","beamng-default","acc","ams2","ets2","ats","snowrunner","fs25")) {
            val state=DeckState(profileId=id,connected=true,inputAvailability="ready",stale=true)
            assertTrue(id,controlsAvailable(state))
            assertFalse(controlsAvailable(state.copy(connected=false)))
            assertFalse(controlsAvailable(state.copy(inputAvailability="unfocused")))
            assertFalse(controlsAvailable(state.copy(inputAvailability="disabled")))
            assertFalse(controlsAvailable(state.copy(demo=true)))
            assertFalse(controlsAvailable(state.copy(menuBusy=true)))
        }
    }
}
