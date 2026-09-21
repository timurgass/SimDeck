package dev.simdeck

import kotlinx.coroutines.withTimeoutOrNull

/** First contact must be a short, completed tap. Only a separately armed contact holds V. */
suspend fun ignitionGesture(
    ready: Boolean,
    awaitRelease: suspend () -> Boolean,
    tap: () -> Unit,
    startHold: () -> String?,
    endHold: (String?) -> Unit,
    blocked: () -> Unit
) {
    var heldId: String? = null
    try {
        if (ready) {
            // Forward duration directly: BeamNG itself distinguishes a short V from starter hold.
            heldId = startHold()
            awaitRelease()
        } else {
            when (withTimeoutOrNull(500) { awaitRelease() }) {
                true -> tap()
                false -> Unit // cancelled touch/scroll must not enable ignition
                null -> { blocked(); awaitRelease() } // holding first cannot become a tap on release
            }
        }
    } finally {
        if (heldId != null) endHold(heldId)
    }
}
