package dev.simdeck

import java.net.InetAddress

internal object DiscoveryAddress {
    fun prefer(previous: Computer?, candidate: Computer): Computer {
        if (previous == null || previous.fingerprint != candidate.fingerprint) return candidate
        return if (score(candidate.host) >= score(previous.host)) candidate else previous
    }

    private fun score(host: String): Int = runCatching {
        val address = InetAddress.getByName(host)
        when {
            address.isLoopbackAddress || address.isLinkLocalAddress -> 0
            address.isSiteLocalAddress -> 3
            address.address.size == 4 -> 2
            else -> 1
        }
    }.getOrDefault(0)
}
