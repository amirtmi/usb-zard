using System.Text.Json;
using UsbWriteGuard;
int count=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);count++;}
using var clean=JsonDocument.Parse("""{"Disk":{"IsReadOnly":false,"IsOffline":false},"WriteProtect":null,"GroupPolicies":[],"Reliability":null}""");
var f=Diagnosis.Analyze(clean.RootElement);
Check(f.Any(x=>x.Code=="SMART_UNAVAILABLE"&&x.Confidence.StartsWith("Unknown")),"Missing SMART remains unknown");
Check(f.Any(x=>x.Code=="HARDWARE_UNDETERMINED"),"Never diagnoses hardware conclusively");
Check(!f.Any(x=>x.Code=="LOCAL_POLICY"),"Missing policy not misreported as blocking");
using var fault=JsonDocument.Parse("""{"Disk":{"IsReadOnly":true,"IsOffline":true},"WriteProtect":1,"GroupPolicies":[{"Value":1}],"Reliability":{"ReadErrorsUncorrected":4,"WriteErrorsUncorrected":null}}""");
var g=Diagnosis.Analyze(fault.RootElement);
foreach(var code in new[]{"LOCAL_POLICY","MANAGED_POLICY","OFFLINE","MEDIA_ERRORS"})Check(g.Any(x=>x.Code==code),code);
Check(g.All(x=>!string.IsNullOrWhiteSpace(x.Persian)&&!string.IsNullOrWhiteSpace(x.English)),"Bilingual findings");
Console.WriteLine($"{count} tests passed; no hardware access.");

using var raw=JsonDocument.Parse("""{"Disk":{"IsReadOnly":false,"IsOffline":false},"WriteProtect":null,"GroupPolicies":[],"Reliability":null,"Native":{"Writable":true,"WriteError":0,"Read":true,"FirstBlockAllZero":true},"Volumes":[{"Letter":"E","FileSystem":"Unknown","VolumeSize":0,"ReadOnly":null}]}""");
var r=Diagnosis.Analyze(raw.RootElement);
Check(r.Any(x=>x.Code=="RAW_FILESYSTEM"),"Recognize real RAW case");
Check(r.Any(x=>x.Code=="ZERO_FIRST_BLOCK"),"Explain zeroed first block");
Check(r.Any(x=>x.Code=="DRIVER_WRITABLE"),"Writable query is not a write test");
Check(r.Any(x=>x.Code=="DISK_ATTRIBUTE"&&!x.Next.StartsWith("Clear")),"Do not recommend clearing absent lock");
using var fail=JsonDocument.Parse("""{"Success":false,"Code":"CHKDSK_4","Message":""}""");
Check(ResultText.Describe("Repair",fail.RootElement).En.Contains("No Media"),"Decode WMI code 4");
Console.WriteLine($"{count} total diagnosis/error tests passed.");

