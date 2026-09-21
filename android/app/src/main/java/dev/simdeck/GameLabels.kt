package dev.simdeck

internal fun gameDisplayName(profileId: String, profileName: String): String = when (profileId) {
    "f1-24" -> "F1 24"
    "f1-25" -> "F1 25"
    else -> profileName
}

internal fun f1UdpFormat(profileId: String): String = if (profileId == "f1-25") "2025" else "2024"
