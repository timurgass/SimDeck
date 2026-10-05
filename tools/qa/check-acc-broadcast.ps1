$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$fixture = Join-Path $repo ('artifacts/acc-broadcast-qa-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$file = Join-Path $fixture 'broadcasting.json'
$installer = Join-Path $repo 'tools/acc-broadcast/Enable-ACC-Broadcast.ps1'
[IO.File]::WriteAllBytes($file, [Text.Encoding]::Unicode.GetBytes('{"updListenerPort":0,"connectionPassword":"","commandPassword":"","keepMe":23}'))
& $installer -ConfigDirectory $fixture
$bytes = [IO.File]::ReadAllBytes($file)
if ($bytes[0] -ne 123 -or $bytes[1] -ne 0) { throw 'Installer must write native UTF-16 LE without BOM.' }
$first = [Text.Encoding]::Unicode.GetString($bytes) | ConvertFrom-Json
if ($first.updListenerPort -ne 9000 -or $first.keepMe -ne 23 -or $first.connectionPassword.Length -ne 32 -or $first.commandPassword.Length -ne 32 -or $first.connectionPassword -eq $first.commandPassword) { throw 'Invalid generated configuration.' }
if (@(Get-ChildItem -LiteralPath $fixture -Filter '*.before-simdeck-*').Count -ne 1) { throw 'Backup missing.' }
& $installer -ConfigDirectory $fixture
$second = [Text.Encoding]::Unicode.GetString([IO.File]::ReadAllBytes($file)) | ConvertFrom-Json
if ($second.connectionPassword -ne $first.connectionPassword -or $second.commandPassword -ne $first.commandPassword -or $second.keepMe -ne 23) { throw 'Rerun changed existing passwords/settings.' }
$second.updListenerPort = 9011
[IO.File]::WriteAllBytes($file, [Text.Encoding]::UTF8.GetBytes(($second | ConvertTo-Json)))
& $installer -ConfigDirectory $fixture
$third = [Text.Encoding]::Unicode.GetString([IO.File]::ReadAllBytes($file)) | ConvertFrom-Json
if ($third.updListenerPort -ne 9011 -or $third.connectionPassword -ne $first.connectionPassword) { throw 'Existing port/password changed.' }
[IO.File]::WriteAllText($file, 'invalid-json')
$rejected = $false
try { & $installer -ConfigDirectory $fixture } catch { $rejected = $true }
if (-not $rejected -or [IO.File]::ReadAllText($file) -ne 'invalid-json') { throw 'Malformed configuration was overwritten.' }
Write-Output 'PASS ACC installer: native encoding, backup, preserved settings/passwords/port, idempotence, invalid JSON rejection.'
