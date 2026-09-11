using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace UsbWriteGuard;
public partial class MainWindow : Window {
 readonly Backend backend=new(); readonly List<Entry> entries=[]; readonly List<Finding> findings=[];
 bool busy, demo; bool Persian=>LanguagePicker.SelectedIndex==0; Device? Selected=>Devices.SelectedItem as Device;
 public MainWindow(){InitializeComponent(); Localize(); Render(); UpdateActions();}
 string T(string fa,string en)=>Persian?fa:en;
 void LanguageChanged(object s,SelectionChangedEventArgs e){if(Subtitle!=null) Localize();}
 void Localize(){
  Subtitle.Text=T("تشخیص USB و SD • عملیات کنترل‌شده • گزارش دو‌زبانه","USB & SD diagnostics • Controlled repairs • Bilingual reports");
  Limits.Text=T(Diagnosis.LimitationFa,Diagnosis.LimitationEn);
  ScanButton.Content=T("جستجوی دستگاه‌ها","Scan devices"); DemoButton.Content=T("نمونه نمایشی","Demo");
  DeviceLabel.Text=T("دستگاه:","Device:"); VolumeLabel.Text=T("پارتیشن:","Volume:");
  DiagnoseButton.Content=T("تشخیص","Diagnose"); ClearButton.Content=T("رفع فقط‌خواندنی دیسک","Clear disk read-only");
  RescanButton.Content=T("بازخوانی اتصال","Rescan"); PolicyButton.Content=T("اصلاح سیاست ویندوز","Change Windows policy");
  VolumeButton.Content=T("رفع قفل پارتیشن","Clear partition read-only"); CheckButton.Content=T("بررسی فایل‌سیستم","Check filesystem");
  RepairButton.Content=T("تعمیر CHKDSK","CHKDSK repair"); FormatButton.Content=T("فرمت و پاک‌کردن","Format & erase");
  ExportButton.Content=T("ذخیره گزارش","Export report"); Status.Text=T("آماده — هیچ عملیاتی خودکار اجرا نمی‌شود.","Ready — no automatic storage actions.");
  Subtitle.FlowDirection=Limits.FlowDirection=Status.FlowDirection=Persian?FlowDirection.RightToLeft:FlowDirection.LeftToRight;
  Report.FlowDirection=FlowDirection.LeftToRight;
  ProbeButton.Content=T("آزمون نوشتن و خواندن","Test write & read");
  Summary.FlowDirection=Persian?FlowDirection.RightToLeft:FlowDirection.LeftToRight;
  UpdateActions();
 }
 void DeviceChanged(object s,SelectionChangedEventArgs e){Volumes.ItemsSource=Selected?.Volumes; Volumes.SelectedIndex=-1; UpdateActions();}
 void VolumeChanged(object s,SelectionChangedEventArgs e)=>UpdateActions();
 void UpdateActions(){
  if(ProbeButton==null||Volumes==null)return;
  var device=Selected;var volume=Volumes.SelectedItem as VolumeInfo;
  bool usable=device!=null&&!demo&&!busy;bool mounted=usable&&volume!=null&&!volume.IsRaw;
  DiagnoseButton.IsEnabled=ClearButton.IsEnabled=RescanButton.IsEnabled=PolicyButton.IsEnabled=usable;
  ClearButton.IsEnabled=usable&&device!.ReadOnly;
  VolumeButton.IsEnabled=CheckButton.IsEnabled=RepairButton.IsEnabled=ProbeButton.IsEnabled=mounted;
  FormatButton.IsEnabled=usable&&volume!=null;
  if(volume?.IsRaw==true)Summary.Text=T("RAW: فایل‌سیستم قابل‌شناسایی نیست. ابتدا بازیابی داده؛ یا فرمت فقط با پذیرش پاک‌شدن. CHKDSK قابل اجرا نیست.","RAW: filesystem is unrecognized. Recover data first, or explicitly erase and format. CHKDSK is unavailable.");
  else if(device!=null)Summary.Text=T("دستگاه انتخاب شد. تشخیص را اجرا و برای عملیات فایل، پارتیشن را انتخاب کنید.","Device selected. Run diagnostics and select a volume for file operations.");
  else Summary.Text=T("۱. جستجوی دستگاه‌ها  ۲. انتخاب دستگاه  ۳. تشخیص؛ سپس اقدام متناسب با نتیجه","1. Scan  2. Select a device  3. Diagnose; then choose an action based on the findings");
 }
 async void ScanClick(object s,RoutedEventArgs e)=>await Scan();
 async Task Scan(){
  await Work(async()=>{demo=false; Devices.ItemsSource=null; findings.Clear(); var result=await backend.RunAsync(new{Action="Scan"}); Record("Scan","All external devices / دستگاه‌های خارجی",result);
   if(result.GetProperty("Success").GetBoolean()) Devices.ItemsSource=result.GetProperty("Data").GetProperty("Devices").Deserialize<Device[]>(JsonDefaults.Options);
   Summary.Text=ResultText.Describe("Scan",result).Fa;
  });
 }
 async void ProbeClick(object s,RoutedEventArgs e)=>await Action("Probe",true);
 async void DiagnoseClick(object s,RoutedEventArgs e)=>await Action("Diagnose",false);
 async void ClearClick(object s,RoutedEventArgs e)=>await Action("ClearDisk",false);
 async void VolumeClick(object s,RoutedEventArgs e)=>await Action("ClearVolume",true);
 async void RescanClick(object s,RoutedEventArgs e)=>await Action("Rescan",false);
 async void PolicyClick(object s,RoutedEventArgs e)=>await Action("Policy",false);
 async void CheckClick(object s,RoutedEventArgs e)=>await Action("Check",true);
 async void RepairClick(object s,RoutedEventArgs e)=>await Action("Repair",true);
 async void FormatClick(object s,RoutedEventArgs e)=>await Action("Format",true);
 async Task Action(string action,bool needsVolume){
  var device=Selected; var volume=Volumes.SelectedItem as VolumeInfo;
  if(device==null||demo){MessageBox.Show(T("ابتدا یک دستگاه واقعی را جستجو و انتخاب کنید.","Scan and select a real device first."));return;}
  if(needsVolume&&volume==null){MessageBox.Show(T("پارتیشن را صریحاً انتخاب کنید.","Select the volume explicitly."));return;}
  string token="";
  if(action!="Diagnose"){
   token=action=="Format"?$"ERASE DISK {device.Number} {volume!.Letter}:":$"CONFIRM {action} DISK {device.Number}";
   string warning=action switch{
    "Probe"=>"یک فایل تصادفی ۱ MiB ساخته، روی دیسک تخلیه، بازخوانی و با SHA-256 مقایسه می‌شود؛ سپس فقط همان فایل موقت حذف می‌شود. این آزمون سلامت کل ظرفیت را اثبات نمی‌کند.\nCreates, flushes, reads and hashes a new 1 MiB file, then removes only that temporary file. This does not certify full capacity.",
    "Format"=>"تمام داده‌های پارتیشن انتخاب‌شده پاک می‌شود. پس از فرمت، یک فایل آزمایشی کوچک نوشته و بازخوانی می‌شود. این کار قفل سخت‌افزاری را رفع نمی‌کند.\nALL DATA ON THE SELECTED VOLUME WILL BE ERASED. This cannot repair hardware locks.",
    "Repair"=>"CHKDSK فایل‌سیستم را تغییر می‌دهد و ممکن است داده آسیب‌دیده را حذف کند. ابتدا پشتیبان بگیرید. قطع عملیات امن نیست.\nCHKDSK modifies the filesystem and may discard damaged data. Back up first. Do not interrupt.",
    "Check"=>"بررسی فقط‌خواندنی ممکن است طولانی باشد؛ برای رسانه در حال خرابی ابتدا بازیابی داده را در نظر بگیرید.\nRead-only filesystem check may take time; consider data recovery first for failing media.",
    "Policy"=>"تنظیم WriteProtect برای کل رایانه تغییر می‌کند، نه فقط این USB. مقدار قبلی ذخیره می‌شود. سیاست سازمانی تغییر نمی‌کند.\nChanges machine-wide WriteProtect, affecting other media. Previous value is backed up; managed policy remains.",
    "Rescan"=>"کش دستگاه‌های ذخیره‌سازی بازخوانی می‌شود. برای اتصال مجدد، خروج امن و اتصال دستی لازم است.\nRefreshes the storage cache. Remount requires safe manual eject and reinsert.",
    _=>"روی دیسک MBR، تغییر ویژگی پارتیشن ممکن است بر همه پارتیشن‌های همان دیسک اثر کند. ویژگی فقط‌خواندنی تغییر می‌کند. ابتدا داده مهم را پشتیبان بگیرید.\nChanges the read-only attribute. Back up important data first."
   };
   if(!Confirm(device.Display+"\n"+(needsVolume?volume!.Display+"\n":"")+warning,token)) return;
  }
  await Work(async()=>{
   var fs=(FileSystems.SelectedItem as ComboBoxItem)?.Content.ToString()??"exFAT";
   var result=await backend.RunAsync(new{Action=action,Disk=device,Volume=volume,Consent=token,FileSystem=fs});
   findings.Clear();
   if(action=="Diagnose"&&result.GetProperty("Success").GetBoolean())findings.AddRange(Diagnosis.Analyze(result.GetProperty("Data")));
   Record(action,device.Display+(needsVolume?" / "+volume!.Display:""),result);
   if(action!="Diagnose"&&action!="Check"){
    var refresh=await backend.RunAsync(new{Action="Scan"});
    if(refresh.GetProperty("Success").GetBoolean()){
     var list=refresh.GetProperty("Data").GetProperty("Devices").Deserialize<Device[]>(JsonDefaults.Options)??[];
     Devices.ItemsSource=list;
     Devices.SelectedItem=list.FirstOrDefault(x=>x.UniqueId==device.UniqueId&&x.Path==device.Path&&x.Serial==device.Serial&&x.Size==device.Size);
     if(volume!=null)Volumes.SelectedItem=Selected?.Volumes.FirstOrDefault(x=>x.Id==volume.Id&&x.Letter==volume.Letter);
    }
   }
   Summary.Text=T(ResultText.Describe(action,result).Fa,ResultText.Describe(action,result).En);
  });
 }
 bool Confirm(string warning,string token){
  var dialog=new Window{Title=T("تأیید عملیات","Confirm operation"),Owner=this,Width=700,Height=500,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize};
  var panel=new StackPanel{Margin=new Thickness(24)}; panel.Children.Add(new TextBlock{Text=warning,TextWrapping=TextWrapping.Wrap});
  panel.Children.Add(new TextBlock{Text="برای تأیید عبارت زیر را تایپ کنید / Type exactly:\n"+token,Margin=new Thickness(0,18,0,8),TextWrapping=TextWrapping.Wrap});
  var input=new TextBox{FontSize=17,Padding=new Thickness(6)};panel.Children.Add(input);
  var ok=new Button{Content=T("تأیید و اجرا","Confirm & execute"),IsEnabled=false}; var cancel=new Button{Content=T("انصراف","Cancel"),IsCancel=true};
  input.TextChanged+=(_,_)=>ok.IsEnabled=input.Text==token; ok.Click+=(_,_)=>dialog.DialogResult=true;
  panel.Children.Add(ok);panel.Children.Add(cancel);dialog.Content=panel; return dialog.ShowDialog()==true;
 }
 async Task Work(Func<Task> action){
  if(busy)return;busy=true;Controls.IsEnabled=false;ExportButton.IsEnabled=false;Status.Text=T("در حال اجرا؛ دستگاه را جدا نکنید…","Working; do not disconnect the device…");
  try{await action();Status.Text=T("پایان؛ نتیجه را در گزارش بررسی کنید.","Finished; review the report for the result.");}
  catch(Exception ex){var detail=JsonSerializer.SerializeToElement(new{ex.Message,HResult=ex.HResult});entries.Add(new(DateTimeOffset.Now,"Backend","",false,"BACKEND_ERROR","Operation failed or outcome is unknown. Inspect the device before retrying.","عملیات ناموفق یا نتیجه نامشخص است؛ پیش از تلاش مجدد دستگاه را بررسی کنید.",detail));Render();Status.Text=T("خطا؛ گزارش را بررسی کنید.","Error; inspect report.");}
  finally{busy=false;Controls.IsEnabled=true;ExportButton.IsEnabled=true; var lastSummary=Summary.Text;UpdateActions();Summary.Text=lastSummary;}
 }
 void Record(string action,string target,JsonElement result){
  bool success=result.GetProperty("Success").GetBoolean();string code=result.GetProperty("Code").GetString()??"UNKNOWN";
  var explanation=ResultText.Describe(action,result);
  entries.Add(new(DateTimeOffset.Now,action,target,success,code,explanation.En,explanation.Fa,result,findings.ToArray()));Render();
 }
 void Render(){
  var b=new StringBuilder().AppendLine("usb zard — "+(demo?"DEMO / نمایشی":"Session report / گزارش نشست")).AppendLine(Diagnosis.LimitationFa).AppendLine(Diagnosis.LimitationEn);
  foreach(var f in findings)b.AppendLine($"\n[{f.Code}] {f.Confidence}\n{f.Persian}\n{f.English}\n{f.Next}");
  foreach(var e in entries){
   b.AppendLine($"\n{e.Time:O} | {e.Action} | {e.Code} | {(e.Success?"COMPLETED / تکمیل":"FAILED / خطا")}\n{e.Target}\n{e.Persian}\n{e.English}");
   if(e.Findings!=null)foreach(var f in e.Findings)b.AppendLine($"[{f.Code}] {f.Persian}\n{f.English}\n{f.Next}");
  }
  Report.Text=b.ToString(); Raw.Text=JsonSerializer.Serialize(new{Demo=demo,Limitations=new[]{Diagnosis.LimitationFa,Diagnosis.LimitationEn},Findings=findings,Actions=entries},JsonDefaults.Options);
 }
 void DemoClick(object s,RoutedEventArgs e){
  if(entries.Count>0 && MessageBox.Show(T("ورود به نمونه، گزارش نشست فعلی را پاک می‌کند. ابتدا گزارش را ذخیره کنید. ادامه می‌دهید؟","Demo clears the current session report. Export it first. Continue?"),"Demo",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;
  demo=true;Devices.ItemsSource=null;entries.Clear();findings.Clear();UpdateActions();
  using var doc=JsonDocument.Parse("""{"Disk":{"IsReadOnly":true,"IsOffline":false},"WriteProtect":1,"GroupPolicies":[],"Reliability":null}""");
  findings.AddRange(Diagnosis.Analyze(doc.RootElement));Render();Status.Text=T("نمایش نمونه؛ هیچ دستگاهی خوانده یا تغییر داده نشد.","Demo only; no device was accessed.");
 }
 void ExportClick(object s,RoutedEventArgs e){
  var dlg=new SaveFileDialog{FileName="usb-zard-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"),Filter="Bilingual text report (*.txt)|*.txt|JSON report (*.json)|*.json",AddExtension=true};
  if(dlg.ShowDialog()!=true)return;
  try{File.WriteAllText(dlg.FileName,dlg.FilterIndex==2?Raw.Text:Report.Text+"\n\n=== Raw details / جزئیات خام ===\n"+Raw.Text,new UTF8Encoding(true));Status.Text=T("گزارش ذخیره شد؛ شامل شناسه دستگاه است.","Report saved; it contains device identifiers.");}
  catch(Exception ex){MessageBox.Show(ex.Message);}
 }
 void OnClosing(object? s,CancelEventArgs e){if(busy){e.Cancel=true;MessageBox.Show(T("تا پایان عملیات برنامه را نبندید.","Wait for the operation to finish before closing."));}}
}