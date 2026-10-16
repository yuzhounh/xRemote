$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$types = Add-Type -Path (Join-Path $projectRoot 'Ui/Utils/RdpReconnectPolicy.cs') -PassThru
$policy = $types | Where-Object Name -eq 'RdpReconnectPolicy'
$method = $policy.GetMethod('CanRetryAutomatically', [Reflection.BindingFlags]'Static,NonPublic')
$cases = @(
    @{ Name='Local disconnect or cancelled reconnect'; Reason=1; Extended=0; Expected=$false },
    @{ Name='Remote user disconnect'; Reason=2; Extended=0; Expected=$false },
    @{ Name='Server disconnect'; Reason=3; Extended=0; Expected=$false },
    @{ Name='Unknown reason'; Reason=0; Extended=0; Expected=$false },
    @{ Name='Network receive failure'; Reason=1028; Extended=0; Expected=$true },
    @{ Name='API disconnect with network outer code'; Reason=1028; Extended=1; Expected=$false },
    @{ Name='Taken over by another connection'; Reason=1028; Extended=5; Expected=$false },
    @{ Name='User RPC disconnect'; Reason=1028; Extended=11; Expected=$false }
)
foreach ($case in $cases) {
    $actual = $method.Invoke($null, [object[]]@([int]$case.Reason, [int]$case.Extended))
    if ($actual -ne $case.Expected) { throw "Failed: $($case.Name)" }
    Write-Output "PASS: $($case.Name)"
}
