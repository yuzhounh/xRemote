# Get the current working directory
$originalDirectory = Get-Location

# Set the current directory to the script directory
Set-Location $PSScriptRoot

# Get the file
$filePath = "..\Ui\AppVersion.cs"
$fileContent = Get-Content $filePath

# Extract the version numbers and pre-release string from the file
$major = [int] ($fileContent | Select-String 'public const uint Major = (\d+)').Matches[0].Groups[1].Value
$minor = [int] ($fileContent | Select-String 'public const uint Minor = (\d+)').Matches[0].Groups[1].Value
$patch = [int] ($fileContent | Select-String 'public const uint Patch = (\d+)').Matches[0].Groups[1].Value
$build = [int] ($fileContent | Select-String 'public const uint Build = (\d+)').Matches[0].Groups[1].Value
$preRelease = ($fileContent | Select-String 'public const string PreRelease = "(.*)";').Matches[0].Groups[1].Value
$forkRevision = [int] ($fileContent | Select-String 'public const uint ForkRevision = (\d+)').Matches[0].Groups[1].Value
if ($forkRevision -lt 1 -or $forkRevision -gt 99) { throw 'Release revision must be 1-99; reset it to 1 when integrating a new upstream baseline.' }

# Construct the version string
if ($preRelease -eq "" -and $build -eq 0) {
    $versionString = "$major.$minor.$patch"
} elseif ($preRelease -eq "") {
    $versionString = "$major.$minor.$patch.$build"
} else {
    $versionString = "$major.$minor.$patch.$build-$preRelease"
}

# Set the current directory back to the original location
Set-Location $originalDirectory

# Write-Output $versionString
# Write-Output "::set-output name=Version::$versionString"
"UpstreamVersion=$versionString" >> $env:GITHUB_ENV
"ForkRevision=$forkRevision" >> $env:GITHUB_ENV
"BuildVersion=$versionString-r$($forkRevision.ToString('D2'))" >> $env:GITHUB_ENV
"ProductName=xRemote" >> $env:GITHUB_ENV
"PreRelease=$preRelease" >> $env:GITHUB_ENV

# echo "Version=$versionString" | Out-File -FilePath $Env:GITHUB_ENV -Encoding utf8 -Append

