# usb zard — English guide

[Home](README.md) · [فارسی](README.fa.md)

## Purpose

usb zard is a C#/.NET 8 WPF application for Windows 10/11 x64. It helps investigate write-protection and unrecognized-filesystem problems on USB flash drives and SD/microSD cards exposed through USB, SD or MMC storage transports.

It uses Windows storage commands and a small set of native, read-only queries. It reports evidence and errors instead of promising universal repair. A controller fail-safe, physical adapter switch, failing NAND or unsupported bridge may not be repairable in software.

## Requirements and installation

- Windows 10/11 x64.
- Administrator access through the application's UAC prompt.
- Windows PowerShell 5.1 and Windows Storage/WMI components.
- For a source build: .NET 8 SDK. The self-contained Release executable includes its .NET runtime.
- No application installer or third-party NuGet dependency is required.
- The executable is unsigned; use the repository's release files and SHA-256 checksums.

Extract the release ZIP before launching **usb zard.exe**. Do not put the running application on the device you intend to format.

## Recommended workflow

1. Back up any readable files before modifying the device.
2. Select **Scan devices**. Check model, reported capacity and serial number.
3. Explicitly select the target device and run **Diagnose**.
4. Select a volume when an action requires one.
5. Read the findings and next step; choose only an applicable operation.
6. For a modifying action, read the warning and type the exact confirmation phrase.
7. Keep the device connected until the operation finishes.
8. Export the TXT or JSON report before closing.

**RAW does not mean read-only.** It means Windows does not recognize a usable filesystem. Clearing an absent read-only flag will not rebuild that filesystem. CHKDSK and file roundtrip tests are blocked for RAW volumes. Recover valuable data first; format only if erasure is acceptable.

## Detection and diagnostics

The application reads disk identity, serial, bus type, reported capacity, volume identity and filesystem information. PnP and VID/PID information is included where Windows exposes it.

Disk read-only, partition read-only and the device/driver's writable response are distinct. An unsupported partition property remains **unknown**, rather than being converted to false.

The native **IOCTL_DISK_IS_WRITABLE** query asks the driver about writable state without writing data. A bounded first-block read records basic observations such as all-zero bytes or a missing boot signature. It is not a full surface scan, data recovery scan or proof that the entire device is empty.

Reliability counters are shown only when a physical-disk identity can be matched unambiguously. Missing SMART/reliability data and Windows' “Healthy” label are not proof of healthy flash memory.

## Operations

| Action | Behavior and scope |
|---|---|
| Diagnose | Read-only evidence collection and bilingual findings. |
| Clear disk read-only | Uses Set-Disk. Reports “no change” when the flag is already off. |
| Clear volume read-only | Uses DiskPart script mode and checks the exposed filesystem read-only flag. On basic MBR disks, an attribute change may affect all volumes on that disk. |
| Rescan | Refreshes the Windows storage cache. Remounting uses manual safe removal and reconnection. |
| Change Windows policy | Backs up and changes local StorageDevicePolicies WriteProtect. This is machine-wide; managed policies are not bypassed. |
| Check filesystem | WMI CHKDSK without repairs, only for a recognized supported filesystem. |
| CHKDSK repair | Explicitly confirmed filesystem repair; may alter or discard damaged filesystem records. No forced dismount, bad-sector recovery or boot scheduling. |
| Test write & read | Creates a unique 1 MiB file, flushes it, reads it back, compares SHA-256 and removes only that temporary file. |
| Format & erase | Explicit quick format to exFAT, NTFS or FAT32. Erases the selected volume and includes post-format verification. |

FAT32 formatting above 32 GiB is conservatively rejected. Format first uses Format-Volume and can fall back to DiskPart /s after an error. Script mode stops on its first failure. Completion requires an accepted formatting operation, the requested filesystem with nonzero size, and a successful small file roundtrip. A process exit code alone is not accepted as proof of repair.

A 1 MiB immediate write/read test does not certify full capacity, detect all counterfeit capacities, or prove retention after power loss or reconnection. If cleanup fails, the temporary file path and cleanup error are retained in the report.

