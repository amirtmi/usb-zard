$ErrorActionPreference='Stop'
$guard=Get-Content "$PSScriptRoot\..\Backend\Guard.ps1" -Raw
$worker=Get-Content "$PSScriptRoot\..\Backend\Worker.ps1" -Raw
$worker=$worker.Replace('$r = $script:RequestJson | ConvertFrom-Json','$r = $script:request')
$script:mutations=0;$script:raw=$false;$script:ro=$false;$script:formatFails=$false;$script:scriptExit=0;$script:commandCount=0;$script:testCount=0
function Get-Disk {param($Number) [pscustomobject]@{Number=7;IsBoot=$false;IsSystem=$false;BusType='USB';Size=8000000000;UniqueId='unique';Path='path';SerialNumber='serial';FriendlyName='Mock USB';IsReadOnly=$script:ro;IsOffline=$false;HealthStatus='Healthy'}}
function Get-Partition {param($DiskNumber,$PartitionNumber) [pscustomobject]@{PartitionNumber=1;IsBoot=$false;IsSystem=$false;DriveLetter='E';IsReadOnly=$null;Size=8000000000;Offset=0}}
function Get-Volume {param([Parameter(ValueFromPipeline=$true)]$InputObject) process {[pscustomobject]@{UniqueId='volume';Path='volume';DriveLetter='E';Size=if($script:raw){0}else{8000000000};FileSystemType=if($script:raw){'Unknown'}else{'exFAT'};FileSystemLabel='TEST'}}}
function Get-CimInstance {param($ClassName,$Filter) if($ClassName -eq 'Win32_Volume'){[pscustomobject]@{DeviceID='volume'}}else{[pscustomobject]@{PNPDeviceID='USB\VID_1234&PID_5678'}}}
function Get-PhysicalDisk {}
function Get-Item {param($Path) $null}
function Test-Path {param($Path) $false}
function Get-NativeState {param($d) @{Writable=$true;WriteError=0;Read=$true;FirstBlockAllZero=$true}}
function Get-VolumeFlag {param($letter) $false}
function Test-FileRoundtrip {param($r,$d) $script:testCount++;@{Verified=$true;Bytes=1048576;CleanedUp=$true}}
function Set-Disk {param([Parameter(ValueFromPipeline=$true)]$InputObject,$IsReadOnly) process {$script:mutations++;$script:ro=$false}}
function Set-Partition {throw 'Unexpected unsupported Set-Partition'}
function Update-HostStorageCache {$script:mutations++}
function Format-Volume {param([Parameter(ValueFromPipeline=$true)]$InputObject,$FileSystem,$NewFileSystemLabel,$Confirm) process {$script:mutations++;if($script:formatFails){throw 'Invalid Parameter'};$script:raw=$false}}
function Invoke-DiskPart {param($commands) $script:commandCount++;@{ExitCode=$script:scriptExit;Output='Localized error text'} }
function Invoke-CimMethod {param($InputObject,$MethodName,$Arguments) $script:mutations++;[pscustomobject]@{ReturnValue=0}}
function New-Item {throw 'No registry changes in tests'}
$script:request=[pscustomobject]@{Action='Scan';Consent='';Disk=[pscustomobject]@{Number=7;Size=8000000000;UniqueId='unique';Path='path';Serial='serial'};Volume=[pscustomobject]@{PartitionNumber=1;Letter='E';Id='volume'};FileSystem='exFAT'}
$block=[ScriptBlock]::Create($guard+[Environment]::NewLine+$worker)
function Run {(& $block|Out-String|ConvertFrom-Json)}
function Check($ok,$name){if(!$ok){throw "FAIL $name"};Write-Output "PASS $name"}
$result=Run;Check ($result.Success -and @($result.Data.Devices).Count -eq 1 -and $result.Data.Devices[0].Number -eq 7) 'scan array shape'
Check ($null -eq $result.Data.Devices[0].Volumes[0].ReadOnly) 'unsupported partition flag stays null'
$script:request.Action='Diagnose';$result=Run
Check ($result.Success -and $result.Data.Native.Writable -and $result.Data.Volumes.Count -eq 1) 'native and volume evidence included'
foreach($action in @('ClearDisk','ClearVolume','Rescan','Check','Repair','Format','Policy','Probe')){
 $script:request.Action=$action;$script:request.Consent='';$before=$script:mutations;$result=Run
 Check (!$result.Success -and $script:mutations -eq $before -and $script:commandCount -eq 0 -and $script:testCount -eq 0) "consent gate $action"
}
$script:request.Action='ClearDisk';$script:request.Consent='CONFIRM ClearDisk DISK 7'
$before=$script:mutations;$result=Run;Check ($result.Code -eq 'NO_CHANGE' -and $script:mutations -eq $before) 'no false repair for absent disk lock'
$script:ro=$true;$result=Run;Check ($result.Success -and !$script:ro) 'clear a real software flag'
$script:raw=$true
foreach($action in @('Check','Repair')){
 $script:request.Action=$action;$script:request.Consent="CONFIRM $action DISK 7";$before=$script:mutations;$result=Run
 Check (!$result.Success -and $result.Code -like '*RAW_FILESYSTEM*' -and $script:mutations -eq $before) "RAW gate $action"
}
$script:request.Action='Format';$script:request.Consent='ERASE DISK 7 E:';$script:formatFails=$true;$result=Run
Check (!$result.Success -and $result.Code -like '*FORMAT_NOT_VERIFIED*' -and $script:testCount -eq 0) 'exit zero does not mask failed RAW format'
$script:formatFails=$false;$result=Run
Check ($result.Success -and $script:testCount -eq 1 -and !$script:raw) 'format requires filesystem and file roundtrip'
$script:formatFails=$true;$script:raw=$false;$script:scriptExit=87;$before=$script:testCount;$result=Run
Check (!$result.Success -and $script:testCount -eq $before) 'existing matching filesystem cannot mask failed format'
Write-Output '18 worker assertions passed; no real storage access.'
