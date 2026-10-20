param(
    [Parameter(Mandatory=$true)][string]$PackageDirectory,
    [Parameter(Mandatory=$true)][ValidateSet('10.126.126.2','10.126.126.14','10.126.126.12','10.126.126.11','10.126.126.13')][string]$Address,
    [ValidateSet('black','lock')][string]$PrivacyMode='black',
    [string]$InstallRoot
)
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(!(New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run in an elevated PowerShell window'}
if(Get-Service xRemoteControl -ErrorAction SilentlyContinue){throw 'Existing service preserved; review an explicit update before replacing it'}
$source=(Resolve-Path -LiteralPath $PackageDirectory).Path
if(!(Test-Path -LiteralPath (Join-Path $source 'xRemote.ControlHost.exe'))){throw 'ControlHost package missing'}
if(!(Get-NetIPAddress -IPAddress $Address -ErrorAction SilentlyContinue)){throw 'Selected EasyTier address is not assigned to this computer'}
Get-Service AIComputeEasyTier -ErrorAction Stop | Out-Null
if(!$InstallRoot){
    $InstallRoot=if($Address -eq '10.126.126.2'){'D:\Archives\20261015 1Remote\control-private\service'}else{'D:\WangJing\xRemote'}
}
$InstallRoot=[IO.Path]::GetFullPath($InstallRoot)
$target=Join-Path $InstallRoot 'code\control-host'
$configuration=Join-Path $InstallRoot 'data\control'
if((Test-Path $target) -or (Test-Path $configuration)){throw 'Existing files preserved; target directories must be absent for first installation'}
New-Item -ItemType Directory -Path $target,$configuration | Out-Null
$acl=New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true,$false)
foreach($sid in @('S-1-5-18','S-1-5-32-544')){
    $account=New-Object Security.Principal.SecurityIdentifier($sid)
    $rule=New-Object Security.AccessControl.FileSystemAccessRule($account,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
    $acl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $target -AclObject $acl
Set-Acl -LiteralPath $configuration -AclObject $acl
Get-ChildItem -LiteralPath $source | Copy-Item -Destination $target -Recurse
$exe=Join-Path $target 'xRemote.ControlHost.exe'
& $exe --init $Address $configuration 24443 $PrivacyMode
if($LASTEXITCODE -ne 0){throw 'Configuration creation failed'}
$command='"'+$exe+'" --config "'+(Join-Path $configuration 'server.json')+'"'
& sc.exe create xRemoteControl binPath= $command start= auto depend= AIComputeEasyTier DisplayName= 'xRemote Session Control'
if($LASTEXITCODE -ne 0){throw 'Service registration failed'}
& sc.exe failure xRemoteControl reset= 86400 actions= restart/5000/restart/15000/restart/60000
if($LASTEXITCODE -ne 0){throw 'Service failure recovery configuration failed'}
New-NetFirewallRule -Name 'xRemote-Control-EasyTier' -DisplayName 'xRemote control private network' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 24443 -LocalAddress $Address -RemoteAddress '10.126.126.0/24' -Program $exe -Profile Any | Out-Null
Start-Service xRemoteControl
Write-Output ('Pairing file (contains a secret; keep private): '+(Join-Path $configuration 'pairing.json'))
