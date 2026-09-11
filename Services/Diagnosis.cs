using System.Text.Json;
namespace UsbWriteGuard;
public static class Diagnosis {
 public const string LimitationEn="Software cannot repair every hardware, firmware or NAND fault. Back up readable data before changes.";
 public const string LimitationFa="نرم‌افزار نمی‌تواند تمام خرابی‌های سخت‌افزار، firmware یا NAND را رفع کند. پیش از تغییر از داده‌های خواندنی پشتیبان بگیرید.";
 public static List<Finding> Analyze(JsonElement d) {
  var f=new List<Finding>(); const string observed="Observed / مشاهده‌شده";
  void Add(string code,string en,string fa,string next,string confidence=observed)=>f.Add(new(code,en,fa,confidence,next));
  if(d.TryGetProperty("Volumes",out var volumes)){
   foreach(var v in volumes.EnumerateArray()){
    var fs=v.GetProperty("FileSystem").GetString();var letter=v.GetProperty("Letter").GetString();
    var raw=fs is null or "" or "RAW" or "Unknown" || v.GetProperty("VolumeSize").GetUInt64()==0;
    if(raw)Add("RAW_FILESYSTEM",$"{letter}: has no recognized filesystem. Clearing read-only cannot reconstruct it.",$"{letter}: فایل‌سیستم قابل‌شناسایی ندارد. برداشتن قفل فقط‌خواندنی آن را بازسازی نمی‌کند.","Recover valuable data first. If erasure is acceptable, explicitly format. CHKDSK is unavailable for RAW. / ابتدا داده مهم را بازیابی کنید؛ در صورت پذیرش پاک‌شدن، فرمت را انتخاب کنید. CHKDSK برای RAW قابل استفاده نیست.");
    if(v.TryGetProperty("ReadOnly",out var pr)&&pr.ValueKind==JsonValueKind.Null)
     Add("PARTITION_ATTRIBUTE_UNAVAILABLE","This device does not expose a partition read-only property.","این دستگاه ویژگی فقط‌خواندنی پارتیشن را ارائه نمی‌کند.","Unknown is not false. Filesystem/driver read-only must be checked separately. / وضعیت نامشخص را نباید خاموش فرض کرد.","Unknown / نامشخص");
   }
  }
  bool ro=d.GetProperty("Disk").GetProperty("IsReadOnly").GetBoolean();
  Add("DISK_ATTRIBUTE",ro?"Windows disk read-only attribute is set.":"Windows disk read-only attribute is already off.",ro?"ویژگی فقط‌خواندنی دیسک فعال است.":"ویژگی فقط‌خواندنی دیسک از قبل خاموش است.",ro?"Clear with consent, then verify. / با تأیید رفع و سپس بررسی کنید.":"Do not repeatedly clear an absent lock; inspect filesystem and actual device errors. / رفع قفل خاموش را تکرار نکنید؛ فایل‌سیستم و خطای دستگاه را بررسی کنید.");
  if(d.TryGetProperty("Native",out var n)){
   if(n.TryGetProperty("WriteError",out var we)&&we.ValueKind==JsonValueKind.Number&&we.GetInt32()==19)
    Add("DRIVER_WRITE_PROTECT","The device/driver rejects the writable query with Win32 error 19.","دستگاه یا درایور، پرس‌وجوی قابلیت نوشتن را با خطای ۱۹ (محافظت نوشتن) رد کرد.","Inspect the adapter lock, reader and connection; firmware fail-safe is possible but not proven. / کلید قفل، کارت‌خوان و اتصال را بررسی کنید؛ حالت حفاظتی firmware احتمال دارد، نه قطعیت.");
   else if(n.TryGetProperty("Writable",out var w)&&w.ValueKind==JsonValueKind.True)
    Add("DRIVER_WRITABLE","The driver reports writable media. This query did not write any data.","درایور نوشتن را مجاز گزارش می‌کند؛ این پرس‌وجو چیزی روی رسانه ننوشت.","A successful file roundtrip is still required to test actual writes. / برای آزمون واقعی نوشتن، فایل آزمایشی باید نوشته و بازخوانی شود.");
   else Add("DRIVER_QUERY_UNKNOWN","Device writable status could not be established.","وضعیت قابلیت نوشتن دستگاه مشخص نشد.","Inspect the native error in details; do not assume hardware protection. / خطای بومی را در جزئیات بررسی کنید؛ قفل سخت‌افزاری را فرض نکنید.","Unknown / نامشخص");
   if(n.TryGetProperty("FirstBlockAllZero",out var zero)&&zero.ValueKind==JsonValueKind.True)
    Add("ZERO_FIRST_BLOCK","The sampled first block contains only zero bytes; no boot signature was found there.","نمونه ابتدایی خوانده‌شده فقط بایت صفر دارد؛ امضای بوت در آن یافت نشد.","This can follow erasure, missing metadata or device faults; it does not prove all media is empty. / ممکن است ناشی از پاک‌شدن، نبود ساختار یا خطای دستگاه باشد؛ خالی‌بودن کل رسانه اثبات نشده است.");
   if(n.TryGetProperty("Read",out var read)&&read.ValueKind==JsonValueKind.False)
    Add("READ_FAILED","The initial raw read failed.","خواندن ابتدایی رسانه شکست خورد.","Check the native read error; avoid repeated repairs and consider recovery. / کد خطای خواندن را بررسی کنید؛ تعمیر مکرر را متوقف و بازیابی را در نظر بگیرید.");
  }
  if(d.TryGetProperty("WriteProtect",out var p)&&p.ValueKind==JsonValueKind.Number&&p.GetInt32()==1)
   Add("LOCAL_POLICY","Local StorageDevicePolicies WriteProtect=1 is present.","تنظیم محلی StorageDevicePolicies با WriteProtect=1 وجود دارد.","Change only with consent; the setting affects the whole machine. / تغییر فقط با تأیید؛ این تنظیم سراسری رایانه است.");
  if(d.GetProperty("GroupPolicies").EnumerateArray().Any(x=>x.GetProperty("Value").ToString()=="1"))
   Add("MANAGED_POLICY","A deny policy exists; applicability depends on the device class and user.","سیاست منع دسترسی وجود دارد؛ اعمال آن به کلاس دستگاه و کاربر وابسته است.","Contact the administrator. / با مدیر سیستم تماس بگیرید.","Possible / احتمالی");
  if(d.GetProperty("Disk").GetProperty("IsOffline").GetBoolean())
   Add("OFFLINE","Disk is offline.","دیسک آفلاین است.","Inspect Windows Disk Management. / مدیریت دیسک ویندوز را بررسی کنید.");
  if(d.GetProperty("Reliability").ValueKind==JsonValueKind.Null)
   Add("SMART_UNAVAILABLE","No unambiguously matched reliability counters were returned.","شمارنده سلامت با تطبیق هویت معتبر دریافت نشد.","Windows Healthy or missing SMART does not certify media health. / عنوان Healthy ویندوز یا نبود SMART سلامت رسانه را تأیید نمی‌کند.","Unknown / نامشخص");
  else foreach(var field in new[]{"ReadErrorsUncorrected","WriteErrorsUncorrected"})
   if(d.GetProperty("Reliability").TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.Number&&value.GetDouble()>0)
    Add("MEDIA_ERRORS",$"Device reports {field}={value}.",$"دستگاه {field}={value} گزارش می‌کند.","Back up/recover data and consider replacing media. / پشتیبان یا بازیابی داده و تعویض رسانه را در نظر بگیرید.");
  Add("HARDWARE_UNDETERMINED","These checks cannot conclusively identify NAND or controller failure.","این بررسی‌ها خرابی NAND یا کنترلر را به‌طور قطعی تعیین نمی‌کنند.","If standard operations fail, test another port/reader/computer before replacing media. / در صورت شکست عملیات استاندارد، پورت، کارت‌خوان یا رایانه دیگر را پیش از تعویض رسانه امتحان کنید.","Unknown / نامشخص");
  return f;
 }
}
