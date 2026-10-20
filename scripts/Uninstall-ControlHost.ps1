$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(!(New-Object Security.Principal.WindowsPrincipal($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run in an elevated PowerShell window'}
if(Get-Service xRemoteControl -ErrorAction SilentlyContinue){
    Stop-Service xRemoteControl
    & sc.exe delete xRemoteControl
    if($LASTEXITCODE -ne 0){throw 'Service removal failed'}
}
Get-NetFirewallRule -Name 'xRemote-Control-EasyTier' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
Write-Output 'Control service removed. Program files and private pairing configuration are preserved; RDP and EasyTier were not disabled.'
