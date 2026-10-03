# ts-map integration

MIT source from [dariowouters/ts-map](https://github.com/dariowouters/ts-map), pinned at `0bf6bed` (the full revision is recorded in UPSTREAM.txt). Copyright and permission notices are in LICENSE.

SimDeck adaptations: .NET 10 Windows library project; sequential sector reading to reduce peak memory; exact stream reads; Windows platform annotation; disabled file logging and logging queue. The importer runs in a separate Companion process and exports road curves, junction connections and city labels. The original drawing/UI code is not used by the tablet.

`libs/libdeflate.dll` comes from the same pinned ts-map tree. Its compression library is the [NVIDIA/libdeflate fork](https://github.com/NVIDIA/libdeflate/tree/3bb5c6924b32a91e6e6a8f54ba00a21f037a8db5); its license is preserved in libs/COPYING. Newtonsoft.Json is restored through NuGet under its MIT license.

**No ETS2 game archives or extracted road maps belong in this repository or release.** Each PC builds its own private cache from its installed, unmodified game and map DLC. SimDeck's importer currently permits ETS2 1.59–1.61; extraction and a live position were checked against 1.61.1.1. Other versions and map mods require separate compatibility checks.
