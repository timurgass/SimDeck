param([string]$ConfigDirectory = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Assetto Corsa Competizione/Config'))
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'AC2-Win64-Shipping' -ErrorAction SilentlyContinue) {
    throw 'Close ACC completely before changing its Broadcasting settings.'
}
if (-not (Test-Path -LiteralPath $ConfigDirectory -PathType Container)) { throw 'ACC Config directory was not found. Start ACC once first.' }
$configPath = Join-Path $ConfigDirectory 'broadcasting.json'
$config = [pscustomobject]@{}
if (Test-Path -LiteralPath $configPath) {
    $bytes = [IO.File]::ReadAllBytes($configPath)
    $utf16 = $bytes.Length -ge 2 -and (($bytes[0] -eq 255 -and $bytes[1] -eq 254) -or $bytes[1] -eq 0)
    $text = if ($utf16) { [Text.Encoding]::Unicode.GetString($bytes) } else { [Text.Encoding]::UTF8.GetString($bytes) }
    $config = $text.TrimStart([char]0xfeff) | ConvertFrom-Json
    if ($null -eq $config -or $config -is [array] -or $config -isnot [pscustomobject]) { throw 'Invalid ACC Broadcasting configuration.' }
    $backup = $configPath + '.before-simdeck-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
    Copy-Item -LiteralPath $configPath -Destination $backup
}
$port = 0
if ($config.PSObject.Properties['updListenerPort']) { $port = [int]$config.updListenerPort }
if ($port -eq 0) { $port = 9000 }
if ($port -lt 1024 -or $port -gt 65535) { throw 'Invalid ACC Broadcasting port.' }
$config | Add-Member -NotePropertyName 'updListenerPort' -NotePropertyValue $port -Force
foreach ($name in @('connectionPassword','commandPassword')) {
    if (-not $config.PSObject.Properties[$name] -or [string]::IsNullOrEmpty($config.$name)) {
        $config | Add-Member -NotePropertyName $name -NotePropertyValue ([Guid]::NewGuid().ToString('N')) -Force
    }
}
$tmp = $configPath + '.simdeck.tmp'
# ACC expects its native UTF-16 LE JSON format without a BOM; UTF-8 causes
# the game to discard the file and recreate it with Broadcasting disabled.
[IO.File]::WriteAllText($tmp, ($config | ConvertTo-Json -Depth 20), (New-Object Text.UnicodeEncoding($false,$false)))
Move-Item -LiteralPath $tmp -Destination $configPath -Force
Write-Output "ACC Broadcasting enabled on port $port. Backup preserved. Restart ACC and start a session with opponents."
