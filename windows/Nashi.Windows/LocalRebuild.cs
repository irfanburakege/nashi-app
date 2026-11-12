using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
namespace Nashi.Windows;
/// Rebuilds Nashi from a local source checkout with `dotnet publish`, then swaps the
/// running app folder for the new build and restarts. Needs the .NET SDK installed.
internal static class LocalRebuild
{
    private const string Project="windows/Nashi.Windows/Nashi.Windows.csproj";
    private static readonly string Remembered=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nashi","source-path.txt");
    private static string? FindSource()
    {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent)
            if(File.Exists(Path.Combine(dir.FullName,Project)))return dir.FullName;
        try{var saved=File.ReadAllText(Remembered).Trim();if(File.Exists(Path.Combine(saved,Project)))return saved;}catch{}
        return null;
    }
    public static async Task RunAsync(Window owner)
    {
        var source=FindSource();
        if(source is null)
        {
            var dialog=new OpenFolderDialog{Title=L10n.Text("Choose the nashi-app source folder")};
            if(dialog.ShowDialog(owner)!=true)return;
            if(!File.Exists(Path.Combine(dialog.FolderName,Project))){MessageBox.Show(owner,L10n.Text("That folder doesn’t contain the Nashi source (windows/Nashi.Windows)."),L10n.Text("Rebuild from source"));return;}
            source=dialog.FolderName;Directory.CreateDirectory(Path.GetDirectoryName(Remembered)!);File.WriteAllText(Remembered,source);
        }
        if(MessageBox.Show(owner,L10n.Text("Build Nashi from "+source+" and restart? This takes a minute or two."),L10n.Text("Rebuild from source"),MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
        var output=Path.Combine(Path.GetTempPath(),"Nashi-rebuild-"+Guid.NewGuid().ToString("N"));
        var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=source};
        foreach(var a in new[]{"publish",Path.Combine(source,Project),"-c","Release","-o",output,"-nologo"})start.ArgumentList.Add(a);
        Process? process=null;var log=new System.Text.StringBuilder();
        var toast=new ToastWindow(owner,()=>{try{process?.Kill(true);}catch{}});toast.Update(new(null,"Building Nashi from source…"));toast.Show();
        try
        {
            try{process=Process.Start(start);}catch(Exception e){throw new IOException("Couldn’t start dotnet. Install the .NET 10 SDK first.",e);}
            if(process is null)throw new IOException("Couldn’t start dotnet.");
            process.OutputDataReceived+=(_,e)=>{if(e.Data is not null)lock(log)log.AppendLine(e.Data);};process.ErrorDataReceived+=(_,e)=>{if(e.Data is not null)lock(log)log.AppendLine(e.Data);};
            process.BeginOutputReadLine();process.BeginErrorReadLine();await process.WaitForExitAsync();
            if(process.ExitCode!=0||!File.Exists(Path.Combine(output,"Nashi.exe"))){var text=log.ToString();throw new IOException("Build failed.\n\n"+(text.Length>1500?text[^1500..]:text));}
        }
        catch(Exception error){toast.Close();MessageBox.Show(owner,error.Message,L10n.Text("Rebuild from source"));return;}
        finally{process?.Dispose();}
        toast.Close();
        // A helper script waits for this process to exit, copies the new build over the app folder and relaunches it.
        var target=Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);var script=Path.Combine(output,"..","Nashi-apply-"+Path.GetFileName(output)+".ps1");
        File.WriteAllText(script,$$"""
            $ErrorActionPreference='Stop'
            Wait-Process -Id {{Environment.ProcessId}} -ErrorAction SilentlyContinue
            Copy-Item -Path '{{output.Replace("'","''")}}\*' -Destination '{{target.Replace("'","''")}}' -Recurse -Force
            Remove-Item -LiteralPath '{{output.Replace("'","''")}}' -Recurse -Force -ErrorAction SilentlyContinue
            Start-Process -FilePath '{{Path.Combine(target,"Nashi.exe").Replace("'","''")}}' -WorkingDirectory '{{target.Replace("'","''")}}'
            Remove-Item -LiteralPath $PSCommandPath -Force
            """);
        var helper=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true};
        foreach(var a in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-WindowStyle","Hidden","-File",Path.GetFullPath(script)})helper.ArgumentList.Add(a);
        Process.Start(helper)?.Dispose();Application.Current.Shutdown();
    }
}
