param([string]$ControlsPath)

$ErrorActionPreference = 'Stop'
if (Get-Process eurotrucks2 -ErrorAction SilentlyContinue) {
    throw 'Закройте ETS2 перед установкой клавиш круиза: игра перезапишет controls.sii при выходе.'
}
if (-not $ControlsPath) {
    $profiles = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Euro Truck Simulator 2\steam_profiles'
    $candidate = Get-ChildItem -LiteralPath $profiles -Filter controls.sii -Recurse -File |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $candidate) { throw 'controls.sii не найден. Передайте путь через -ControlsPath.' }
    $ControlsPath = $candidate.FullName
}
$ControlsPath = (Resolve-Path -LiteralPath $ControlsPath).Path
$original = [IO.File]::ReadAllText($ControlsPath)
$updated = $original
$bindings = @(
    @{ Action='cruiectrlinc'; Key='f8' },
    @{ Action='cruiectrldec'; Key='f9' }
)
foreach ($binding in $bindings) {
    $action = $binding.Action
    $key = $binding.Key
    $pattern = '(mix ' + $action + ' `)([^`]*)(`)'
    $matches = [regex]::Matches($updated, $pattern)
    if ($matches.Count -ne 1) { throw "Ожидалась одна строка $action, найдено $($matches.Count). Файл не изменён." }
    $before = $matches[0].Groups[2].Value
    if ($before -notmatch ('^(keyboard\.' + $key + '\?0 \| )?semantical\.' + $action + '\?0$')) {
        throw "Привязка $action уже изменена пользователем. Файл не изменён."
    }
    $replacement = '$1' + 'keyboard.' + $key + '?0 | semantical.' + $action + '?0' + '$3'
    $updated = [regex]::Replace($updated, $pattern, $replacement)
}
if ($updated -eq $original) { Write-Output "Клавиши F8/F9 уже установлены: $ControlsPath"; exit 0 }
$backup = "$ControlsPath.simdeck-$(Get-Date -Format yyyyMMddHHmmss).bak"
[IO.File]::Copy($ControlsPath, $backup)
$temp = "$ControlsPath.simdeck.tmp"
try {
    [IO.File]::WriteAllText($temp, $updated, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temp -Destination $ControlsPath -Force
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp }
}
Write-Output "ETS2: круиз + = F8, круиз − = F9. Резервная копия: $backup"
