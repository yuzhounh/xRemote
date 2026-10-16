param([string]$PublishDirectory=(Join-Path (Split-Path $PSScriptRoot -Parent) 'tmp\auto-update\final-publish'))
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskWork=Join-Path $taskRoot ('tmp\auto-update\launcher-test-'+[Guid]::NewGuid().ToString('N'))
$taskTarget=Join-Path $taskWork 'installed app 测试'
$taskPayload=Join-Path $taskWork 'payload'
New-Item -ItemType Directory -Path $taskTarget,(Join-Path $taskPayload 'updater') -Force | Out-Null
$taskVs=& (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe') -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$taskDev=Join-Path $taskVs 'Common7\Tools\VsDevCmd.bat'
$taskProbe=Join-Path $taskWork 'probe.exe'
$taskSource=Join-Path $PSScriptRoot 'update-tests\LauncherProbe.cpp'
$taskBatch=Join-Path $taskWork 'build-probe.cmd'
[IO.File]::WriteAllLines($taskBatch,@('@echo off',('call "'+$taskDev+'" -no_logo -arch=x64 >nul'),'if errorlevel 1 exit /b 1',('cl /nologo /EHsc /O1 /MT /DUNICODE /D_UNICODE /Fo"'+(Join-Path $taskWork 'probe.obj')+'" "'+$taskSource+'" /link /SUBSYSTEM:WINDOWS /OUT:"'+$taskProbe+'" shell32.lib'),'exit /b %errorlevel%'),[Text.Encoding]::ASCII)
$taskBuild=Start-Process -FilePath $env:ComSpec -ArgumentList ('/d /s /c ""'+$taskBatch+'""') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $taskWork 'build.log') -RedirectStandardError (Join-Path $taskWork 'build-error.log')
if($taskBuild.ExitCode -ne 0){throw 'Probe build failed'}
Copy-Item -LiteralPath (Join-Path $PublishDirectory 'xRemote.exe') -Destination $taskTarget
Copy-Item -LiteralPath $taskProbe -Destination (Join-Path $taskTarget 'xRemote.App.exe')
function Quote-TaskArg([string]$value){ '"'+([regex]::Replace(([regex]::Replace($value,'(\\*)"','$1$1\"')),'(\\+)$','$1$1'))+'"' }
function Start-TaskProbe([string]$marker){
    $taskArgs=@($marker,'a path with spaces','quoted"value','trailing\','中文参数')
    $taskProcess=Start-Process -FilePath (Join-Path $taskTarget 'xRemote.exe') -ArgumentList (($taskArgs | ForEach-Object { Quote-TaskArg $_ }) -join ' ') -WindowStyle Hidden -PassThru
    if(-not $taskProcess.WaitForExit(30000) -or $taskProcess.ExitCode -ne 0){throw 'Native launcher failed'}
    $taskDeadline=[DateTime]::UtcNow.AddSeconds(30)
    while(-not(Test-Path -LiteralPath $marker) -and [DateTime]::UtcNow -lt $taskDeadline){Start-Sleep -Milliseconds 100}
    if(-not(Test-Path -LiteralPath $marker)){throw 'Application did not start'}
    $taskRead=[IO.File]::ReadAllText($marker,[Text.Encoding]::Unicode).TrimEnd("`n").Split("`n")
    if(($taskRead -join '|') -ne ($taskArgs[1..4] -join '|')){throw 'Launcher changed startup arguments'}
}
Start-TaskProbe (Join-Path $taskWork 'normal-launch.txt')
Copy-Item -LiteralPath (Join-Path $PublishDirectory 'xRemote.exe') -Destination $taskPayload
Copy-Item -LiteralPath $taskProbe -Destination (Join-Path $taskPayload 'xRemote.App.exe')
Copy-Item -LiteralPath (Join-Path $PublishDirectory 'updater\xRemote.Updater.exe') -Destination (Join-Path $taskPayload 'updater')
[IO.File]::WriteAllText((Join-Path $taskPayload '1Remote.dll'),'new complete component')
$taskZip=Join-Path $taskWork 'xRemote-v1.3.0-r999-x64.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($taskPayload,$taskZip)
& (Join-Path $PSScriptRoot 'New-UpdateManifest.ps1') -PackagePath $taskZip | Out-Null
$taskSha=[Security.Cryptography.SHA256]::Create()
try{$taskId=[BitConverter]::ToString($taskSha.ComputeHash([Text.Encoding]::UTF8.GetBytes($taskTarget.ToUpperInvariant()))).Replace('-','').ToLowerInvariant().Substring(0,24)}finally{$taskSha.Dispose()}
$taskState=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('xRemote\Updates\'+$taskId)
New-Item -ItemType Directory -Path $taskState -Force | Out-Null
$taskHelperSource=Join-Path $PublishDirectory 'updater\xRemote.Updater.exe'
$taskHelperName='helper-'+(Get-FileHash -LiteralPath $taskHelperSource).Hash.ToLowerInvariant()+'.exe'
Copy-Item -LiteralPath $taskHelperSource -Destination (Join-Path $taskState $taskHelperName)
[IO.File]::WriteAllText((Join-Path $taskState 'helper-name.txt'),$taskHelperName,[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath $taskZip -Destination $taskState
Copy-Item -LiteralPath (Join-Path $taskWork 'xRemote-update-win-x64.json') -Destination (Join-Path $taskState 'pending.json')
[IO.File]::WriteAllText((Join-Path $taskTarget 'xRemote.App.exe'),'unstartable partial app host')
[IO.File]::WriteAllText((Join-Path $taskTarget '1Remote.dll'),'partial component')
[IO.File]::WriteAllText((Join-Path $taskTarget '1Remote.json'),'private settings unchanged')
$taskBefore=(Get-FileHash -LiteralPath (Join-Path $taskTarget '1Remote.json')).Hash
Start-TaskProbe (Join-Path $taskWork 'recovered-launch.txt')
if((Get-FileHash -LiteralPath (Join-Path $taskTarget '1Remote.json')).Hash -ne $taskBefore){throw 'Settings changed'}
if((Get-Content (Join-Path $taskState 'result.json') -Raw | ConvertFrom-Json).state -ne 'applied'){throw 'Update not completed'}
@{state='passed';nativeStartup=$true;startupRecoveryWithUnstartableApp=$true;argumentsPreserved=$true;settingsUnchanged=$true;target=$taskTarget;cache=$taskState} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskWork 'receipt.json') -Encoding utf8
Write-Output ('PASS native launcher, complete signed startup update before loading a broken app, silent relaunch and argument/data preservation: '+(Join-Path $taskWork 'receipt.json'))
