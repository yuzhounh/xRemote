param(
    [Parameter(Mandatory=$true)][string]$PublishDirectory,
    [string]$OutputDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) 'dist'),
    [string]$StagingDirectory
)
$ErrorActionPreference = 'Stop'
$taskSource = (Resolve-Path -LiteralPath $PublishDirectory).Path
$taskMetadata = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskSource '1Remote.exe'))
if ($taskMetadata.ProductName -ne 'xRemote' -or $taskMetadata.FileVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw 'Input is not a published xRemote Windows build'
}
$taskParts = $taskMetadata.FileVersion.Split('.')
$taskVersion = ($taskParts[0..2] -join '.') + '-r' + $taskParts[3]
$taskForbidden = @(Get-ChildItem -LiteralPath $taskSource -Recurse -Force | Where-Object {
    $_.Name -match '^1Remote\.(json|db|dataSources\.json)(-wal|-shm)?$|^\.locality$|^\.icons$|^\.logs$|^\.rdp-passwords\.json$'
})
if ($taskForbidden.Count -gt 0) { throw 'Input contains user data; publish to an empty build directory before packaging' }
if (-not (Test-Path -LiteralPath (Join-Path $taskSource 'updater\xRemote.Updater.exe'))) { throw 'Publish the silent updater before packaging' }
if (-not (Test-Path -LiteralPath (Join-Path $taskSource 'xRemote.exe'))) { throw 'Publish the native update launcher before packaging' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskDestination = Join-Path (Resolve-Path -LiteralPath $OutputDirectory).Path ('xRemote-v' + $taskVersion + '-x64.zip')
if (Test-Path -LiteralPath $taskDestination) { throw 'Release archive already exists; refusing to overwrite it' }
$taskStage = if ($StagingDirectory) { [System.IO.Path]::GetFullPath($StagingDirectory) } else { Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts\package-' + $taskVersion) }
if (Test-Path -LiteralPath $taskStage) { throw 'Packaging staging directory already exists' }
New-Item -ItemType Directory -Path $taskStage | Out-Null
Get-ChildItem -LiteralPath $taskSource -Force | Copy-Item -Destination $taskStage -Recurse
Rename-Item -LiteralPath (Join-Path $taskStage '1Remote.exe') -NewName 'xRemote.App.exe'
New-Item -Path (Join-Path $taskStage 'FORCE_INTO_PORTABLE_MODE') -ItemType File -Force | Out-Null
Copy-Item -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'LICENSE') -Destination $taskStage
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($taskStage, $taskDestination, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$taskHash = (Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($taskDestination + '.sha256') -Value ($taskHash + '  ' + (Split-Path $taskDestination -Leaf)) -Encoding ascii
Write-Output $taskDestination
Write-Output ('SHA256=' + $taskHash)
& (Join-Path $PSScriptRoot 'New-UpdateManifest.ps1') -PackagePath $taskDestination