## Safeguards

- No automatic repairs on startup.
- Typed confirmation for modifying operations; a separate erasure phrase for formatting.
- Revalidation of device path, identity, serial, size, bus and boot/system flags.
- Revalidation of partition-to-volume mapping and volume identity.
- Internal/system/boot targets and ambiguous device identities are rejected.
- Volume operations are restricted to explicitly selected non-system drive letters D–Z.
- No application command for clean, repartitioning, raw sector writes, firmware flashing or vendor manufacturing tools.
- No forced termination of an in-progress storage operation.
- The application cannot eliminate every hot-unplug race; do not swap devices during operations.

The selected volume's letter alone is not treated as sufficient identity.

## Reports and troubleshooting

Reports contain timestamps, action targets, bilingual explanations, findings, suggested next steps, native error codes and structured details. Save TXT for reading or JSON for inspection. Session history stays in memory until exported.

Reports can contain serials, volume IDs, device paths and policy details. Review or redact them before posting an issue.

Examples:

- **NO_CHANGE**: no disk read-only attribute needed clearing; it does not mean the media was repaired.
- **RAW_FILESYSTEM**: Windows cannot identify a usable filesystem; CHKDSK/file testing is unavailable.
- **CHKDSK_4**: WMI reported “No Media In Drive”; this is not conclusive proof of NAND failure.
- **FORMAT_NOT_VERIFIED**: a usable requested filesystem was not verified; original/fallback errors are preserved.
- **WRITE_VERIFY_FAILED**: the file roundtrip did not validate.
- **GUARD_…**: safety validation stopped the action; rescan and confirm the device.

If standard operations fail, stop repeating destructive attempts. Try a direct alternate port, a different card reader where applicable, and another computer. If failure persists, consider professional recovery for important data or device replacement.

## Build and tests

Run from the repository root on Windows:

```powershell
dotnet build UsbWriteGuard.csproj -c Release
dotnet run --project Tests/UsbWriteGuard.Tests.csproj -c Release
powershell -NoProfile -ExecutionPolicy RemoteSigned -File Tests/Guard.Tests.ps1
powershell -NoProfile -ExecutionPolicy RemoteSigned -File Tests/Worker.Tests.ps1
dotnet run --project Tests.Ui/UiSmoke.csproj -c Release -- previews
dotnet publish UsbWriteGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o release
```

RemoteSigned above applies only to the test process; it does not change machine policy. Do not override managed organizational policies. The supplied tests use synthetic data or mocked storage commands and do not format a connected USB device.

## Architecture

- MainWindow.xaml/.cs: WPF interface, action gating, confirmation and export.
- Models.cs: device, volume, finding and action records.
- Services/Backend.cs: trusted embedded scripts and structured UTF-8 stdin/JSON process protocol.
- Services/Diagnosis.cs: evidence-based findings.
- Services/ResultText.cs: bilingual operation/error explanations.
- Backend/Guard.ps1: identity, volume and consent checks.
- Backend/Native.ps1: native read-only queries, DiskPart script runner and file verification.
- Backend/Worker.ps1: standard Windows observations and explicitly selected operations.
- Assets: application logo and icon.
- Tests and Tests.Ui: synthetic/mocked tests and offscreen UI rendering.

## Limitations

There is no universal support for every USB controller, SD reader, firmware, encryption state or storage layout. Unmounted/locked/encrypted media, missing identities and card readers reported through other transports can be unsupported.

**No software can fully repair every hardware, firmware or NAND write-protection fault.**

## Microsoft references

- [IOCTL_DISK_IS_WRITABLE](https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-ioctl_disk_is_writable)
- [DiskPart volume attributes](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/attributes-volume)
- [Win32_Volume CHKDSK and return codes](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/vdswmi/chkdsk-method-in-class-win32-volume)
- [Format-Volume](https://learn.microsoft.com/en-us/powershell/module/storage/format-volume)
- [DiskPart](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/diskpart)
