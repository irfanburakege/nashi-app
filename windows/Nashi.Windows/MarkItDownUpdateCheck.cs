using System.Diagnostics;
namespace Nashi.Windows;

/// Fork-only: when Nashi opens, runs Update-MarkItDown.ps1 hidden in the background. The script lives in the folder
/// that holds nashi-app (next to Update-Nashi.ps1), checks PyPI at most once a day and asks before installing anything.
/// Set NASHI_NO_MARKITDOWN_UPDATE_CHECK to turn it off.
internal static class MarkItDownUpdateCheck
{
    public static void Start()
    {
        try
        {
            // Test runs set NASHI_SMOKE_DIRECTORY; they must never pop up an update question.
            if(Environment.GetEnvironmentVariable("NASHI_NO_MARKITDOWN_UPDATE_CHECK") is not null||Environment.GetEnvironmentVariable("NASHI_SMOKE_DIRECTORY") is not null)return;
            var script=FindScript();if(script is null)return;
            var info=new ProcessStartInfo("pwsh.exe"){UseShellExecute=false,CreateNoWindow=true};
            foreach(var a in new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-FromNashi","-NashiExe",Environment.ProcessPath??""})info.ArgumentList.Add(a);
            Process.Start(info)?.Dispose();
        }
        catch(Exception error)when(error is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException){}// No PowerShell 7: just skip the check.
    }

    // The installed Nashi.exe (nashi-app\windows\Nashi.Windows\bin\Release\<tfm>\win-x64) is 7 folders below the
    // fork folder that holds the script; the Nashi-previous backup is 1 below it.
    private static string? FindScript()
    {
        var folder=new DirectoryInfo(AppContext.BaseDirectory);
        for(var level=0;folder is not null&&level<8;level++,folder=folder.Parent)
        {var candidate=Path.Combine(folder.FullName,"Update-MarkItDown.ps1");if(File.Exists(candidate))return candidate;}
        return null;
    }
}
