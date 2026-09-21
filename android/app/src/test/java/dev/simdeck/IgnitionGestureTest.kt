package dev.simdeck

import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.*
import kotlinx.coroutines.ExperimentalCoroutinesApi
import org.junit.Assert.*
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class IgnitionGestureTest {
    @Test fun shortFirstTouchSendsOnlyTap() = runTest {
        val events = mutableListOf<String>()
        ignitionGesture(false, { delay(100); true }, { events += "tap" }, { events += "down"; "p" }, { events += "up" }, { events += "blocked" })
        assertEquals(listOf("tap"), events)
    }
    @Test fun holdingFirstCannotStartOrToggleIgnitionOnRelease() = runTest {
        val events = mutableListOf<String>()
        val releasedAt = currentTime + 1500
        ignitionGesture(false, { delay((releasedAt - currentTime).coerceAtLeast(0)); true }, { events += "tap" }, { events += "down"; "p" }, { events += "up" }, { events += "blocked" })
        assertEquals(listOf("blocked"), events)
    }
    @Test fun cancelledFirstTouchDoesNothing() = runTest {
        val events = mutableListOf<String>()
        ignitionGesture(false, { delay(100); false }, { events += "tap" }, { events += "down"; "p" }, { events += "up" }, { events += "blocked" })
        assertTrue(events.isEmpty())
    }
    @Test fun armedHoldForwardsExactContactDurationWithoutExtraTap() = runTest {
        val events = mutableListOf<String>()
        ignitionGesture(true, { delay(1700); true }, { events += "tap" }, { events += "down@$currentTime"; "p" }, { events += "up@$currentTime" }, { events += "blocked" })
        assertEquals(listOf("down@0", "up@1700"), events)
    }
    @Test fun cancelledArmedTouchStillReleases() = runTest {
        val events = mutableListOf<String>()
        ignitionGesture(true, { delay(100); false }, { events += "tap" }, { events += "down"; "p" }, { events += "up" }, { events += "blocked" })
        assertEquals(listOf("down", "up"), events)
    }
    @Test fun disposingGestureReleasesStarter() = runTest {
        val events = mutableListOf<String>()
        val task = launch {
            ignitionGesture(true, { delay(5000); true }, { events += "tap" }, { events += "down"; "p" }, { events += "up" }, { events += "blocked" })
        }
        runCurrent(); advanceTimeBy(100); task.cancelAndJoin()
        assertEquals(listOf("down", "up"), events)
    }
}
