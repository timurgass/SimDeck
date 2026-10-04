# ETS2 telemetry plugin

Release package: `SimDeck-ETS2-Telemetry-0.8.5.zip`. Extract all files, close ETS2, then run `Install-ETS2.cmd` and select the **Euro Truck Simulator 2** game folder. The installer checks the DLL checksum, backs up an existing different DLL, and copies the tested Win64 plugin to `bin\win_x64\plugins`. Restart ETS2 and load a truck.

The package includes the MIT-licensed [RenCloud/scs-sdk-plugin](https://github.com/RenCloud/scs-sdk-plugin) version 1.12.1, `release_v_1_12_1/Win64/scs-telemetry.dll`, and its `LICENSE`. DLL SHA-256: `1D03DBC7A975E72203C60A7B9998021CEB8800B836BF28A131279979AD386CD4`. SimDeck reads the plugin's `Local\SCSTelemetry` shared memory; it does not change the game's key bindings or save data.

For a manual install, copy `scs-telemetry.dll` to `<ETS2 game folder>\bin\win_x64\plugins\` while the game is closed. On a Steam installation, the game folder is usually under a Steam library's `steamapps\common\Euro Truck Simulator 2`.

## American Truck Simulator

The same DLL supports ATS. Extract [SimDeck-ATS-Telemetry-0.9.16.zip](https://github.com/timurgass/SimDeck/releases/download/v0.9.16/SimDeck-ATS-Telemetry-0.9.16.zip), close ATS and run `Install-ATS.cmd`. It passes `-Game ats` to the shared installer, validates `bin/win_x64/amtrucks.exe` and installs into ATS only. [ATS setup and limitations](../../ATS-PROFILE.md).
