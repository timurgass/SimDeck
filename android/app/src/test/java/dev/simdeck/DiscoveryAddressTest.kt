package dev.simdeck

import org.junit.Assert.assertEquals
import org.junit.Test

class DiscoveryAddressTest {
    // Synthetic RFC1918 addresses; these are not any user's LAN settings.
    @Test fun lanWinsOverVpnForSameCompanion() {
        val lan = Computer("SimDeck-PC", "10.255.255.10", 9443, "same-fingerprint")
        val vpn = lan.copy(host = "198.51.100.5")
        assertEquals(lan, DiscoveryAddress.prefer(vpn, lan))
        assertEquals(lan, DiscoveryAddress.prefer(lan, vpn))
    }

    @Test fun changedLanAddressAndNewFingerprintCanReplaceOldEntry() {
        val previous = Computer("SimDeck-PC", "10.255.255.10", 9443, "old")
        assertEquals("10.255.255.11", DiscoveryAddress.prefer(previous, previous.copy(host = "10.255.255.11")).host)
        assertEquals("new", DiscoveryAddress.prefer(previous, previous.copy(fingerprint = "new")).fingerprint)
    }
}
