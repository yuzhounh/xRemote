param([Parameter(Mandatory=$true)][string]$PublishDirectory,
    [ValidateSet('protocol','entries')][string]$Mode='protocol')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskBinary=(Resolve-Path -LiteralPath $PublishDirectory).Path
$taskOutput=Join-Path $taskRoot $(if($Mode -eq 'entries'){'artifacts\autosave-entry-audit\checks'}else{'artifacts\protocol-confirmation\checks'})
& dotnet build (Join-Path $PSScriptRoot 'protocol-confirmation-check\Check.csproj') -c Release -m:2 -o $taskOutput
if($LASTEXITCODE -ne 0){throw 'Protocol confirmation check build failed'}
if($Mode -eq 'entries'){ & (Join-Path $taskOutput 'Check.exe') $taskBinary entries }
else { & (Join-Path $taskOutput 'Check.exe') $taskBinary }
if($LASTEXITCODE -ne 0){throw 'Protocol confirmation regression failed'}
