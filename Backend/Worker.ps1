$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::InputEncoding = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$r = $script:RequestJson | ConvertFrom-Json
function Get-VolumeRows($d) {
 $rows=@()
 foreach($p in @(Get-Partition -DiskNumber $d.Number)){
  $v=$p|Get-Volume -ErrorAction SilentlyContinue
  if($v -and "$($v.DriveLetter)" -match '^[A-Z]$'){
   $rows+=@{PartitionNumber=[int]$p.PartitionNumber;Letter="$($v.DriveLetter)";Id="$($v.UniqueId)";FileSystem="$($v.FileSystemType)";ReadOnly=$p.IsReadOnly;PartitionSize=[uint64]$p.Size;Offset=[uint64]$p.Offset;VolumeSize=[uint64]$v.Size;Label="$($v.FileSystemLabel)"}
  }
 }
 return $rows
}
function Scan-Devices {
    $result = @()
    foreach ($d in @(Get-Disk)) {
        if ("$($d.BusType)" -notin @('USB','SD','MMC') -or $d.IsBoot -or $d.IsSystem) { continue }
        $warnings = @()
        $volumes = @()
        try {
            foreach ($p in @(Get-Partition -DiskNumber $d.Number)) {
                $v = $p | Get-Volume -ErrorAction SilentlyContinue
                if ($v -and "$($v.DriveLetter)" -match '^[A-Z]$') {
                    $volumes += @{ PartitionNumber=[int]$p.PartitionNumber; Letter="$($v.DriveLetter)"; Id="$($v.UniqueId)"; FileSystem="$($v.FileSystemType)"; Label="$($v.FileSystemLabel)"; ReadOnly=$p.IsReadOnly; PartitionSize=[uint64]$p.Size; Offset=[uint64]$p.Offset; VolumeSize=[uint64]$v.Size }
                }
            }
        } catch { $warnings += "$_" }
        $pnp = ''; $usb = ''
        try {
            $w = Get-CimInstance Win32_DiskDrive -Filter "Index=$($d.Number)"
            $pnp = "$($w.PNPDeviceID)"
            $parent = $pnp
            for ($i=0; $i -lt 8 -and $parent; $i++) {
                if ($parent -match 'VID_[0-9A-F]{4}&PID_[0-9A-F]{4}') { $usb = $Matches[0]; break }
                $parent = (Get-PnpDeviceProperty -InstanceId $parent -KeyName 'DEVPKEY_Device_Parent' -ErrorAction Stop).Data
            }
        } catch { $warnings += "PNP: $_" }
        $result += @{ Number=[int]$d.Number; UniqueId="$($d.UniqueId)"; Path="$($d.Path)"; Serial="$($d.SerialNumber)".Trim(); Name="$($d.FriendlyName)"; Bus="$($d.BusType)"; Size=[uint64]$d.Size; ReadOnly=[bool]$d.IsReadOnly; Offline=[bool]$d.IsOffline; Health="$($d.HealthStatus)"; PnpId=$pnp; UsbId=$usb; Volumes=@($volumes); Warnings=@($warnings) }
    }
    return $result
}
try {
    $data = $null; $code='OK'; $messageFa=''; $messageEn=''
    if ($r.Action -eq 'Scan') {
 $data=@{Devices=@(Scan-Devices)}
 if($data.Devices.Count -eq 0){$code='NO_DEVICES';$messageFa='دستگاه USB یا SD پیدا نشد؛ اتصال، کارت‌خوان و دسترسی مدیر را بررسی و دوباره جستجو کنید.';$messageEn='No eligible USB/SD device found. Check connection, reader and administrator access, then rescan.'}
 else {$messageFa="$($data.Devices.Count) دستگاه پیدا شد؛ یکی را انتخاب و تشخیص را اجرا کنید.";$messageEn="$($data.Devices.Count) device(s) found. Select a device and run diagnostics."}
 }
    else {
        $d = Assert-Target $r
        if ($r.Action -eq 'Diagnose') {
            $policy = $null; $policyError = ''; $rules = @(); $health = $null; $healthError = ''
            try {
                $key = Get-Item 'HKLM:\SYSTEM\CurrentControlSet\Control\StorageDevicePolicies' -ErrorAction SilentlyContinue
                if ($key) { $policy = $key.GetValue('WriteProtect', $null) }
                foreach ($root in @('HKLM:\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices','HKCU:\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices')) {
                    if (Test-Path $root) {
                        foreach ($k in @((Get-Item $root)) + @(Get-ChildItem $root -Recurse)) {
                            foreach ($name in $k.GetValueNames()) { if ($name -match '^Deny') { $rules += @{Path=$k.Name; Name=$name; Value=$k.GetValue($name)} } }
                        }
                    }
                }
            } catch { $policyError = "$_" }
            try {
                $physical = @(Get-PhysicalDisk | Where-Object { "$($_.UniqueId)" -ceq "$($d.UniqueId)" })
                if ($physical.Count -ne 1) { throw 'No unambiguous physical-disk identity match. SMART unavailable.' }
                $health = $physical[0] | Get-StorageReliabilityCounter | Select-Object Temperature,PowerOnHours,Wear,ReadErrorsTotal,ReadErrorsUncorrected,WriteErrorsTotal,WriteErrorsUncorrected
                if (!$health) { throw 'Reliability counters unavailable.' }
            } catch { $healthError = "$_" }
            $data = @{ Native=(Get-NativeState $d); Volumes=@(Get-VolumeRows $d); Disk=$d | Select-Object Number,IsReadOnly,IsOffline,HealthStatus,OperationalStatus,Size,LogicalSectorSize,PhysicalSectorSize,PartitionStyle; WriteProtect=$policy; PolicyError=$policyError; GroupPolicies=@($rules); Reliability=$health; ReliabilityError=$healthError; HardwareLock='Unknown: physical switch and firmware cannot be distinguished reliably by this API.' }
        } else {
            Assert-Consent $r
            switch ($r.Action) {
                'ClearDisk' {
                    if($d.IsReadOnly){$d | Set-Disk -IsReadOnly $false -ErrorAction Stop}
                    else {$code='NO_CHANGE';$messageFa='ویژگی فقط‌خواندنی دیسک از قبل خاموش بود؛ هیچ قفلی با این عمل رفع نشد.';$messageEn='Disk read-only was already off. This action did not repair a write lock.'}
                    $after=Assert-Target $r
                    $data=@{IsReadOnly=[bool]$after.IsReadOnly;Native=(Get-NativeState $after)}
                    if($after.IsReadOnly){throw 'READONLY_PERSISTS'}
                }
                'ClearVolume' {
                    $target=Assert-Volume $r $d
                    $native=Get-NativeState $d
                    $before=Get-VolumeFlag $r.Volume.Letter
                    if($before -eq $false){$code='NO_CHANGE';$messageFa='فایل‌سیستم فقط‌خواندنی نیست؛ تغییری لازم نبود.';$messageEn='Filesystem read-only flag is off; no change needed.';$data=@{ReadOnly=$before}}
                    else {
                     $transcript=Invoke-DiskPart @("select disk $($d.Number)","select volume $($r.Volume.Letter)",'attributes volume clear readonly')
                     $d=Assert-Target $r;Assert-Volume $r $d|Out-Null
                     $after=Get-VolumeFlag $r.Volume.Letter
                     $data=@{DiskPart=$transcript;ReadOnly=$after;Scope='On basic MBR this attribute can affect all volumes on the disk.'}
                     if($transcript.ExitCode -ne 0 -or $after -ne $false){throw 'VOLUME_CLEAR_UNVERIFIED: the volume attribute change could not be verified.'}
                    }
                }
                'Rescan' { Update-HostStorageCache; $data=@{Note='Storage cache refreshed; safely eject/reinsert manually to remount.'};$messageFa='فهرست ذخیره‌سازی تازه شد؛ برای اتصال مجدد از خروج امن ویندوز استفاده کنید.';$messageEn='Storage cache refreshed. Use safe removal and reconnect for a remount.' }
                'Probe' {
                    $data=Test-FileRoundtrip $r $d
                    if(!$data.Verified){throw 'WRITE_VERIFY_FAILED'}
                    $code='WRITE_VERIFIED';$messageFa='نوشتن و بازخوانی فایل آزمایشی ۱ MiB با SHA-256 تطبیق داشت؛ سلامت کل ظرفیت تأیید نشده است.';$messageEn='1 MiB write/read SHA-256 matched; this does not certify full capacity or long-term retention.'
                }
                'Policy' {
                    $keyPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\StorageDevicePolicies'
                    $key = Get-Item $keyPath -ErrorAction SilentlyContinue
                    $previous = $null; $kind = $null
                    if ($key -and $key.GetValueNames() -contains 'WriteProtect') { $previous = $key.GetValue('WriteProtect'); $kind = "$($key.GetValueKind('WriteProtect'))" }
                    $backup = @{ Time=[DateTime]::UtcNow.ToString('o'); Path=$keyPath; KeyExisted=[bool]$key; ValueExisted=($null -ne $previous); Previous=$previous; Kind=$kind }
                    $dir = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'UsbWriteGuard\Backups'
                    New-Item -ItemType Directory -Path $dir -Force | Out-Null
                    $backupFile = Join-Path $dir ("policy-" + [Guid]::NewGuid().ToString('N') + '.json')
                    $backup | ConvertTo-Json | Set-Content -LiteralPath $backupFile -Encoding UTF8
                    if (!(Test-Path $keyPath)) { New-Item -Path $keyPath -Force | Out-Null }
                    New-ItemProperty -Path $keyPath -Name WriteProtect -PropertyType DWord -Value 0 -Force | Out-Null
                    $data = @{ Previous=$previous; Current=(Get-ItemPropertyValue -Path $keyPath -Name WriteProtect); Backup=$backupFile; Scope='Machine-wide; domain/MDM policies are not modified.' }
                }
                { $_ -in @('Check','Repair') } {
                    $target=Assert-Volume $r $d
                    if("$($target.Volume.FileSystemType)" -notin @('FAT','FAT32','NTFS','exFAT') -or $target.Volume.Size -le 0){throw 'RAW_FILESYSTEM: CHKDSK cannot repair an unrecognized/RAW filesystem. Recover data or explicitly format first.'}
                    $letter="$($target.Volume.DriveLetter):"
                    $cim=@(Get-CimInstance Win32_Volume -Filter "DriveLetter='$letter'")
                    if($cim.Count -ne 1 -or "$($cim[0].DeviceID)" -ine "$($target.Volume.Path)"){throw 'GUARD_CIM_VOLUME'}
                    $repair=$r.Action -eq 'Repair'
                    $result=Invoke-CimMethod -InputObject $cim[0] -MethodName Chkdsk -Arguments @{FixErrors=$repair;VigorousIndexCheck=$true;SkipFolderCycle=$false;ForceDismount=$false;RecoverBadSectors=$false;OkToRunAtBootUp=$false}
                    $descriptions=@('Completed','Scheduled for reboot','Unsupported filesystem','Unknown filesystem','No media reported by WMI','Unknown WMI error')
                    $index=[int]$result.ReturnValue
                    $data=@{ReturnValue=$index;Description=if($index -lt $descriptions.Count){$descriptions[$index]}else{'Unknown return code'};FixErrors=$repair}
                    if($index -ne 0){throw "CHKDSK_$index"}
                    $messageFa='بررسی CHKDSK تکمیل شد؛ این نتیجه آزمون سلامت سخت‌افزار نیست.';$messageEn='CHKDSK completed; this is not a hardware health test.'
                }
                'Format' {
                    $target=Assert-Volume $r $d
                    if("$($r.FileSystem)" -notin @('exFAT','NTFS','FAT32')){throw 'GUARD_FILESYSTEM'}
                    if($r.FileSystem -eq 'FAT32' -and $target.Partition.Size -gt 32GB){throw 'FAT32_LIMIT'}
                    $attempts=@();$formatAccepted=$false
                    try {
                     $target.Volume|Format-Volume -FileSystem $r.FileSystem -NewFileSystemLabel 'USB_ZARD' -Confirm:$false -ErrorAction Stop|Out-Null
                     $attempts+=@{Method='Format-Volume';Completed=$true};$formatAccepted=$true
                    }catch {
                     $attempts+=@{Method='Format-Volume';Completed=$false;Message="$($_.Exception.Message)";ErrorId="$($_.FullyQualifiedErrorId)"}
                     $d=Assert-Target $r;Assert-Volume $r $d|Out-Null
                     $diskpartResult=Invoke-DiskPart @("select disk $($d.Number)","select volume $($r.Volume.Letter)","format fs=$($r.FileSystem) label=USB_ZARD quick")
                     $attempts+=@{Method='DiskPart /s';Result=$diskpartResult};$formatAccepted=($diskpartResult.ExitCode -eq 0)
                    }
                    $d=Assert-Target $r
                    $fresh=Assert-Volume $r $d
                    $data=@{Attempts=$attempts;FileSystem="$($fresh.Volume.FileSystemType)";VolumeSize=[uint64]$fresh.Volume.Size}
                    if(!$formatAccepted -or "$($fresh.Volume.FileSystemType)" -ine "$($r.FileSystem)" -or $fresh.Volume.Size -le 0){throw 'FORMAT_NOT_VERIFIED: no usable requested filesystem appeared.'}
                    $data.Probe=Test-FileRoundtrip $r $d
                    if(!$data.Probe.Verified){throw 'FORMAT_WRITE_VERIFY_FAILED'}
                    $code='FORMAT_VERIFIED';$messageFa='فرمت و آزمون نوشتن/بازخوانی ۱ MiB موفق بود. همه داده‌های پارتیشن پاک شدند.';$messageEn='Format and a 1 MiB write/read test succeeded. Previous volume data was erased.'
                }
                default { throw 'GUARD_ACTION: unsupported action.' }
            }
        }
    }
    @{ Success=$true; Code=$code; MessageFa=$messageFa; MessageEn=$messageEn; Data=$data } | ConvertTo-Json -Depth 12 -Compress
} catch {
    @{ Success=$false; Code="$($_.FullyQualifiedErrorId)"; Message="$($_.Exception.Message)"; HResult=$_.Exception.HResult; NativeErrorCode=$_.Exception.NativeErrorCode; StatusCode="$($_.Exception.StatusCode)"; Category="$($_.CategoryInfo.Category)"; Data=$data } | ConvertTo-Json -Depth 12 -Compress
}
