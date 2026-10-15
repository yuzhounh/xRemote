$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskDependency = Join-Path $taskRoot 'Shawn.Utils'
$taskPatch = Join-Path $taskRoot 'patches\Shawn.Utils-xRemote.patch'
& git -C $taskDependency apply --check $taskPatch 2>$null
if ($LASTEXITCODE -eq 0) {
    & git -C $taskDependency apply $taskPatch
    if ($LASTEXITCODE -ne 0) { throw 'Could not apply the xRemote dependency patch' }
} else {
    & git -C $taskDependency apply --reverse --check $taskPatch 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Dependency contents do not match the xRemote patch; initialize the pinned submodule revision first' }
}
Write-Output 'xRemote dependency patch is applied.'
