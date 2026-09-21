# SimDeck wire protocol 1 — implemented subset

Transport: UTF-8 JSON over WSS. Pairing: HTTPS POST `/pair`, `{ "code": "123456", "name": "Tablet" }`; response `{ "token": "…", "protocolMajor": 1 }`. Pairing is closed until opened on the PC, expires in 120 seconds and closes after success or five failed attempts.

The Android user compares the complete SHA-256 certificate fingerprint with Companion before pairing. Discovery TXT data is not authenticated. TLS uses a pinned certificate; changing the certificate requires pairing again. Do not log PINs, bearer tokens or authorization headers.

Connect `/ws` with `Authorization: Bearer <token>`. One controlling WebSocket is permitted. Server sends `hello` with a new `sessionId`, `profileId` (`beamng-default`), `profileRevision` (1) and capabilities. All subsequent client messages include `protocolMajor: 1` and that `sessionId`.

Client messages:

- `control.invoke`: `commandId`, `profileId`, `profileRevision`, `actionId`, `phase`, `pressId`. Actions: `lights`, `reset` use `press`; `horn` uses `down` and `up`. `ignition` uses `press` followed by a separate `down`/`up` hold, with server-enforced `tapThenHold` semantics. IDs <=80 characters. Commands deduplicated within the session; reconnect creates a new session and never replays pending input.
- `input.renew`: `pressIds` array of active holds, every 100 ms. Hold lease is 500 ms. Expired IDs cannot be revived.
- `input.releaseAll`: releases the session's synthetic inputs.
- `ping`: server responds `pong`.

Server `control.ack` includes `success` and `code`; `injected` means Windows accepted synthetic input, not that the game changed state. Demo mode and unfocused/disabled input reject injection.

As of 0.1.1, `hello.capabilities.gestures.ignition` is `tapThenHold`. Server `input.state` includes `ignitionReady`, initially false. A successfully completed 100-ms ignition pulse arms one subsequent hold. Attempting a hold before that returns `ignition_tap_required`. Starting the hold consumes the permission. Releasing only emits key-up, never a second press. Reset, focus loss, session changes, and release-all disarm it. The client ignores a first contact held for >=500 ms; after receiving `ignitionReady: true`, it forwards the entire next contact's duration without adding a long-press delay. Thus the game receives a real held key, rather than a delayed short pulse at finger release. This flag is input-sequence state, not game ignition telemetry.

Server emits full `telemetry.snapshot` messages at up to 30 Hz. `data: null` means there is no sample; `ageMs` is source age at send time. The client adds local elapsed time since receiving the sample. Data is stale after 500 ms. `source: demo` always labels generated values. Fields have fixed units: m/s, RPM, integer gear (-1 reverse, 0 neutral), fuel/pedals as fractions 0..1. Null `maxRpm` and `fuelLiters` must not be interpreted as zero. UI's default 8000 RPM scale is explicitly labelled a display setting.

WebSocket input messages <=8 KiB; HTTP request body <=4 KiB. A bounded acknowledgement queue precedes the latest telemetry snapshot. No telemetry history queue. Socket writes have a two-second timeout.

Discovery: `_simdeck._tcp`, TXT `version=1`, `fingerprint=<64 lowercase hex digits>`; port currently 9443, configurable in Companion settings JSON. OutGauge is read only from loopback UDP, default 4444. This implementation does not expose UDP telemetry reception to the LAN.

Known alpha gaps: no independent process watchdog for forced Companion termination, no QR, no negotiated minor versions, no editor/profile synchronisation, no HID. Changing bindings revokes pairing so an old client cannot unknowingly run changed controls.

## Extended BeamNG UDP (SimDeck 0.1.2)

48 bytes, little-endian: ASCII `SMD1` at 0, uint32 version=1 at 4, float32 speedMps at 8, float32 rpm at 12, int32 gear at 16, uint32 mode at 20 (0 unknown, 1 arcade, 2 realistic), float32 fuelFraction/throttle/brake/clutch at 24/28/32/36, float32 maxRpm at 40, int32 maxGear at 44. Unknown positive limits use zero on UDP and null on WSS. Packets with invalid size, signature, version, non-finite numbers or out-of-range values are rejected.

WSS data adds nullable `gearboxMode` (`arcade`/`realistic`), `maxGear`, and derived `gearDisplay`. Existing numeric `gear` remains the physical gear index. Android derives its display from `gear` and `gearboxMode`. Fresh extended packets take priority over OutGauge for 500 ms. This preserves older clients and avoids rapid mode loss if both sources run.

## Profile revision 2 (SimDeck 0.2.0)

The authenticated `hello` includes `profileName`, `profileRevision: 2`, and `controls` (22 records with `id`, `page`, `label`, `description`, `key`, `gesture`). Android renders the server catalog, groups controls by page, and echoes the received revision on invoke. Physical key selection remains server-side: the client sends action IDs, never scan codes. The four-button fallback remains available when an older Companion does not send a catalog. `input_busy` rejects a chord overlapping an existing hold or another action overlapping a chord. Chords retain modifiers through their 100 ms pulse and release keys in reverse order, with retry ownership retained after release failure.

## State telemetry v2 and profile revision 3 (0.2.1)

New UDP packets are 60 bytes: magic `SMD2`, version=2; offsets 8–47 match v1. Offset 48: uint32 stateKnown mask; offset 52: uint32 stateActive mask; offset 56: int32 headlights (-1 unknown, 0 off, 1 low beam, 2 high beam). Bits 0–9 map to hazards, leftSignal, rightSignal, fogLights, fourWheelDrive, range, differentials, couplers, ignition, lightbar. Only known bits appear in WSS `actionStates`; absence means unknown, not false. `headlights` is nullable. Active bits without a matching known bit are rejected. V1 packets continue to supply gauges without invented states.

The UI derives appearance only from fresh telemetry, never by optimistic local toggling. Left/right use signal input state rather than instantaneous blinking lamps. Unknown or stale state clears latched appearance. Gearbox and ESC multi-mode controls are not represented as binary power switches.

Profile revision 3 removes shiftUp, shiftDown, parkingBrake, handbrakeHold and adds recoverAlt (hold Ctrl+Insert), recoverRoad (Alt+T), loadHome (Home), saveHome (Ctrl+Home). Recovery commands invalidate ignition sequencing; saveHome does not.

## F1 preset synchronization (0.3.1)

`controls` supports up to 96 actions. Each record may include `group`, an optional string defaulting to empty, used for a secondary tab within `page`. Commands and key ownership remain unchanged. F1 provides 69 actions in Control Scheme, MFD and Menu Controls. Existing clients capped at 64 actions must be updated alongside Companion. F1 profile migration is tracked independently by `F1PresetVersion`; it bumps the saved profile revision once and preserves custom actions, pairing and BeamNG. Quick adjustment headings only expand local UI; their minus/plus buttons send existing action IDs. Changing either level of navigation releases active holds.
