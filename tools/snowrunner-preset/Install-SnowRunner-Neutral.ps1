param([string]$SettingsPath)
$ErrorActionPreference='Stop'
if(Get-Process SnowRunner -ErrorAction SilentlyContinue){throw 'Close SnowRunner before changing keyboard assignments.'}
if(-not $SettingsPath){
 $files=@();$roots=@();$steam=Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
 if($steam.SteamPath){$roots+=Join-Path $steam.SteamPath 'userdata'}
 foreach($root in $roots){if(Test-Path -LiteralPath $root){foreach($user in Get-ChildItem -LiteralPath $root -Directory){$file=Join-Path $user.FullName '1465360\remote\user_settings.cfg';if(Test-Path -LiteralPath $file){$files+=Get-Item -LiteralPath $file}}}}
 $storage=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\SnowRunner\base\storage'
 if(Test-Path -LiteralPath $storage){$files+=Get-ChildItem -LiteralPath $storage -Recurse -File | Where-Object {$_.Name -in @('user_settings.cfg','user_settings.dat')}}
 $files=@($files | Sort-Object LastWriteTimeUtc -Descending)
 if($files.Count -eq 1){$SettingsPath=$files[0].FullName}else{throw 'Specify -SettingsPath with the current SnowRunner user_settings.cfg path.'}
}
$resolved=(Resolve-Path -LiteralPath $SettingsPath).Path
if([IO.Path]::GetFileName($resolved) -notin @('user_settings.cfg','user_settings.dat')){throw 'Expected SnowRunner user_settings.cfg or user_settings.dat.'}
$raw=[IO.File]::ReadAllText($resolved);$document=$raw.TrimEnd([char]0) | ConvertFrom-Json
if(-not $document.UserSettings -or $null -eq $document.UserSettings.bindings){throw 'SnowRunner keyboard settings were not found.'}
$neutral=@($document.UserSettings.bindings | Where-Object {$_.toolLink -eq 'Exploration.SetGearNeutral'})
if($neutral.Count -ne 1){throw 'Expected exactly one neutral gear binding.'}
$conflict=@($document.UserSettings.bindings | Where-Object {$_.device -eq 'keyboard' -and $_.keyCode -eq 77 -and $_.combIndex -eq 0 -and $_.toolLink -ne 'Exploration.SetGearNeutral' -and $_.toolLink -notlike 'UI.FlyCameraRotate.*'})
if($conflict.Count){throw 'NumPad6 is already assigned to another action. No settings were changed.'}
if($neutral[0].device -eq 'keyboard' -and $neutral[0].keyCode -eq 77 -and $neutral[0].combIndex -eq 0){Write-Host 'Neutral already assigned to NumPad6.';exit 0}
$neutral[0].device='keyboard';$neutral[0].keyCode=77;$neutral[0].combIndex=0
$backup=$resolved+'.simdeck-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.bak'
Copy-Item -LiteralPath $resolved -Destination $backup
$text=$document | ConvertTo-Json -Depth 100 -Compress
if($raw.EndsWith([string][char]0)){$text+=[char]0}
$temp=$resolved+'.simdeck-tmp';[IO.File]::WriteAllText($temp,$text,(New-Object Text.UTF8Encoding($false)))
try{if(Get-Process SnowRunner -ErrorAction SilentlyContinue){throw 'SnowRunner started during setup. No changes were applied.'};Move-Item -LiteralPath $temp -Destination $resolved -Force}finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
Write-Host 'Neutral gear assigned to NumPad6. Other bindings preserved. Backup created beside the settings file.'
