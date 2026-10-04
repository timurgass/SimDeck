[CmdletBinding()]
param(
    [ValidateSet("ets2", "ats")][string]$Game = "ets2",
    [string]$ControlsPath,
    [string]$ProfileId,
    [switch]$ListProfiles,
    [switch]$Preview
)

$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath = Join-Path $scriptDirectory 'preset-actions.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'Рядом с установщиком не найден preset-actions.json. Полностью распакуйте архив.'
}

$gameName = if ($Game -eq 'ats') { 'American Truck Simulator' } else { 'Euro Truck Simulator 2' }
$processName = if ($Game -eq 'ats') { 'amtrucks' } else { 'eurotrucks2' }
$root = Join-Path ([Environment]::GetFolderPath('MyDocuments')) $gameName
function Get-ProfileLabel([string]$folderName) {
    if ($folderName.Length % 2 -eq 0 -and $folderName -match '^[0-9A-Fa-f]+$') {
        try {
            [byte[]]$bytes = for ($i = 0; $i -lt $folderName.Length; $i += 2) {
                [Convert]::ToByte($folderName.Substring($i, 2), 16)
            }
            $decoded = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
            if ($decoded -match '^[\p{L}\p{N} ._-]+$') { return "$decoded [$folderName]" }
        } catch { }
    }
    return $folderName
}
$candidates = @(
    foreach ($folder in @('steam_profiles', 'profiles')) {
        $path = Join-Path $root $folder
        if (Test-Path -LiteralPath $path) {
            Get-ChildItem -LiteralPath $path -Filter controls.sii -Recurse -File -ErrorAction Stop
        }
    }
) | Sort-Object FullName -Unique | Sort-Object LastWriteTime -Descending

if ($ListProfiles) {
    if (-not $candidates) { throw 'Профили выбранной игры с controls.sii не найдены.' }
    $candidates | Select-Object @{Name='Profile';Expression={Get-ProfileLabel $_.Directory.Name}},LastWriteTime,FullName | Format-Table -AutoSize
    return
}

if ($ControlsPath) {
    if ($ProfileId) { throw 'Укажите либо -ControlsPath, либо -ProfileId.' }
    $ControlsPath = (Resolve-Path -LiteralPath $ControlsPath -ErrorAction Stop).Path
} else {
    $choices = @($candidates)
    if ($ProfileId) { $choices = @($choices | Where-Object { $_.Directory.Name -eq $ProfileId }) }
    if ($choices.Count -eq 0) { throw 'Профиль выбранной игры не найден. Запустите с -ListProfiles или укажите -ControlsPath.' }
    if ($choices.Count -gt 1) {
        Write-Host 'Найдено несколько профилей ETS2:'
        for ($i = 0; $i -lt $choices.Count; $i++) {
            Write-Host ('{0}. {1}  ({2:yyyy-MM-dd HH:mm})' -f ($i + 1), (Get-ProfileLabel $choices[$i].Directory.Name), $choices[$i].LastWriteTime)
        }
        $answer = Read-Host 'Введите номер профиля'
        $number = 0
        if (-not [int]::TryParse($answer, [ref]$number) -or $number -lt 1 -or $number -gt $choices.Count) {
            throw 'Неверный номер профиля. Файлы не изменены.'
        }
        $ControlsPath = $choices[$number - 1].FullName
    } else {
        $ControlsPath = $choices[0].FullName
    }
}

if ((Split-Path -Leaf $ControlsPath) -ne 'controls.sii') {
    throw 'Нужен именно файл controls.sii. Файлы не изменены.'
}
$liveRoot = $root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$isLiveProfile = $ControlsPath.StartsWith($liveRoot, [StringComparison]::OrdinalIgnoreCase)
if ($isLiveProfile -and (Get-Process -Name $processName -ErrorAction SilentlyContinue)) {
    throw 'Сохраните игру и полностью закройте выбранную игру: при выходе она перезапишет controls.sii.'
}

$actions = Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json
if ($actions.Count -ne 29) { throw 'Повреждён файл preset-actions.json: ожидалось 29 действий.' }
foreach ($property in @('id', 'gameAction', 'scsKey')) {
    $values = @($actions | ForEach-Object { [string]($_.$property) })
    if (@($values | Where-Object { $_ -notmatch '^[A-Za-z0-9_]+$' }).Count -gt 0 -or @($values | Select-Object -Unique).Count -ne $actions.Count) {
        throw "Повреждён файл preset-actions.json: повтор или неверное значение $property."
    }
}

$originalBytes = [IO.File]::ReadAllBytes($ControlsPath)
$hasBom = $originalBytes.Length -ge 3 -and $originalBytes[0] -eq 239 -and $originalBytes[1] -eq 187 -and $originalBytes[2] -eq 191
$encoding = [Text.UTF8Encoding]::new($hasBom, $true)
$original = if ($hasBom) { $encoding.GetString($originalBytes, 3, $originalBytes.Length - 3) } else { $encoding.GetString($originalBytes) }
$updated = $original
$changes = [System.Collections.Generic.List[string]]::new()

foreach ($row in $actions) {
    $action = [string]$row.gameAction
    $key = [string]$row.scsKey
    $pattern = '(?m)^(?<prefix>[ \t]*config_lines\[\d+\]:[ \t]*"mix[ \t]+' + [regex]::Escape($action) + '[ \t]+`)(?<expr>[^`]*)(?<suffix>`"[ \t]*\r?)$'
    $hits = [regex]::Matches($updated, $pattern)
    if ($hits.Count -ne 1) {
        throw "Команда SCS '$action' должна встречаться ровно один раз, найдено $($hits.Count). Файл не изменён."
    }
    $oldExpression = $hits[0].Groups['expr'].Value
    $parts = @($oldExpression -split '[ \t]+\|[ \t]+')
    if ($parts -notcontains "semantical.$($action)?0") {
        throw "Неизвестный формат команды '$action'. Файл не изменён."
    }
    $complexKeys = @($parts | Where-Object { $_ -match 'keyboard\.' -and $_ -notmatch '^keyboard\.[a-z0-9_]+\?0$' })
    if ($complexKeys.Count -gt 0) {
        throw "У команды '$action' сложное сочетание клавиш. Файл не изменён; настройте его вручную."
    }
    $otherInputs = @($parts | Where-Object { $_ -notmatch '^keyboard\.[a-z0-9_]+\?0$' })
    $newExpression = (@("keyboard.$($key)?0") + $otherInputs) -join ' | '
    if ($newExpression -ne $oldExpression) {
        $changes.Add("$action : $oldExpression -> $newExpression")
        $group = $hits[0].Groups['expr']
        $updated = $updated.Substring(0, $group.Index) + $newExpression + $updated.Substring($group.Index + $group.Length)
    }
}

Write-Host "Профиль: $ControlsPath"
Write-Host "Проверено действий SimDeck: $($actions.Count); требуется изменить: $($changes.Count)."
if ($Preview) { $changes | ForEach-Object { Write-Host $_ }; return }
if ($changes.Count -eq 0) { Write-Host 'Раскладка уже установлена. Файл не изменён.'; return }

$backup = "$ControlsPath.simdeck-$(Get-Date -Format yyyyMMddHHmmssfff).bak"
$temp = "$ControlsPath.simdeck.tmp"
try {
    [IO.File]::WriteAllText($temp, $updated, $encoding)
    [IO.File]::Replace($temp, $ControlsPath, $backup)
} finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force }
}
Write-Host "Готово: 29 команд выбранной игры соответствуют кнопкам SimDeck. Резервная копия: $backup"
