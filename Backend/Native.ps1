function Get-NativeState($disk) {
 try {
  if(!('UsbZard.Native' -as [type])) {
   Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace UsbZard {
public static class Native {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern SafeFileHandle CreateFile(string p,uint a,uint s,IntPtr sec,uint mode,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)] public static extern bool DeviceIoControl(SafeFileHandle h,uint c,IntPtr i,uint il,IntPtr o,uint ol,out uint n,IntPtr ov);
 [DllImport("kernel32.dll",SetLastError=true)] public static extern bool ReadFile(SafeFileHandle h,byte[] b,uint size,out uint n,IntPtr ov);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern bool GetVolumeInformation(string root,StringBuilder name,uint n,out uint serial,out uint max,out uint flags,StringBuilder fs,uint f);
}
}
'@
  }
  $h=[UsbZard.Native]::CreateFile(('\\.\PhysicalDrive'+[int]$disk.Number),2147483648,3,[IntPtr]::Zero,3,0,[IntPtr]::Zero)
  if($h.IsInvalid){return @{OpenError=[Runtime.InteropServices.Marshal]::GetLastWin32Error();Writable=$null}}
  try {
   [uint32]$n=0
   $w=[UsbZard.Native]::DeviceIoControl($h,0x70024,[IntPtr]::Zero,0,[IntPtr]::Zero,0,[ref]$n,[IntPtr]::Zero)
   $we=if($w){0}else{[Runtime.InteropServices.Marshal]::GetLastWin32Error()}
   $length=[Math]::Max(4096,[int]$disk.LogicalSectorSize)
   if($length -gt 65536){throw 'Unsupported logical sector size'}
   $b=New-Object byte[] $length
   $read=[UsbZard.Native]::ReadFile($h,$b,$length,[ref]$n,[IntPtr]::Zero)
   $re=if($read){0}else{[Runtime.InteropServices.Marshal]::GetLastWin32Error()}
   return @{OpenError=0;Writable=$w;WriteError=$we;Read=$read;ReadError=$re;BytesRead=$n;FirstBlockAllZero=if($read -and $n -gt 0){@($b[0..($n-1)]|Where-Object{$_ -ne 0}).Count -eq 0}else{$null};BootSignature=if($n -ge 512){'{0:X2}{1:X2}' -f $b[510],$b[511]}else{''};Note='IOCTL is a query, not a write test. First block only; no complete media scan.'}
  }finally{$h.Dispose()}
 }catch{return @{OpenError=$null;Writable=$null;Error="$_"}}
}
function Get-VolumeFlag($letter) {
 if("$letter" -notmatch '^[D-Z]$' -or !('UsbZard.Native' -as [type])){return $null}
 [uint32]$sn=0;[uint32]$max=0;[uint32]$flags=0
 $name=New-Object Text.StringBuilder 261;$fs=New-Object Text.StringBuilder 261
 $ok=[UsbZard.Native]::GetVolumeInformation(("$letter"+':\'),$name,261,[ref]$sn,[ref]$max,[ref]$flags,$fs,261)
 if(!$ok){return $null}
 return (($flags -band 0x80000) -ne 0)
}
function Invoke-DiskPart([string[]]$commands) {
 # /s stops at the first error. DiskPart's interactive stdin mode can exit 0 even on failures.
 $scriptFile=Join-Path ([IO.Path]::GetTempPath()) ('usb-zard-'+[Guid]::NewGuid().ToString('N')+'.diskpart')
 try {
  [IO.File]::WriteAllLines($scriptFile,(@($commands)+@('exit')),[Text.Encoding]::ASCII)
  $pi=New-Object Diagnostics.ProcessStartInfo
  $pi.FileName="$env:windir\System32\diskpart.exe";$pi.Arguments='/s "'+$scriptFile+'"'
  $pi.UseShellExecute=$false;$pi.CreateNoWindow=$true;$pi.RedirectStandardOutput=$true;$pi.RedirectStandardError=$true
  $p=[Diagnostics.Process]::Start($pi)
  try {
   $output=$p.StandardOutput.ReadToEndAsync();$errorText=$p.StandardError.ReadToEndAsync();$p.WaitForExit()
   return @{ExitCode=$p.ExitCode;Output=$output.Result;Error=$errorText.Result}
  } finally {$p.Dispose()}
 } finally {[IO.File]::Delete($scriptFile)}
}
function Test-FileRoundtrip($r,$d) {
 $target=Assert-Volume $r $d
 if("$($target.Volume.FileSystemType)" -notin @('FAT','FAT32','exFAT','NTFS') -or $target.Volume.Size -le 0){throw 'RAW_FILESYSTEM: cannot create a test file on an unrecognized filesystem.'}
 $testPath=Join-Path ("$($target.Volume.DriveLetter):\") ('usb-zard-test-'+[Guid]::NewGuid().ToString('N')+'.tmp')
 $created=$false;$ok=$false;$cleanup=$false;$errorMessage='';$cleanupError=''
 try {
  $bytes=New-Object byte[] 1048576
  $rng=[Security.Cryptography.RandomNumberGenerator]::Create();try{$rng.GetBytes($bytes)}finally{$rng.Dispose()}
  $s=New-Object IO.FileStream($testPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None,4096,[IO.FileOptions]::WriteThrough)
  $created=$true
  try{$s.Write($bytes,0,$bytes.Length);$s.Flush($true)}finally{$s.Dispose()}
  $actual=[IO.File]::ReadAllBytes($testPath)
  $sha=[Security.Cryptography.SHA256]::Create()
  try{$expected=[Convert]::ToBase64String($sha.ComputeHash($bytes));$hash=[Convert]::ToBase64String($sha.ComputeHash($actual))}finally{$sha.Dispose()}
  $ok=($actual.Length -eq $bytes.Length -and $expected -ceq $hash)
 }catch{$errorMessage="$_"}
 finally {
  if($created) {
   try {
    $current=Assert-Target $r
    Assert-Volume $r $current | Out-Null
    [IO.File]::Delete($testPath);$cleanup=$true
   }catch{$cleanupError="$_"}
  }
 }
 return @{Verified=$ok;Bytes=1048576;SHA256=$hash;TemporaryFile=$testPath;CleanedUp=$cleanup;Error=$errorMessage;CleanupError=$cleanupError;Note='A small immediate write/read test. Does not certify total capacity or retention after unplugging.'}
}
