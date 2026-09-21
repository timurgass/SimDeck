package dev.simdeck

import org.junit.Assert.assertEquals
import org.junit.Test

class GameLabelsTest {
    @Test fun selectedF1ProfileControlsTheVisibleGameNameWithoutTelemetry() {
        assertEquals("F1 24", gameDisplayName("f1-24", "старое значение"))
        assertEquals("F1 25", gameDisplayName("f1-25", "F1 24"))
        assertEquals("BeamNG.drive", gameDisplayName("beamng-default", "BeamNG.drive"))
    }

    @Test fun selectedF1ProfileControlsTheUdpHint() {
        assertEquals("2024", f1UdpFormat("f1-24"))
        assertEquals("2025", f1UdpFormat("f1-25"))
    }
}
