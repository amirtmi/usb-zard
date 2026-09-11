$ErrorActionPreference='Stop'
. "$PSScriptRoot\..\Backend\Guard.ps1"
$script:count=0
function Good-Disk { return [pscustomobject]@{Number=7;IsBoot=$false;IsSystem=$false;BusType='USB';Size=8000000000;UniqueId='unique';Path='path';SerialNumber='serial'} }
$script:disk=Good-Disk
$script:part=[pscustomobject]@{PartitionNumber=1;IsBoot=$false;IsSystem=$false;DriveLetter='E'}
function Get-Disk { param($Number) return $script:disk }
function Get-Partition { param($DiskNumber,$PartitionNumber) return $script:part }
function Get-Volume { param([Parameter(ValueFromPipeline=$true)]$InputObject) process { [pscustomobject]@{UniqueId='volume'} } }
$r=[pscustomobject]@{Action='ClearDisk';Consent='CONFIRM ClearDisk DISK 7';Disk=[pscustomobject]@{Number=7;Size=8000000000;UniqueId='unique';Path='path';Serial='serial'};Volume=[pscustomobject]@{PartitionNumber=1;Letter='E';Id='volume'}}
function Reject($name,[scriptblock]$body) { $rejected=$false; try { & $body | Out-Null } catch { $rejected=$true }; if(!$rejected){throw "FAIL $name"}; $script:count++; Write-Output "PASS $name" }
Assert-Target $r | Out-Null
Assert-Consent $r
Assert-Volume $r $script:disk | Out-Null
$script:disk.IsSystem=$true
Reject 'System disk denied' { Assert-Target $r }
$script:disk=Good-Disk; $script:disk.BusType='NVMe'
Reject 'Internal bus denied' { Assert-Target $r }
$script:disk=Good-Disk; $script:disk.UniqueId='swapped'
Reject 'Swapped device denied' { Assert-Target $r }
$script:disk=Good-Disk; $script:disk.Path=''
Reject 'Unknown identity denied' { Assert-Target $r }
$script:disk=Good-Disk; $script:part.IsBoot=$true
Reject 'Boot partition denied' { Assert-Target $r }
$script:part.IsBoot=$false; $r.Volume.Id='other'
Reject 'Changed volume denied' { Assert-Volume $r $script:disk }
$r.Volume.Id='volume'; $r.Volume.Letter='C'
Reject 'System letter denied' { Assert-Volume $r $script:disk }
$r.Volume.Letter='E'; $r.Consent=''
Reject 'Missing consent denied' { Assert-Consent $r }
$r.Action='Format'; $r.Consent='CONFIRM Format DISK 7'
Reject 'Format requires erase token' { Assert-Consent $r }
$r.Consent='ERASE DISK 7 E:'
Assert-Consent $r
Write-Output "$script:count rejection tests and valid request checks passed; no hardware access."
