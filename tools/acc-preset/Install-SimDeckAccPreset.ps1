param(
    [string]$ControlsPath = "",
    [string]$SettingsPath = "",
    [switch]$AllowRunningGame
)

$ErrorActionPreference = "Stop"

if (-not $AllowRunningGame -and (Get-Process -Name "AC2-Win64-Shipping" -ErrorAction SilentlyContinue)) {
    throw "Close Assetto Corsa Competizione before installing the preset. The game overwrites controls.json when it exits."
}

if ([string]::IsNullOrWhiteSpace($ControlsPath)) {
    $candidates = @(
        (Join-Path ([Environment]::GetFolderPath("MyDocuments")) "Assetto Corsa Competizione\Config\controls.json"),
        (Join-Path $env:USERPROFILE "Documents\Assetto Corsa Competizione\Config\controls.json")
    ) | Select-Object -Unique
    $ControlsPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($ControlsPath) -or -not (Test-Path -LiteralPath $ControlsPath)) {
    throw "ACC controls.json was not found. Start ACC once, save a controls preset, close the game, and run this installer again."
}

if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $SettingsPath = Join-Path (Split-Path -Parent $ControlsPath) "saveSettings.json"
}

if (-not (Test-Path -LiteralPath $SettingsPath)) {
    throw "ACC saveSettings.json was not found next to controls.json. Start ACC once, close the game, and run this installer again."
}

$controls = Get-Content -LiteralPath $ControlsPath -Raw | ConvertFrom-Json
if ($null -eq $controls.keyboardSettings) { throw "controls.json has no keyboardSettings section." }
$settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
if ($null -eq $settings.assists -or $null -eq $settings.assists.PSObject.Properties["autoEngineSwitch"]) {
    throw "saveSettings.json has no assists.autoEngineSwitch setting."
}

function Binding([string]$action, [string]$key, [bool]$shift = $false, [bool]$ctrl = $false, [bool]$alt = $false) {
    [pscustomobject][ordered]@{
        actionType = $action
        key = [pscustomobject][ordered]@{
            key = $key
            bShift = $shift
            bCtrl = $ctrl
            bAlt = $alt
            bCmd = $false
        }
    }
}

$simDeck = @(
    Binding "PitLimiter" "P"
    Binding "IgnitionSequenceOn" "I"
    # ACC 1.10.4 does not expose IgnitionOff as a keyboard binding. SimDeck uses
    # this supported shortcut to open Electronics MFD and select OFF there.
    Binding "SetMfdElectronics" "F2"
    Binding "Starter" "S"
    Binding "CycleCarLightStages" "L"
    Binding "EnableFlashingLights" "H"
    Binding "CycleWiper" "W"
    Binding "EnableRainLights" "R"
    Binding "CycleCamera" "C"
    Binding "LookLeft" "Q"
    Binding "LookRight" "E"
    Binding "LookBack" "B"
    Binding "Pause" "Escape"
    Binding "IncreaseTC" "T" $false $true
    Binding "DecreaseTC" "T" $false $false $true
    Binding "IncreaseTCCut" "Two" $false $true
    Binding "DecreaseTCCut" "Two" $false $false $true
    Binding "IncreaseABS" "A" $false $true
    Binding "DecreaseABS" "A" $false $false $true
    Binding "IncreaseEngineMap" "M" $false $true
    Binding "DecreaseEngineMap" "M" $false $false $true
    Binding "IncreaseBrakeBias" "B" $false $true
    Binding "DecreaseBrakeBias" "B" $false $false $true
    Binding "Up" "Up"
    Binding "Down" "Down"
    Binding "Left" "Left"
    Binding "Right" "Right"
    Binding "Forward" "Enter"
    Binding "Backward" "BackSpace"
    Binding "CycleHudMfd" "Insert"
    Binding "DisplayPageUp" "PageUp"
    Binding "DisplayPageDown" "PageDown"
    Binding "SetMfdPitstop" "F1"
)

$targetActions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$retiredActions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
[void]$retiredActions.Add("IgnitionOff")
$targetKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($binding in $simDeck) {
    [void]$targetActions.Add($binding.actionType)
    $k = $binding.key
    [void]$targetKeys.Add("$($k.key)|$($k.bShift)|$($k.bCtrl)|$($k.bAlt)|$($k.bCmd)")
}

$preserved = @($controls.keyboardSettings.raceCommandButtonList | Where-Object {
    if ($retiredActions.Contains([string]$_.actionType)) { return $false }
    if ($targetActions.Contains([string]$_.actionType)) { return $false }
    if ($null -eq $_.key) { return $true }
    $signature = "$($_.key.key)|$($_.key.bShift)|$($_.key.bCtrl)|$($_.key.bAlt)|$($_.key.bCmd)"
    return -not $targetKeys.Contains($signature)
})

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backup = Join-Path (Split-Path -Parent $ControlsPath) ("controls.before-simdeck-" + $stamp + ".json")
$settingsBackup = Join-Path (Split-Path -Parent $SettingsPath) ("saveSettings.before-simdeck-" + $stamp + ".json")
Copy-Item -LiteralPath $ControlsPath -Destination $backup
Copy-Item -LiteralPath $SettingsPath -Destination $settingsBackup
$controls.keyboardSettings.raceCommandButtonList = @($preserved) + @($simDeck)
$settings.assists.autoEngineSwitch = $false
$json = $controls | ConvertTo-Json -Depth 100
[IO.File]::WriteAllText($ControlsPath, $json, [Text.UTF8Encoding]::new($false))
$settingsJson = $settings | ConvertTo-Json -Depth 100
[IO.File]::WriteAllText($SettingsPath, $settingsJson, [Text.UTF8Encoding]::new($false))

$check = Get-Content -LiteralPath $ControlsPath -Raw | ConvertFrom-Json
$installed = @($check.keyboardSettings.raceCommandButtonList | Where-Object { $targetActions.Contains([string]$_.actionType) }).Count
if ($installed -ne $simDeck.Count) { throw "Preset verification failed: expected $($simDeck.Count), found $installed. Backup: $backup" }
$settingsCheck = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
if ($settingsCheck.assists.autoEngineSwitch -ne $false) { throw "Preset verification failed: ACC automatic engine control is still enabled. Backup: $settingsBackup" }

Write-Host "SimDeck ACC preset installed: $installed bindings." -ForegroundColor Green
Write-Host "Manual ignition control enabled in ACC." -ForegroundColor Green
Write-Host "Controls backup: $backup"
Write-Host "Settings backup: $settingsBackup"
Write-Host "Start ACC and keep the SimDeck profile selected in Companion."
