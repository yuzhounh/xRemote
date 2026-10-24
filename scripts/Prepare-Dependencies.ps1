$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
foreach ($taskEntry in @(
    @{ Dependency='Shawn.Utils'; Patch='Shawn.Utils-xRemote.patch' },
    @{ Dependency='Shawn.Utils'; Patch='Shawn.Utils-net10.patch' },
    @{ Dependency='Dragablz'; Patch='Dragablz-net10.patch' }
)) {
    $taskDependency = Join-Path $taskRoot $taskEntry.Dependency
    $taskPatch = Join-Path $taskRoot ('patches\' + $taskEntry.Patch)
    & git -C $taskDependency apply --check $taskPatch 2>$null
    if ($LASTEXITCODE -eq 0) {
        & git -C $taskDependency apply $taskPatch
        if ($LASTEXITCODE -ne 0) { throw ('Could not apply dependency patch: ' + $taskEntry.Patch) }
    } else {
        & git -C $taskDependency apply --reverse --check $taskPatch 2>$null
        if ($LASTEXITCODE -ne 0) { throw ('Dependency contents do not match ' + $taskEntry.Patch + '; initialize the pinned submodule revision first') }
    }
}
Write-Output 'xRemote dependency patch is applied.'
