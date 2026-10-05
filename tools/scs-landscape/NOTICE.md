# SCS landscape extractor

This directory is a separate executable licensed under GPL-2.0-only. It uses
TruckLib 0.5.1 (GPL-2.0) by sk-zk and the existing ts-map archive reader (MIT).
Companion launches it as a separate process and reads its JSON output; it does
not reference or link TruckLib. Game files and extracted map data are not shipped.

Source: https://github.com/timurgass/SimDeck/tree/main/tools/scs-landscape
TruckLib source: https://github.com/sk-zk/TruckLib/tree/bd745344fc52d3b2d70ce9ac7c88d61b99934805
TruckLib dependencies: https://github.com/sk-zk/TruckLib.Core,
https://github.com/sk-zk/TruckLib.Models, https://github.com/sk-zk/TruckLib.HashFs

Build with .NET 10: `dotnet publish tools/scs-landscape -c Release -r win-x64
--self-contained true -o artifacts/Companion/scs-landscape`.
Package this directory next to SimDeck.exe; the Windows distribution includes
the extractor runtime as well. License text accompanies the executable.

`source/` in the Windows archive contains this tool, the MIT archive reader,
build configuration and corresponding source archives of all five TruckLib
packages at the exact commits listed in `source/dependencies.json`. These sources
are packaged by `package-source.py` after publish. They contain no game assets.

## Map appearance (0.9.23)

`LandscapeRaster.cs` creates original procedural canopy, crop, paving, sand, rock and water textures. It downloads no imagery and redistributes no extracted game textures. Game files provide area boundaries and material categories; fine texture details are illustrative. Texture placement is anchored in game-world coordinates. The renderer is GPL-2.0-only, like this separate extractor.
