param([Parameter(Mandatory=$true)][string]$PublishDirectory)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskPublish=[IO.Path]::GetFullPath($PublishDirectory)
$taskInfo=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskPublish '1Remote.exe'))
if($taskInfo.ProductName -ne 'xRemote'){throw 'Expected an xRemote publish directory'}
$taskWork=Join-Path $taskRoot 'tmp\auto-update\native-launcher'
New-Item -ItemType Directory -Path $taskWork -Force | Out-Null
$taskVersion=$taskInfo.FileVersion.Replace('.',',')
$taskIcon=(Join-Path $taskRoot 'Ui\LOGO.ico').Replace('\','/')
$taskRc=@"
#include <windows.h>
1 ICON "$taskIcon"
1 VERSIONINFO
FILEVERSION $taskVersion
PRODUCTVERSION $taskVersion
FILEOS VOS_NT_WINDOWS32
FILETYPE VFT_APP
BEGIN
 BLOCK "StringFileInfo"
 BEGIN
  BLOCK "040904b0"
  BEGIN
   VALUE "FileDescription", "xRemote\0"
   VALUE "ProductName", "xRemote\0"
   VALUE "FileVersion", "$($taskInfo.FileVersion)\0"
   VALUE "ProductVersion", "$($taskInfo.ProductVersion)\0"
   VALUE "OriginalFilename", "xRemote.exe\0"
  END
 END
 BLOCK "VarFileInfo"
 BEGIN
  VALUE "Translation", 0x409, 1200
 END
END
"@
$taskRcPath=Join-Path $taskWork 'launcher.rc'
[IO.File]::WriteAllText($taskRcPath,$taskRc,[Text.Encoding]::Unicode)
$taskVswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$taskVs=& $taskVswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(-not $taskVs){throw 'Visual C++ Build Tools are required for the native update launcher'}
$taskDev=Join-Path $taskVs 'Common7\Tools\VsDevCmd.bat'
$taskRes=Join-Path $taskWork 'launcher.res'; $taskObj=Join-Path $taskWork 'launcher.obj'; $taskExe=Join-Path $taskPublish 'xRemote.exe'
$taskSource=Join-Path $taskRoot 'Updater\Launcher.cpp'
$taskBatch=Join-Path $taskWork 'build.cmd'
$taskCommands=@('@echo off',('call "'+$taskDev+'" -no_logo -arch=x64 >nul'),'if errorlevel 1 exit /b 1',('rc /nologo /fo "'+$taskRes+'" "'+$taskRcPath+'"'),'if errorlevel 1 exit /b 1',('cl /nologo /EHsc /std:c++17 /O1 /MT /DUNICODE /D_UNICODE /Fo"'+$taskObj+'" "'+$taskSource+'" "'+$taskRes+'" /link /SUBSYSTEM:WINDOWS /OUT:"'+$taskExe+'" bcrypt.lib shell32.lib user32.lib'),'exit /b %errorlevel%')
[IO.File]::WriteAllLines($taskBatch,$taskCommands,[Text.Encoding]::ASCII)
$taskBuild=Start-Process -FilePath $env:ComSpec -ArgumentList ('/d /s /c ""'+$taskBatch+'""') -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput (Join-Path $taskWork 'build.log') -RedirectStandardError (Join-Path $taskWork 'build-error.log')
Get-Content (Join-Path $taskWork 'build.log') -Tail 5
Get-Content (Join-Path $taskWork 'build-error.log') -Tail 5
if($taskBuild.ExitCode -ne 0){throw 'Native update launcher build failed'}
Write-Output $taskExe
