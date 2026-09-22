param(
    [string]$ControlsPath = "",
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

$controls = Get-Content -LiteralPath $ControlsPath -Raw | ConvertFrom-Json
if ($null -eq $controls.keyboardSettings) { throw "controls.json has no keyboardSettings section." }

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
    Binding "IgnitionOff" "O"
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
$targetKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($binding in $simDeck) {
    [void]$targetActions.Add($binding.actionType)
    $k = $binding.key
    [void]$targetKeys.Add("$($k.key)|$($k.bShift)|$($k.bCtrl)|$($k.bAlt)|$($k.bCmd)")
}

$preserved = @($controls.keyboardSettings.raceCommandButtonList | Where-Object {
    if ($targetActions.Contains([string]$_.actionType)) { return $false }
    if ($null -eq $_.key) { return $true }
    $signature = "$($_.key.key)|$($_.key.bShift)|$($_.key.bCtrl)|$($_.key.bAlt)|$($_.key.bCmd)"
    return -not $targetKeys.Contains($signature)
})

$backup = Join-Path (Split-Path -Parent $ControlsPath) ("controls.before-simdeck-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".json")
Copy-Item -LiteralPath $ControlsPath -Destination $backup
$controls.keyboardSettings.raceCommandButtonList = @($preserved) + @($simDeck)
$json = $controls | ConvertTo-Json -Depth 100
[IO.File]::WriteAllText($ControlsPath, $json, [Text.UTF8Encoding]::new($false))

$check = Get-Content -LiteralPath $ControlsPath -Raw | ConvertFrom-Json
$installed = @($check.keyboardSettings.raceCommandButtonList | Where-Object { $targetActions.Contains([string]$_.actionType) }).Count
if ($installed -ne $simDeck.Count) { throw "Preset verification failed: expected $($simDeck.Count), found $installed. Backup: $backup" }

Write-Host "SimDeck ACC preset installed: $installed bindings." -ForegroundColor Green
Write-Host "Backup: $backup"
Write-Host "Start ACC and keep the SimDeck profile selected in Companion."
