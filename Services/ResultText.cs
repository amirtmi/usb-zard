using System.Text.Json;
namespace UsbWriteGuard;
public static class ResultText {
 public static (string Fa,string En) Describe(string action,JsonElement result) {
  string Field(string name)=>result.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
  var fa=Field("MessageFa");var en=Field("MessageEn");
  if(fa.Length>0&&en.Length>0)return(fa,en);
  var success=result.GetProperty("Success").GetBoolean();
  if(success)return action=="Diagnose"?("تشخیص تکمیل شد؛ یافته‌ها و اقدام بعدی را بخوانید.","Diagnostics completed; review findings and next steps."):("فرمان تکمیل شد؛ نتیجه آن در جزئیات ثبت شده است.","Command completed; its result is recorded in details.");
  var text=Field("Code")+" "+Field("Message");
  if(text.Contains("RAW_FILESYSTEM"))return("فایل‌سیستم RAW یا ناشناخته است؛ CHKDSK و آزمون فایل قابل اجرا نیست. ابتدا بازیابی داده یا فرمت با تأیید لازم است.","RAW/unrecognized filesystem: CHKDSK and file tests cannot run. Recover data or explicitly format.");
  if(text.Contains("FORMAT_NOT_VERIFIED"))return("فرمت تأیید نشد؛ فایل‌سیستم معتبر ساخته نشده است. خطای روش اصلی و DiskPart در جزئیات ثبت شد. تکرار کورکورانه فرمت را متوقف کنید.","Format was not verified; no valid filesystem was created. Original and DiskPart errors are preserved. Stop blind repeated formatting.");
  if(text.Contains("CHKDSK_4"))return("WMI خطای ۴: «رسانه در درایو یافت نشد» گزارش کرد؛ این خطا به‌تنهایی خرابی فیزیکی را ثابت نمی‌کند.","WMI CHKDSK code 4: No Media In Drive; this alone does not prove physical failure.");
  if(text.Contains("CHKDSK_2")||text.Contains("CHKDSK_3"))return("CHKDSK فایل‌سیستم را پشتیبانی یا شناسایی نکرد.","CHKDSK does not support or recognize this filesystem.");
  if(text.Contains("WRITE_VERIFY_FAILED"))return("آزمون نوشتن/بازخوانی تأیید نشد. خطای فایل و وضعیت حذف فایل موقت را بررسی کنید.","Write/read verification failed. Inspect the file error and temporary-file cleanup status.");
  if(text.Contains("VOLUME_CLEAR_UNVERIFIED"))return("رفع ویژگی پارتیشن قابل تأیید نیست؛ ممکن است این رسانه روش موردنیاز را پشتیبانی نکند. نتیجه را موفق فرض نکنید.","Volume attribute clearing could not be verified; this device may not support the required method. Do not assume success.");
  if(text.Contains("GUARD_"))return("برای محافظت از داده، عملیات متوقف شد: هویت، پارتیشن یا تأیید با وضعیت فعلی تطبیق ندارد. دوباره جستجو کنید.","Safety check stopped the operation: identity, volume or consent does not match. Scan again.");
  if(text.Contains("FAT32_LIMIT"))return("برای پارتیشن بیش از ۳۲ GiB، exFAT یا NTFS را انتخاب کنید.","Select exFAT or NTFS for a partition above 32 GiB.");
  if(text.Contains("Not Supported"))return("این عملیات توسط دستگاه یا ارائه‌دهنده ذخیره‌سازی ویندوز پشتیبانی نمی‌شود.","The device or Windows storage provider does not support this operation.");
  if(text.Contains("Invalid Parameter"))return("ویندوز پارامتر عملیات را رد کرد؛ ساختار رسانه، هندسه یا محدودیت ارائه‌دهنده ممکن است علت باشد.","Windows rejected the operation parameters; media layout, geometry or provider limitations may be involved.");
  return("عملیات ناموفق است؛ پیام و کد دقیق خطا در جزئیات ثبت شد. پیش از تکرار، فایل‌سیستم و اتصال دستگاه را بررسی کنید.","Operation failed. Exact error and code are preserved. Check filesystem and connection before retrying.");
 }
}
