[CmdletBinding()]
param(
    [ValidateSet('F1 24', 'F1 25')]
    [string]$Game = 'F1 24',
    [string]$GamePath
)

$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$actionsFile = Join-Path $scriptDirectory 'preset-actions.json'
$keyboardFiles = @('keyboard.xml', 'keyboard_fre.xml', 'keyboard_ger.xml')
$processName = if ($Game -eq 'F1 25') { 'F1_25' } else { 'F1_24' }

if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
    throw "Закройте $Game перед установкой раскладки."
}

function Find-GamePath([string]$gameName) {
    $candidates = [System.Collections.Generic.List[string]]::new()
    $candidates.Add("D:\games\steamapps\common\$gameName")
    $candidates.Add("C:\Program Files (x86)\Steam\steamapps\common\$gameName")
    $candidates.Add("C:\Program Files\EA Games\$gameName")

    $libraryFiles = @(
        'C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf',
        'C:\Program Files (x86)\Steam\config\libraryfolders.vdf'
    )
    foreach ($libraryFile in $libraryFiles) {
        if (-not (Test-Path -LiteralPath $libraryFile)) { continue }
        $content = [System.IO.File]::ReadAllText($libraryFile)
        foreach ($match in [regex]::Matches($content, '"path"\s+"([^"]+)"')) {
            $library = $match.Groups[1].Value.Replace('\\', '\')
            $candidates.Add((Join-Path $library "steamapps\common\$gameName"))
        }
    }

    return $candidates | Where-Object {
        Test-Path -LiteralPath (Join-Path $_ 'actionmaps\keyboard.xml')
    } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    $GamePath = Find-GamePath $Game
}
if ([string]::IsNullOrWhiteSpace($GamePath)) {
    $GamePath = Read-Host "Папка $Game не найдена автоматически. Введите полный путь к папке игры"
}

$resolvedGame = (Resolve-Path -LiteralPath $GamePath).Path
$actionMaps = Join-Path $resolvedGame 'actionmaps'
foreach ($fileName in $keyboardFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $actionMaps $fileName))) {
        throw "Не найден $fileName в $actionMaps. Проверьте путь к игре."
    }
}
if (-not (Test-Path -LiteralPath $actionsFile)) {
    throw "Рядом с установщиком отсутствует preset-actions.json."
}

$keyCodes = @{
    Back = 8; Tab = 9; Return = 13; Escape = 27
    Left = 37; Up = 38; Right = 39; Down = 40; Delete = 46
    OemSemicolon = 186; OemPlus = 187; OemMinus = 189
    OemQuotes = 222; OemOpenBrackets = 219; OemCloseBrackets = 221
}
foreach ($code in 65..90) { $keyCodes[[string][char]$code] = $code }
foreach ($number in 0..9) { $keyCodes["D$number"] = 48 + $number }
foreach ($number in 1..12) { $keyCodes["F$number"] = 111 + $number }

$bindings = [ordered]@{}
$actions = Get-Content -LiteralPath $actionsFile -Raw | ConvertFrom-Json
foreach ($row in $actions) {
    if (-not $keyCodes.ContainsKey([string]$row.key)) {
        throw "Неизвестная клавиша в раскладке: $($row.key)"
    }
    $axisName = 'vk_code_0x{0:X2}' -f $keyCodes[[string]$row.key]
    foreach ($actionName in ([string]$row.source -split '\|')) {
        $bindings[$actionName] = $axisName
    }
}

# Базовое управление оставляет клавиши SimDeck свободными от конфликтов.
$bindings['Accelerate'] = 'vk_code_0x20'
$bindings['Brake'] = 'vk_code_0xA2'
$bindings['Steer Left'] = 'vk_code_0xBC'
$bindings['Steer Right'] = 'vk_code_0xBE'
$bindings['Gear Up'] = 'vk_code_0x41'
$bindings['Gear Down'] = 'vk_code_0x5A'
$bindings['Clutch'] = 'vk_code_0x41'
$bindings['Clutch Classic'] = 'vk_code_0x41'
$bindings['DRS'] = 'vk_code_0x57'
$bindings['Handbrake'] = 'vk_code_0x57'
$bindings['Overtake'] = 'vk_code_0x44'
$addableActions = @(
    'Look Back',
    'Brake Bias Down', 'Brake Bias Up',
    'Differential Down', 'Differential Up',
    'Fuel Mix Down', 'Fuel Mix Up',
    'ERS Mode Down', 'ERS Mode Up'
)

