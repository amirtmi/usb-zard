using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UsbWriteGuard;
internal static class Program {
 [STAThread] static void Main(string[] args) {
  var app=new App();app.InitializeComponent();var w=new MainWindow();
  ((Button)w.FindName("DemoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var report=(TextBox)w.FindName("Report");
  if(!report.Text.Contains("DEMO")||!report.Text.Contains("LOCAL_POLICY"))throw new Exception("Demo report failed");
  var dir=args.Length>0?args[0]:".";Directory.CreateDirectory(dir);
  foreach(var language in new[]{0,1}){
   ((ComboBox)w.FindName("LanguagePicker")).SelectedIndex=language;
   var root=(Grid)w.Content;root.Background=w.Background;root.Margin=new Thickness(0);
   root.Measure(new Size(1100,850));root.Arrange(new Rect(0,0,1100,850));root.UpdateLayout();
   var image=new RenderTargetBitmap(1100,850,96,96,PixelFormats.Pbgra32);image.Render(root);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
   using var stream=File.Create(Path.Combine(dir,language==0?"preview-fa.png":"preview-en.png"));encoder.Save(stream);
   Console.WriteLine("PASS UI layout and demo language "+language);
  }
  // Model a synthetic RAW-device case without issuing storage commands.
  var real=new MainWindow();
  var device=new Device{Number=2,Name="Mass Storage Device",Serial="DEMO-SERIAL-0001",Size=34359738368,Bus="USB",ReadOnly=false,Volumes=[new VolumeInfo{Letter="E",Id="test",PartitionNumber=1,FileSystem="Unknown",PartitionSize=34359738368,VolumeSize=0,ReadOnly=null}]};
  var devices=(ComboBox)real.FindName("Devices");devices.ItemsSource=new[]{device};devices.SelectedIndex=0;
  ((ComboBox)real.FindName("Volumes")).SelectedIndex=0;
  foreach(var name in new[]{"ClearButton","CheckButton","RepairButton","ProbeButton","VolumeButton"})
   if(((Button)real.FindName(name)).IsEnabled)throw new Exception("RAW/absent-lock action should be disabled: "+name);
  if(!((Button)real.FindName("FormatButton")).IsEnabled)throw new Exception("Explicit format must remain available for RAW.");
  if(!((TextBlock)real.FindName("Summary")).Text.Contains("RAW"))throw new Exception("RAW explanation missing");
  Console.WriteLine("PASS RAW UI gates and summary");
  real.Close();w.Close();app.Shutdown();
 }
}
