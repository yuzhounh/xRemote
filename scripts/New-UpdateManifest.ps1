param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [string]$SigningKeyPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'update-signing-private\release-key.xml')
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskPackage = (Resolve-Path -LiteralPath $PackagePath).Path
if ((Split-Path $taskPackage -Leaf) -notmatch '^xRemote-(v(\d+)\.(\d+)\.(\d+)-r(\d+))-x64\.zip$') { throw 'Not a versioned Windows x64 release package' }
$taskTag = $Matches[1]
$taskVersion = $Matches[2..5] -join '.'
if (-not (Test-Path -LiteralPath $SigningKeyPath)) { throw 'Release signing key is required; preserve the existing private key outside version control' }
$taskRsa = [System.Security.Cryptography.RSA]::Create()
try {
    $taskRsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $SigningKeyPath).Path))
    if ($taskRsa.ToXmlString($false) -ne [IO.File]::ReadAllText((Join-Path $taskRoot 'Updater\update-public-key.xml')).Trim()) { throw 'Signing key does not match the application trust key' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $taskArchive = [IO.Compression.ZipFile]::OpenRead($taskPackage)
    try {
        $taskFiles = @($taskArchive.Entries | Where-Object { $_.Name -and $_.FullName -ne 'FORCE_INTO_PORTABLE_MODE' } | Sort-Object FullName | ForEach-Object {
            $taskEntry = $_
            if ($taskEntry.FullName -match '(^|/)(\.\.?|\.[^/]*|data|Protocols|KiTTY|PuTTY|unins[^/]*)(/|$)|[\\:]|^1Remote\.(json|db|dataSources\.json)|\.(db|sqlite|sqlite3)(-wal|-shm)?$') { throw 'Package contains protected data or an unsafe path' }
            $taskStream = $taskEntry.Open()
            $taskSha = [Security.Cryptography.SHA256]::Create()
            try { $taskHash = [BitConverter]::ToString($taskSha.ComputeHash($taskStream)).Replace('-','').ToLowerInvariant() }
            finally { $taskStream.Dispose(); $taskSha.Dispose() }
            [ordered]@{ Path=$taskEntry.FullName; Sha256=$taskHash; Size=$taskEntry.Length }
        })
    } finally { $taskArchive.Dispose() }
    foreach($taskRequired in @('xRemote.exe','xRemote.App.exe','1Remote.dll','updater/xRemote.Updater.exe')) {
        if ($taskRequired -notin @($taskFiles | ForEach-Object { $_.Path })) { throw ('Missing required program component: '+$taskRequired) }
    }
    $taskManifest = [ordered]@{ Schema=1; Version=$taskVersion; Tag=$taskTag; Package=(Split-Path $taskPackage -Leaf); Sha256=(Get-FileHash -LiteralPath $taskPackage -Algorithm SHA256).Hash.ToLowerInvariant(); Size=(Get-Item -LiteralPath $taskPackage).Length; Files=$taskFiles }
    $taskBytes = [Text.Encoding]::UTF8.GetBytes(($taskManifest | ConvertTo-Json -Depth 5 -Compress))
    $taskSignature = $taskRsa.SignData($taskBytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $taskEnvelope = [ordered]@{ Payload=[Convert]::ToBase64String($taskBytes); Signature=[Convert]::ToBase64String($taskSignature) }
    $taskOutput = Join-Path (Split-Path $taskPackage -Parent) 'xRemote-update-win-x64.json'
    [IO.File]::WriteAllText($taskOutput,($taskEnvelope | ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
    Write-Output $taskOutput
} finally { $taskRsa.Dispose() }
