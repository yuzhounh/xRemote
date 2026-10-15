param([Parameter(Mandatory=$true)][string]$PackageDirectory, [string]$InnoSetupCompiler,
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'dist'))
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskPackage = (Resolve-Path -LiteralPath $PackageDirectory).Path
$taskVersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskPackage 'xRemote.exe'))
if ($taskVersionInfo.ProductName -ne 'xRemote' -or $taskVersionInfo.FileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Expected a clean xRemote package directory' }
if (@(Get-ChildItem -LiteralPath $taskPackage -Recurse -Force | Where-Object { $_.Name -match '^1Remote\.(json|db|dataSources\.json)(-wal|-shm)?$|^\.locality$|^\.rdp-passwords\.json$' }).Count -gt 0) { throw 'Package contains user data' }
$taskParts = $taskVersionInfo.FileVersion.Split('.')
$taskReleaseVersion = ($taskParts[0..2] -join '.') + '-r' + $taskParts[3]
if (-not $InnoSetupCompiler) {
    $taskCandidates = @((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'), (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'), (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'))
    $InnoSetupCompiler = $taskCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $InnoSetupCompiler) { throw 'Install Inno Setup 6 or provide -InnoSetupCompiler' }
$taskMarkerDirectory = Join-Path $taskRoot ('artifacts\installer-' + $taskReleaseVersion)
New-Item -ItemType Directory -Path $taskMarkerDirectory -Force | Out-Null
$taskModeMarker = Join-Path $taskMarkerDirectory 'FORCE_INTO_APPDATA_MODE'
New-Item -ItemType File -Path $taskModeMarker -Force | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path
& $InnoSetupCompiler '/Qp' "/O$taskOutput" "/DReleaseVersion=$taskReleaseVersion" "/DFileVersion=$($taskVersionInfo.FileVersion)" "/DPackageSource=$taskPackage" "/DModeMarker=$taskModeMarker" (Join-Path $taskRoot 'Installer\xRemote.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed' }
$taskSetup = Join-Path $taskOutput ('xRemote-v' + $taskReleaseVersion + '-x64-setup.exe')
$taskHash = (Get-FileHash -LiteralPath $taskSetup -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($taskSetup + '.sha256') -Value ($taskHash + '  ' + (Split-Path $taskSetup -Leaf)) -Encoding ascii
Write-Output $taskSetup
Write-Output ('SHA256=' + $taskHash)
