param([string]$Directory = 'artifacts/scs-installer-test')
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$test = [IO.Path]::GetFullPath((Join-Path $repo $Directory))
if (-not $test.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test directory must be inside the repository.' }
New-Item -ItemType Directory -Force -Path $test | Out-Null
$manifest = Get-Content (Join-Path $repo 'tools/ets2-preset/preset-actions.json') -Raw | ConvertFrom-Json
foreach ($game in @('ets2','ats')) {
    $path = Join-Path $test ($game + '/controls.sii')
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    $lines = @('SiiNunit','{','input_config : test {')
    $i = 0
    foreach ($row in $manifest) {
        $lines += (' config_lines[{0}]: "mix {1} `keyboard.f12?0 | joy.b1?0 | semantical.{1}?0`"' -f $i++, $row.gameAction)
    }
    $lines += ' config_lines[100]: "mix custom `keyboard.f11?0 | semantical.custom?0`"','}','}'
    [IO.File]::WriteAllText($path, ($lines -join "`r`n"), [Text.UTF8Encoding]::new($false))
    $before = (Get-FileHash $path).Hash
    & (Join-Path $repo 'tools/ets2-preset/Install-ETS2-Preset.ps1') -Game $game -ControlsPath $path -Preview | Out-Null
    if ((Get-FileHash $path).Hash -ne $before) { throw "$game preview modified controls" }
    & (Join-Path $repo 'tools/ets2-preset/Install-ETS2-Preset.ps1') -Game $game -ControlsPath $path | Out-Null
    $text = [IO.File]::ReadAllText($path)
    foreach ($row in $manifest) {
        if (-not $text.Contains(('mix {0} `keyboard.{1}?0 | joy.b1?0 | semantical.{0}?0`' -f $row.gameAction,$row.scsKey))) { throw "$game binding failed: $($row.id)" }
    }
    if (-not $text.Contains('mix custom `keyboard.f11?0 | semantical.custom?0`')) { throw 'Unrelated binding changed' }
    $backups = @(Get-ChildItem ($path + '.simdeck-*.bak'))
    if ($backups.Count -ne 1 -or (Get-FileHash $backups[0].FullName).Hash -ne $before) { throw 'Backup differs from original' }
    $installed = (Get-FileHash $path).Hash
    & (Join-Path $repo 'tools/ets2-preset/Install-ETS2-Preset.ps1') -Game $game -ControlsPath $path | Out-Null
    if ((Get-FileHash $path).Hash -ne $installed -or @(Get-ChildItem ($path + '.simdeck-*.bak')).Count -ne 1) { throw 'Repeat installation is not idempotent' }
    Write-Host "PASS $game preview, 29 bindings, joystick/custom preservation, exact backup and idempotence"
}
foreach ($file in @('tools/ets2-preset/Install-ATS-Preset.cmd','tools/ets2-telemetry/Install-ATS.cmd')) {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $repo $file))
    if (@($bytes | Where-Object { $_ -gt 127 }).Count) { throw 'Launcher must be ASCII' }
    $text = [Text.Encoding]::ASCII.GetString($bytes)
    if ($text -match '(?<!\r)\n' -or -not $text.Contains('-Game ats')) { throw 'Launcher CRLF/game selection invalid' }
}
foreach ($file in @('tools/ets2-preset/Install-ETS2-Preset.ps1','tools/ets2-telemetry/Install-ETS2-Telemetry.ps1')) {
    $tokens=$null;$errors=$null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $repo $file),[ref]$tokens,[ref]$errors)
    if ($errors.Count) { throw ($errors | Out-String) }
}
Write-Host 'PASS ATS launchers and PowerShell parsing'
