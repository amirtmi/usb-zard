using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
namespace UsbWriteGuard;
public sealed class Backend {
 public async Task<JsonElement> RunAsync(object request) {
  var asm=Assembly.GetExecutingAssembly();
  string Read(string suffix) { using var r=new StreamReader(asm.GetManifestResourceStream(asm.GetManifestResourceNames().Single(n=>n.EndsWith(suffix)))!); return r.ReadToEnd(); }
  var script=Read("Guard.ps1")+"\n"+Read("Native.ps1")+"\n"+Read("Worker.ps1");
  // Only a fixed bootstrap is placed on the command line; the full trusted
  // embedded script and a separately serialized request travel through stdin.
  // This avoids Windows' 32767-character command-line limit.
  const string bootstrap="$ErrorActionPreference='Stop'; [Console]::InputEncoding=New-Object Text.UTF8Encoding($false); [Console]::OutputEncoding=New-Object Text.UTF8Encoding($false); $envelope=[Console]::In.ReadToEnd()|ConvertFrom-Json; $script:RequestJson=$envelope.Request; & ([ScriptBlock]::Create($envelope.Script))";
  var exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");
  var info=new ProcessStartInfo(exe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,StandardInputEncoding=new UTF8Encoding(false)};
  foreach(var a in new[]{"-NoLogo","-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(bootstrap))})info.ArgumentList.Add(a);
  using var p=Process.Start(info)??throw new InvalidOperationException("PowerShell could not start.");
  var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();
  await p.StandardInput.WriteAsync(JsonSerializer.Serialize(new{Script=script,Request=JsonSerializer.Serialize(request)}));p.StandardInput.Close();
  await p.WaitForExitAsync();var output=await stdout;var error=await stderr;
  if(p.ExitCode!=0)throw new InvalidOperationException($"PowerShell exit {p.ExitCode}: {error}");
  try {using var doc=JsonDocument.Parse(output.TrimStart('\uFEFF').Trim());return doc.RootElement.Clone();}
  catch(JsonException ex){throw new InvalidOperationException($"Invalid backend response. {error}\n{output}",ex);}
 }
}