$updatedFiles = [ordered]@{}
$missingByFile = [ordered]@{}
foreach ($fileName in $keyboardFiles) {
    $filePath = Join-Path $actionMaps $fileName
    $document = [System.IO.File]::ReadAllText($filePath)
    $mapPattern = '(?s)<ActionMap\b[^>]*actionMapName="kb_preset_2"[^>]*>.*?</ActionMap>'
    $mapMatch = [regex]::Match($document, $mapPattern)
    if (-not $mapMatch.Success) { throw "В $fileName не найден Keyboard Preset 2. Файлы не изменены." }
    $block = $mapMatch.Value
    $missing = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in $bindings.GetEnumerator()) {
        $escaped = [regex]::Escape([string]$entry.Key)
        $actionPattern = '(?s)(<Action\s+actionName="' + $escaped + '"\s*>.*?<Axis\s+[^>]*axisName=")[^"]+("[^>]*/?>.*?</Action>)'
        $actionMatch = [regex]::Match($block, $actionPattern)
        if (-not $actionMatch.Success) {
            if ($addableActions -contains [string]$entry.Key) {
                $addition = "    <Action actionName=`"$($entry.Key)`">`r`n" +
                    "                <Axis axisName=`"$($entry.Value)`" type=`"uniDirectionalPositive`" deadZone=`"0.0`" saturation=`"1.0`" />`r`n" +
                    "            </Action>`r`n        "
                $block = $block.Replace('</ActionMap>', $addition + '</ActionMap>')
                continue
            }
            $missing.Add([string]$entry.Key)
            continue
        }
        $replacement = $actionMatch.Groups[1].Value + [string]$entry.Value + $actionMatch.Groups[2].Value
        $block = $block.Substring(0, $actionMatch.Index) + $replacement + $block.Substring($actionMatch.Index + $actionMatch.Length)
    }
    $null = [xml]$block
    if ($missing.Count -gt 0) { $missingByFile[$fileName] = @($missing) }
    $updatedFiles[$fileName] = $document.Substring(0, $mapMatch.Index) + $block + $document.Substring($mapMatch.Index + $mapMatch.Length)
}

if ($missingByFile.Count -gt 0) {
    $details = ($missingByFile.GetEnumerator() | ForEach-Object { "$($_.Key): $($_.Value -join ', ')" }) -join [Environment]::NewLine
    throw "Версия файлов $Game отличается от проверенной раскладки. Ничего не изменено.`n$details"
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDirectory = Join-Path $actionMaps "simdeck-backup-$stamp"
New-Item -ItemType Directory -Path $backupDirectory | Out-Null
foreach ($fileName in $keyboardFiles) {
    $filePath = Join-Path $actionMaps $fileName
    Copy-Item -LiteralPath $filePath -Destination (Join-Path $backupDirectory $fileName)
}

try {
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    foreach ($fileName in $keyboardFiles) {
        [System.IO.File]::WriteAllText((Join-Path $actionMaps $fileName), [string]$updatedFiles[$fileName], $utf8NoBom)
    }
} catch {
    foreach ($fileName in $keyboardFiles) {
        Copy-Item -LiteralPath (Join-Path $backupDirectory $fileName) -Destination (Join-Path $actionMaps $fileName) -Force
    }
    throw "Запись не удалась; исходные файлы восстановлены. $($_.Exception.Message)"
}

Write-Host ''
Write-Host "SimDeck: Keyboard Preset 2 установлен для $Game." -ForegroundColor Green
Write-Host "Изменено действий: $($bindings.Count) в каждом из $($keyboardFiles.Count) файлов."
Write-Host "Резервная копия: $backupDirectory"
Write-Host 'Запустите игру и выберите Keyboard Preset 2.'
