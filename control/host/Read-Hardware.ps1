param([switch]$StorageOnly)
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$issues=[Collections.Generic.List[string]]::new()
function Read-Section([string]$name,[scriptblock]$read) {
    try { & $read } catch { $issues.Add($name) }
}
if(!$StorageOnly){
$cpu=@(Read-Section 'CPU' { Get-CimInstance Win32_Processor -OperationTimeoutSec 15 })
$ram=@(Read-Section 'Memory' { Get-CimInstance Win32_PhysicalMemory -OperationTimeoutSec 5 })
$os=Read-Section 'Windows' { Get-CimInstance Win32_OperatingSystem -OperationTimeoutSec 5 }
$version=Read-Section 'WindowsVersion' { Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' }
$gpu=@(Read-Section 'GPU' { Get-CimInstance Win32_VideoController -OperationTimeoutSec 5 | Where-Object { $_.PNPDeviceID -like 'PCI\*' -and $_.Name -notmatch 'Remote|Indirect|Basic Render' } | ForEach-Object { $_.Name.Trim() } | Sort-Object -Unique })
}
$diskObjects=@(Read-Section 'Disks' { Get-CimInstance Win32_DiskDrive -OperationTimeoutSec 5 | Where-Object { $_.Size -gt 0 } | Sort-Object Index })
$volumeObjects=@(Read-Section 'Volumes' { Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' -OperationTimeoutSec 5 | Sort-Object DeviceID })
$mapping=@{}
$disks=@(foreach($disk in $diskObjects){
    Read-Section 'DiskMapping' {
        foreach($partition in @(Get-CimAssociatedInstance -InputObject $disk -Association Win32_DiskDriveToDiskPartition -OperationTimeoutSec 5)){
            foreach($volume in @(Get-CimAssociatedInstance -InputObject $partition -Association Win32_LogicalDiskToPartition -OperationTimeoutSec 5)){
                $letter=[string]$volume.DeviceID
                if(!$mapping.ContainsKey($letter)){$mapping[$letter]=[Collections.Generic.List[int]]::new()}
                if(!$mapping[$letter].Contains([int]$disk.Index)){$mapping[$letter].Add([int]$disk.Index)}
            }
        }
    }
    [ordered]@{index=[int]$disk.Index;model=([string]$disk.Model).Trim();sizeBytes=[long]$disk.Size}
})
$volumes=@(foreach($volume in $volumeObjects){
    $size=$null;$free=$null
    if($null -ne $volume.Size -and $volume.Size -gt 0){$size=[long]$volume.Size}
    if($null -ne $volume.FreeSpace -and $null -ne $size -and $volume.FreeSpace -ge 0 -and $volume.FreeSpace -le $size){$free=[long]$volume.FreeSpace}
    $indices=@();if($mapping.ContainsKey([string]$volume.DeviceID)){$indices=@($mapping[[string]$volume.DeviceID])}
    [ordered]@{drive=[string]$volume.DeviceID;label=[string]$volume.VolumeName;fileSystem=[string]$volume.FileSystem;sizeBytes=$size;freeBytes=$free;diskNumbers=$indices}
})
if($StorageOnly){
    [ordered]@{schema=1;machine=$env:COMPUTERNAME;observedAt=[DateTime]::UtcNow.ToString('o');disks=$disks;volumes=$volumes;unavailable=@($issues | Sort-Object -Unique)} | ConvertTo-Json -Depth 8 -Compress
    exit 0
}
$build=if($os){[string]$os.BuildNumber}else{''}
if($version -and $null -ne $version.UBR -and $build){$build+='.'+[string]$version.UBR}
$memoryBytes=$null;if($ram.Count -gt 0){$memoryBytes=[long](($ram | Measure-Object Capacity -Sum).Sum)}
$windows=$null
if($os){$windows=[ordered]@{name=[string]$os.Caption;version=[string]$os.Version;displayVersion=[string]$version.DisplayVersion;build=$build;architecture=[string]$os.OSArchitecture;lastBootAt=$os.LastBootUpTime.ToUniversalTime().ToString('o')}}
[ordered]@{
    schema=1;machine=$env:COMPUTERNAME;observedAt=[DateTime]::UtcNow.ToString('o')
    cpu=[ordered]@{models=@($cpu | ForEach-Object {($_.Name -replace '\s+',' ').Trim()} | Sort-Object -Unique);sockets=$cpu.Count;cores=[int](($cpu | Measure-Object NumberOfCores -Sum).Sum);threads=[int](($cpu | Measure-Object NumberOfLogicalProcessors -Sum).Sum)}
    memoryBytes=$memoryBytes;windows=$windows;gpus=$gpu;disks=$disks;volumes=$volumes;unavailable=@($issues | Sort-Object -Unique)
} | ConvertTo-Json -Depth 8 -Compress
