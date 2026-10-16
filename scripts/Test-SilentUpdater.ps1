param([string]$UpdaterPath=(Join-Path (Split-Path $PSScriptRoot -Parent) 'tmp\auto-update\publish\updater\xRemote.Updater.exe'))
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskWork=Join-Path $taskRoot ('tmp\auto-update\helper-smoke-'+[Guid]::NewGuid().ToString('N'))
$taskPayload=Join-Path $taskWork 'payload'
$taskTarget=Join-Path $taskWork 'installed'
New-Item -ItemType Directory -Path (Join-Path $taskPayload 'updater'),$taskTarget -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $taskPayload 'xRemote.exe'),'new executable')
[IO.File]::WriteAllText((Join-Path $taskPayload 'xRemote.App.exe'),'new app host')
[IO.File]::WriteAllText((Join-Path $taskPayload '1Remote.dll'),'new component')
[IO.File]::WriteAllText((Join-Path $taskPayload 'updater\xRemote.Updater.exe'),'new helper')
[IO.File]::WriteAllText((Join-Path $taskTarget 'xRemote.exe'),'old executable')
[IO.File]::WriteAllText((Join-Path $taskTarget '1Remote.dll'),'old component')
[IO.File]::WriteAllText((Join-Path $taskTarget '1Remote.json'),'preserved profile')
[IO.File]::WriteAllText((Join-Path $taskTarget '1Remote.db'),'preserved database')
[IO.File]::WriteAllText((Join-Path $taskTarget 'FORCE_INTO_APPDATA_MODE'),'')
$taskDataHashes=@{}; foreach($taskName in @('1Remote.json','1Remote.db','FORCE_INTO_APPDATA_MODE')){$taskDataHashes[$taskName]=(Get-FileHash -LiteralPath (Join-Path $taskTarget $taskName)).Hash}
$taskZip=Join-Path $taskWork 'xRemote-v1.3.0-r999-x64.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($taskPayload,$taskZip)
& (Join-Path $PSScriptRoot 'New-UpdateManifest.ps1') -PackagePath $taskZip | Out-Null
$taskSha=[Security.Cryptography.SHA256]::Create()
try{$taskId=[BitConverter]::ToString($taskSha.ComputeHash([Text.Encoding]::UTF8.GetBytes($taskTarget.ToUpperInvariant()))).Replace('-','').ToLowerInvariant().Substring(0,24)}finally{$taskSha.Dispose()}
$taskState=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('xRemote\Updates\'+$taskId)
New-Item -ItemType Directory -Path $taskState -Force | Out-Null
Copy-Item -LiteralPath $taskZip -Destination $taskState
Copy-Item -LiteralPath (Join-Path $taskWork 'xRemote-update-win-x64.json') -Destination (Join-Path $taskState 'pending.json')
$taskParent=Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList '-NoProfile -NonInteractive -Command "Start-Sleep -Seconds 4"' -WindowStyle Hidden -PassThru
$taskRequest=Join-Path $taskState 'launch-smoke.json'
@{Target=$taskTarget;ParentPid=$taskParent.Id;ParentStarted=$taskParent.StartTime.ToUniversalTime().Ticks;Restart=$false;Arguments=@()} | ConvertTo-Json | Set-Content -LiteralPath $taskRequest -Encoding utf8
$taskHelper=Start-Process -FilePath $UpdaterPath -ArgumentList ('"'+$taskRequest+'"') -WindowStyle Hidden -PassThru
Start-Sleep -Milliseconds 600
if([IO.File]::ReadAllText((Join-Path $taskTarget 'xRemote.exe')) -ne 'old executable'){throw 'Updated files before parent exited'}
if($taskHelper.MainWindowHandle -ne 0){throw 'Updater created a visible main window'}
if(-not $taskHelper.WaitForExit(30000)){throw 'Updater did not finish'}
if($taskHelper.ExitCode -ne 0){throw ('Updater failed: '+[IO.File]::ReadAllText((Join-Path $taskState 'result.json')))}
if([IO.File]::ReadAllText((Join-Path $taskTarget 'xRemote.exe')) -ne 'new executable'){throw 'New EXE not installed'}
foreach($taskName in $taskDataHashes.Keys){if((Get-FileHash -LiteralPath (Join-Path $taskTarget $taskName)).Hash -ne $taskDataHashes[$taskName]){throw ('Data changed: '+$taskName)}}
if(Test-Path -LiteralPath (Join-Path $taskTarget 'FORCE_INTO_PORTABLE_MODE')){throw 'Installation mode changed'}
$taskPe=[IO.File]::ReadAllBytes($UpdaterPath); $taskPeOffset=[BitConverter]::ToInt32($taskPe,0x3c); $taskSubsystem=[BitConverter]::ToUInt16($taskPe,$taskPeOffset+24+68)
if($taskSubsystem -ne 2){throw 'Updater must use Windows GUI subsystem without a console'}
$taskReceipt=[ordered]@{state='passed';parentWait=$true;visibleMainWindow=$false;consoleSubsystem=$false;updaterExitCode=$taskHelper.ExitCode;profileAndDatabaseUnchanged=$true;automaticRestart=$false;target=$taskTarget;updateState=$taskState;result=(Get-Content (Join-Path $taskState 'result.json') -Raw | ConvertFrom-Json)}
$taskReceipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskWork 'receipt.json') -Encoding utf8
Write-Output ('PASS real signed updater waits for exit, silently applies full package, preserves data/mode, and does not restart: '+(Join-Path $taskWork 'receipt.json'))
