function Assert-Target($r) {
    $d = Get-Disk -Number ([int]$r.Disk.Number) -ErrorAction Stop
    if ($d.IsBoot -or $d.IsSystem -or "$($d.BusType)" -notin @('USB','SD','MMC')) { throw 'GUARD_SYSTEM_OR_BUS: target is not an eligible external disk.' }
    if ($d.Size -le 0 -or [string]::IsNullOrWhiteSpace("$($d.UniqueId)") -or [string]::IsNullOrWhiteSpace("$($d.Path)")) { throw 'GUARD_IDENTITY: missing stable identity.' }
    if ("$($d.UniqueId)" -cne "$($r.Disk.UniqueId)" -or "$($d.Path)" -cne "$($r.Disk.Path)" -or "$($d.SerialNumber)".Trim() -cne "$($r.Disk.Serial)".Trim() -or [uint64]$d.Size -ne [uint64]$r.Disk.Size) { throw 'GUARD_CHANGED: device identity changed; scan again.' }
    $parts = @(Get-Partition -DiskNumber $d.Number -ErrorAction Stop)
    if (@($parts | Where-Object { $_.IsBoot -or $_.IsSystem -or "$($_.DriveLetter):" -eq $env:SystemDrive }).Count -gt 0) { throw 'GUARD_SYSTEM_VOLUME: system partition detected.' }
    return $d
}
function Assert-Volume($r, $d) {
    if (!$r.Volume -or "$($r.Volume.Letter)" -notmatch '^[D-Z]$') { throw 'GUARD_VOLUME: select a mounted non-system volume D-Z.' }
    $p = Get-Partition -DiskNumber $d.Number -PartitionNumber ([int]$r.Volume.PartitionNumber) -ErrorAction Stop
    $v = $p | Get-Volume -ErrorAction Stop
    if ("$($p.DriveLetter)" -cne "$($r.Volume.Letter)" -or "$($v.UniqueId)" -cne "$($r.Volume.Id)" -or [string]::IsNullOrWhiteSpace("$($v.UniqueId)")) { throw 'GUARD_VOLUME_CHANGED: volume identity changed.' }
    return @{ Partition=$p; Volume=$v }
}
function Assert-Consent($r) {
    $expected = "CONFIRM $($r.Action) DISK $($r.Disk.Number)"
    if ($r.Action -eq 'Format') { $expected = "ERASE DISK $($r.Disk.Number) $($r.Volume.Letter):" }
    if ("$($r.Consent)" -cne $expected) { throw 'GUARD_CONSENT: explicit confirmation missing.' }
}
