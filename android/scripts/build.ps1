param([switch]$SkipNativePreparation)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
& python (Join-Path $PSScriptRoot 'prepare-vnc.py')
if($LASTEXITCODE -ne 0){throw 'VNC dependency preparation failed'}
if(!$SkipNativePreparation){
    & python (Join-Path $PSScriptRoot 'prepare-native.py')
    if($LASTEXITCODE -ne 0){throw 'Native dependency preparation failed'}
}
if(!(Test-Path (Join-Path $env:USERPROFILE '.android\xremote\signing.properties'))){
    throw 'The dedicated xRemote signing configuration is missing from the user profile'
}
if(!(Test-Path (Join-Path $projectRoot 'local.properties'))){
    $sdkPath=Join-Path $env:LOCALAPPDATA 'Android\Sdk'
    if(!(Test-Path $sdkPath)){throw 'Android SDK not found; create local.properties with sdk.dir'}
    [IO.File]::WriteAllText((Join-Path $projectRoot 'local.properties'),('sdk.dir='+$sdkPath.Replace('\','/').Replace(':','\:')),(New-Object Text.UTF8Encoding($false)))
}
if(!(Test-Path 'C:\t')){New-Item -ItemType Directory -Path 'C:\t' | Out-Null}
$env:JAVA_TOOL_OPTIONS='-Djdk.net.unixdomain.tmpdir=C:\t -XX:ActiveProcessorCount=2'
Push-Location $projectRoot
try {
    & .\gradlew.bat --no-daemon --max-workers=2 assembleRelease lintRelease
    if($LASTEXITCODE -ne 0){throw 'Build or lint failed'}
} finally {Pop-Location}
$apk=Join-Path $projectRoot 'app\build\outputs\apk\release\app-release.apk'
$sdk=Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$tools=Get-ChildItem (Join-Path $sdk 'build-tools') -Directory | Sort-Object Name -Descending | Select-Object -First 1
& (Join-Path $tools.FullName 'apksigner.bat') verify --verbose $apk
if($LASTEXITCODE -ne 0){throw 'Signature verification failed'}
Get-FileHash $apk -Algorithm SHA256
Write-Output $apk

