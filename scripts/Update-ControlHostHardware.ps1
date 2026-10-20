param([Parameter(Mandatory=$true)][string]$PackageDirectory)
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(!([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Administrator rights are required to update xRemoteControl'}
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$service=Get-CimInstance Win32_Service -Filter "Name='xRemoteControl'"
if(!$service -or $service.PathName -notmatch '^\s*(?:"([^"]+)"|(.+?\.exe))\s+--config\s+(?:"([^"]+)"|(.+?))\s*$'){throw 'Unexpected service registration'}
$exe=if($Matches[1]){$Matches[1]}else{$Matches[2]}
$configPath=if($Matches[3]){$Matches[3]}else{$Matches[4]}
if([IO.Path]::GetFileName($exe) -ne 'xRemote.ControlHost.exe' -or [IO.Path]::GetFileName($configPath) -ne 'server.json'){throw 'Unexpected service files'}
$target=Split-Path $exe -Parent
$configuration=Split-Path $configPath -Parent
$serviceRoot=Split-Path (Split-Path $target -Parent) -Parent
$work=if($env:COMPUTERNAME -eq 'DESKTOP-VTID97L'){'D:\Archives\Backups\20261015 1Remote\storage-update-20261016'}else{Join-Path $serviceRoot 'tmp\storage-update-20261016'}
if(!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $configPath)){throw 'Service files missing'}
$manifest=Get-Content -LiteralPath (Join-Path $package 'manifest.json') -Raw | ConvertFrom-Json
$allowed=@('xRemote.ControlHost.dll','xRemote.ControlHost.pdb','Read-Hardware.ps1')
foreach($entry in $manifest){if($entry.name -notin $allowed -or (Get-FileHash -LiteralPath (Join-Path $package $entry.name) -Algorithm SHA256).Hash -ne $entry.sha256){throw 'Update payload mismatch'}}
if($manifest.Count -ne $allowed.Count){throw 'Update payload incomplete'}
$oldHash=(Get-FileHash -LiteralPath (Join-Path $target 'xRemote.ControlHost.dll') -Algorithm SHA256).Hash
$newHash=($manifest | Where-Object name -eq 'xRemote.ControlHost.dll').sha256
if($oldHash -eq $newHash){Write-Output 'Already updated';exit 0}
if($oldHash -notin @('999BF5B042AFE9064E8CD938CA9226EB989D4F586C69FBAB55CD432A7BF9F15B','C0F49C79661755B0C6938F35556B16A1563788F2C3BB2844E1B7D004EB7C54EF')){throw 'Service binary differs from the verified installed baselines; update stopped'}
New-Item -ItemType Directory -Force -Path $work | Out-Null
$rollback=Join-Path $work ('rollback-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $rollback | Out-Null
$before=@{};foreach($file in Get-ChildItem -LiteralPath $configuration -File){if($file.Name -ne 'operations.log'){$before[$file.Name]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}}
foreach($name in $allowed){$path=Join-Path $target $name;if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination $rollback}}
try{
    Stop-Service xRemoteControl -ErrorAction Stop
    (Get-Service xRemoteControl).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(20))
    foreach($entry in $manifest){Copy-Item -LiteralPath (Join-Path $package $entry.name) -Destination (Join-Path $target $entry.name)}
    Start-Service xRemoteControl
    (Get-Service xRemoteControl).WaitForStatus('Running',[TimeSpan]::FromSeconds(20))
    foreach($name in $before.Keys){if((Get-FileHash -LiteralPath (Join-Path $configuration $name) -Algorithm SHA256).Hash -ne $before[$name]){throw 'Protected configuration changed'}}
    foreach($entry in $manifest){if((Get-FileHash -LiteralPath (Join-Path $target $entry.name) -Algorithm SHA256).Hash -ne $entry.sha256){throw 'Installed payload mismatch'}}
    [ordered]@{machine=$env:COMPUTERNAME;state='Running';updatedAt=[DateTime]::UtcNow.ToString('o');oldHash=$oldHash;newHash=$newHash;configurationUnchanged=$true;rollback=$rollback} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'receipt.json') -Encoding UTF8
    Write-Output ('Updated '+$env:COMPUTERNAME+'; configuration unchanged')
}catch{
    Stop-Service xRemoteControl -ErrorAction SilentlyContinue
    foreach($file in Get-ChildItem -LiteralPath $rollback -File){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $target $file.Name)}
    Start-Service xRemoteControl -ErrorAction SilentlyContinue
    [ordered]@{machine=$env:COMPUTERNAME;state='Failed';error=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'receipt.json') -Encoding UTF8
    throw
}
