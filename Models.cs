using System.Text.Json;
namespace UsbWriteGuard;
public sealed class VolumeInfo {
 public int PartitionNumber {get;set;} public string Letter {get;set;} = "";
 public string Id {get;set;} = ""; public string FileSystem {get;set;} = "";
 public bool? ReadOnly {get;set;} public ulong PartitionSize {get;set;} public ulong VolumeSize {get;set;} public ulong Offset {get;set;} public string Label {get;set;} = "";
 public bool IsRaw => VolumeSize==0 || FileSystem is "Unknown" or "RAW" or "";
 public string Display => $"{Letter}:  {(IsRaw?"RAW":FileSystem)}  {PartitionSize/1073741824.0:F2} GiB";
}
public sealed class Device {
 public int Number {get;set;} public string UniqueId {get;set;} = ""; public string Path {get;set;} = "";
 public string Serial {get;set;} = ""; public string Name {get;set;} = ""; public string Bus {get;set;} = "";
 public ulong Size {get;set;} public bool ReadOnly {get;set;} public bool Offline {get;set;}
 public string PnpId {get;set;} = ""; public string UsbId {get;set;} = ""; public string Health {get;set;} = "";
 public VolumeInfo[] Volumes {get;set;} = [];
 public string Display => $"Disk {Number} • {Name} • {Size / 1073741824.0:F2} GiB • {Bus} • {Serial}";
}
public sealed record Finding(string Code, string English, string Persian, string Confidence, string Next);
public sealed record Entry(DateTimeOffset Time, string Action, string Target, bool Success, string Code, string English, string Persian, JsonElement Details, Finding[]? Findings=null);
public static class JsonDefaults {
 public static readonly JsonSerializerOptions Options = new(){PropertyNameCaseInsensitive=true,WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
}