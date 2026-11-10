using System.Text;
namespace Nashi.Windows;

/// Fork-only self-check run by "Nashi.exe --check-markitdown" (the update script calls it before installing):
/// the MarkItDown tool is still offered for its file types and converts a sample through the installed markitdown.exe.
internal static class MarkItDownChecks
{
    public static void Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"Nashi-markitdown-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            foreach(var ext in new[]{"pdf","docx","pptx","xlsx","html"})
            {var sample=Path.Combine(root,"Offered."+ext);File.WriteAllBytes(sample,[]);if(!MarkItDownConversion.Supports([sample]))throw new InvalidOperationException("Markdown (MarkItDown) is no longer offered for ."+ext+" files.");}
            if(MarkItDownConversion.Find() is null)throw new InvalidOperationException("markitdown.exe was not found. Install it with: pip install \"markitdown[all]\"");
            var input=Path.Combine(root,"Sample.html");File.WriteAllText(input,"<html><body><h1>Nashi check</h1><p>Merhaba dünya</p></body></html>",new UTF8Encoding(false));
            var output=Path.Combine(root,"out");Directory.CreateDirectory(output);
            // Task.Run keeps the conversion off the WPF dispatcher, so blocking here cannot deadlock.
            var outputs=Task.Run(()=>MarkItDownConversion.ConvertAsync([input],output,null,CancellationToken.None)).GetAwaiter().GetResult();
            if(outputs.Count!=1||Path.GetExtension(outputs[0])!=".md")throw new InvalidOperationException("MarkItDown did not produce one .md file.");
            var text=File.ReadAllText(outputs[0],Encoding.UTF8);
            if(!text.Contains("# Nashi check")||!text.Contains("Merhaba dünya"))throw new InvalidOperationException("MarkItDown output is wrong: "+text);
        }
        finally{try{Directory.Delete(root,true);}catch(IOException){}}
    }
}
