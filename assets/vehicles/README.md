# Shared vehicle artwork

These are production PNG assets embedded in both the Android APK and Windows Companion, served locally to Safari. Generated with the built-in imagegen tool, with transparent backgrounds. No game screenshots, licensed manufacturer logos, remote asset calls, or personal data are included.

- `farm.png` — 1536×1024, side-view farming class atlas: tractor, combine, truck, wheel loader, telehandler, forestry machine, sprayer, cultivator.
- `equipment.png` — 2172×724, tracked tractor, agricultural trailer and separate tyre.
- `road.png` — 1254×1254, top-view technical body/chassis atlas: car, SUV, pickup, van, bus, tractor unit, Formula cars and translucent semitrailer.
- `gt.png` — 1024×1536, closed GT racing coupe.

Source rectangles are selected at runtime by Compose `drawImage` and nested SVG viewBoxes. The PNG files themselves are unchanged tool outputs. Both clients share the same rectangles; app code places actual available wheel geometry, labels and state overlays. Generated coloured trim is illustration detail, **not** a measured damage or toggle indicator.

## Generation briefs

The farming atlas brief specified isolated orthographic side-view detailed green/grey machines in a 4×2 grid, no UI or text, with a separate rear cultivator that can be animated. A follow-up edit requested removal of coloured state glows without changing machinery.

The road atlas brief specified isolated orthographic top-view cutaway bodies in a 3×3 grid, no UI or text. Road bodies have no baked wheel count; a separate tyre is positioned from telemetry. The Formula car has four fixed tyres. The final slot is a transparent connected semitrailer.

The equipment brief specified three isolated sprites on one horizontal transparent canvas: tracked farm tractor, agricultural grain trailer and detailed top-view rubber tyre.

Final GT prompt:

> Create a production game dashboard vehicle sprite, transparent background. A single detailed top-down orthographic GT endurance racing coupe with CLOSED ROOF, windscreen, cabin roll cage visible through subtly transparent black body panels, front hood engine mechanical detail, dark graphite chassis, realistic FOUR wheels, large rear racing wing, subtle amber pinstripes. Front of car at TOP, rear at BOTTOM. Entire car fully contained, large centered subject almost fills canvas height, no perspective, no labels, no text, no HUD, no background, no green/red halos or damage indicators. A polished technical automotive illustration with realistic metal, rubber tread, motorsport mechanical parts, matching black charcoal dashboard artwork. This is a GT closed coupe, clearly NOT an open-wheel Formula car. Tall portrait sprite, straight perfectly vertical car centerline.

These are class illustrations, not exact models or a claim of model-specific geometry. See [live data coverage](../../docs/AUTO-VEHICLE.md).

## Harvest atlas (0.9.5)

`harvest.png` — built-in imagegen, transparent RGBA 1254 × 1254. Separate bare combine, detached grain header, cultivator and mouldboard plough; no generic implement is assumed to be a plough. Runtime source rectangles are declared in both clients. Brief: detailed left-facing side elevation matching farm.png, four separate cells, combine without any header or implement, detached header only, cultivator only, plough only, no logos/text/background. Full generation prompt in docs/UI-VEHICLES-095.md.
