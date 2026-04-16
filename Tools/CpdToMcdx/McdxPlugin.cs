using Calcpad.Core.Plugins;

namespace CpdToMcdx;

/// <summary>
/// Plugin implementation for Calcpad Symbolic.
/// Loaded dynamically from Plugins/ folder.
/// </summary>
public class McdxPlugin : ICalcpadPlugin
{
    public string Name => "MathCad Prime Converter";
    public string Version => "1.0.0";
    public string ExportMenuText => "Export to MathCad Prime (.mcdx)";
    public string ImportMenuText => "Import from MathCad Prime (.mcdx)";
    public string FileFilter => "MathCad Prime (*.mcdx)|*.mcdx";
    public string DefaultExtension => ".mcdx";
    public bool CanExport => true;
    public bool CanImport => true;
    public string ToolTip => "MathCad Prime Converter (CPD ↔ MCDX)";

    public string Export(string cpdContent, string outputPath, Dictionary<string, string>? options = null)
    {
        string version = options?.GetValueOrDefault("version", "9.0") ?? "9.0";
        var regions = CpdParser.Parse(cpdContent);
        McdxWriter.Write(outputPath, regions, version);

        // Generate HTML preview
        var htmlPath = Path.ChangeExtension(outputPath, ".html");
        var html = HtmlPreview.Generate(regions, Path.GetFileNameWithoutExtension(outputPath));
        File.WriteAllText(htmlPath, html);
        OpenBrowser(htmlPath);

        return $"Exported {regions.Count} regions to {outputPath} (MathCad Prime {version})";
    }

    public string Import(string inputPath)
    {
        var regions = McdxReader.Read(inputPath);

        // Generate HTML preview
        var htmlPath = Path.ChangeExtension(inputPath, "_preview.html");
        var html = HtmlPreview.Generate(regions, Path.GetFileNameWithoutExtension(inputPath));
        File.WriteAllText(htmlPath, html);
        OpenBrowser(htmlPath);

        return CpdWriter.Write(regions);
    }

    public string GenerateHtmlPreview(string content, string title)
    {
        var regions = CpdParser.Parse(content);
        return HtmlPreview.Generate(regions, title);
    }

    private static void OpenBrowser(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.GetFullPath(path),
                UseShellExecute = true
            });
        }
        catch { }
    }
}
