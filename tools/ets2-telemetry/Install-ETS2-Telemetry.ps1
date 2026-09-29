param([string]$GameDirectory)

$ErrorActionPreference = 'Stop'
$expectedHash = '1D03DBC7A975E72203C60A7B9998021CEB8800B836BF28A131279979AD386CD4'
$source = Join-Path $PSScriptRoot 'scs-telemetry.dll'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw 'scs-telemetry.dll is missing. Extract the entire SimDeck ETS2 Telemetry ZIP first.'
}
if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'scs-telemetry.dll checksum does not match the tested release.'
}

if (-not $GameDirectory) {
    Add-Type -AssemblyName System.Windows.Forms
    $picker = [System.Windows.Forms.FolderBrowserDialog]::new()
    $picker.Description = 'Select the Euro Truck Simulator 2 game folder (contains bin)'
    if ($picker.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        throw 'No game folder selected.'
    }
    $GameDirectory = $picker.SelectedPath
}
$gameDirectoryResolved = (Resolve-Path -LiteralPath $GameDirectory).Path
$gameExe = Join-Path $gameDirectoryResolved 'bin\win_x64\eurotrucks2.exe'
if (-not (Test-Path -LiteralPath $gameExe -PathType Leaf)) {
    throw "Euro Truck Simulator 2 executable not found: $gameExe"
}
if (Get-Process -Name eurotrucks2 -ErrorAction SilentlyContinue | Where-Object {
    -not $_.Path -or [string]::Equals($_.Path, $gameExe, [StringComparison]::OrdinalIgnoreCase)
}) {
    throw 'Close Euro Truck Simulator 2 before installing the telemetry plugin.'
}

$plugins = Join-Path $gameDirectoryResolved 'bin\win_x64\plugins'
New-Item -ItemType Directory -Path $plugins -Force | Out-Null
$target = Join-Path $plugins 'scs-telemetry.dll'
if (Test-Path -LiteralPath $target -PathType Leaf) {
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $expectedHash) {
        Write-Host "Already installed: $target"
        exit 0
    }
    $backup = "$target.backup-$(Get-Date -Format yyyyMMdd-HHmmss)"
    Copy-Item -LiteralPath $target -Destination $backup -ErrorAction Stop
    Write-Host "Previous plugin backed up: $backup"
}
Copy-Item -LiteralPath $source -Destination $target -Force -ErrorAction Stop
if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expectedHash) {
    throw "Installed plugin checksum mismatch: $target"
}
Write-Host "Installed: $target"
Write-Host 'Start ETS2, load a truck, and select the ETS2 profile in SimDeck Companion.'
