using Nashi.Core;
namespace Nashi.Windows;

/// Converts documents to Markdown with Microsoft's MarkItDown CLI (https://github.com/microsoft/markitdown, MIT).
/// MarkItDown is not bundled: Nashi runs the markitdown.exe the user installed with pip.
public static class MarkItDownConversion
{
    public const string ToolId="markitdown";
    public const string Title="Markdown (MarkItDown)";
    private static readonly string[] Supported=["pdf","docx","pptx","xlsx","xls","html","csv","json","xml","epub","ipynb","msg","zip"];

    public static bool Supports(IReadOnlyList<string> inputs)=>inputs.Count>0&&inputs.All(p=>File.Exists(p)&&Supported.Contains(FeatureCatalog.Extension(p)));

    /// NASHI_MARKITDOWN overrides the lookup; then PATH; then the usual per-user pip Scripts folders.
    public static string? Find()
    {
        var custom=Environment.GetEnvironmentVariable("NASHI_MARKITDOWN");if(!string.IsNullOrWhiteSpace(custom)&&File.Exists(custom))return Path.GetFullPath(custom);
        foreach(var folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {
            try{var candidate=Path.Combine(folder.Trim('"'),"markitdown.exe");if(Path.IsPathFullyQualified(candidate)&&File.Exists(candidate))return candidate;}catch(ArgumentException){}
        }
        foreach(var root in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Python"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Python")})
        {
            if(!Directory.Exists(root))continue;
            foreach(var python in Directory.GetDirectories(root,"Python3*").OrderByDescending(d=>d,StringComparer.OrdinalIgnoreCase))
            {var candidate=Path.Combine(python,"Scripts","markitdown.exe");if(File.Exists(candidate))return candidate;}
        }
        return null;
    }

    public static async Task<IReadOnlyList<string>> ConvertAsync(IReadOnlyList<string> inputs,string destination,IProgress<OperationProgress>? progress,CancellationToken token)
    {
        var executable=Find()??throw new InvalidOperationException("MarkItDown isn't installed. Install it with: pip install \"markitdown[all]\"");
        using var tx=new ExportTransaction(destination);var outputs=new List<string>();
        for(var i=0;i<inputs.Count;i++)
        {
            token.ThrowIfCancellationRequested();var input=inputs[i];
            progress?.Report(new((double)i/inputs.Count*.98,"Converting "+Path.GetFileName(input)+" to Markdown…"));
            var output=SafePaths.Unique(tx.StagingDirectory,Path.GetFileNameWithoutExtension(input)+".md");
            // A full path never starts with "-", so a file name can't be read as a MarkItDown option.
            try{await ProcessRunner.RunAsync(executable,["-o",output,Path.GetFullPath(input)],token);}
            catch(IOException error){throw new IOException("MarkItDown couldn't convert "+Path.GetFileName(input)+": "+LastLine(error.Message),error);}
            if(!File.Exists(output))throw new IOException("MarkItDown produced no output for "+Path.GetFileName(input)+".");
            outputs.Add(output);
        }
        return tx.Publish(outputs,token);
    }

    // MarkItDown failures are Python tracebacks; the last line names the actual problem.
    private static string LastLine(string text)=>text.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).LastOrDefault()??text;
}
