using Calcpad.Core;
using System.Text;

namespace CpdToMcdx;

/// <summary>Generate HTML preview using Calcpad's own parser (same output as Calcpad WPF/CLI)</summary>
static class HtmlPreview
{
    /// <summary>Generate HTML from .cpd source code using Calcpad's ExpressionParser</summary>
    public static string Generate(string cpdSource, string title)
    {
        var sb = new StringBuilder();

        // Use Calcpad's ExpressionParser to generate HTML (identical to Calcpad CLI)
        var settings = new Settings();
        settings.Math.Decimals = 6;

        // Pre-process with MacroParser
        var macroParser = new MacroParser();
        macroParser.Parse(cpdSource, out var unwrappedCode, null, 0, true);

        var parser = new ExpressionParser()
        {
            Settings = settings
        };

        try
        {
            parser.Parse(unwrappedCode ?? cpdSource, true, false);
        }
        catch
        {
            // If parse fails, try without calculation
            try
            {
                parser.Parse(unwrappedCode ?? cpdSource, false, false);
            }
            catch { }
        }

        var htmlResult = parser.HtmlResult ?? "<p>Error parsing</p>";

        // Build complete HTML using Calcpad's template structure
        sb.Append(GetHtmlTemplate(title));
        sb.Append(htmlResult);
        sb.Append(" </body></html>");

        return sb.ToString();
    }

    /// <summary>Generate HTML from Region list (fallback without Calcpad parser)</summary>
    public static string Generate(List<Region> regions, string title)
    {
        // Convert regions to CPD source and use the Calcpad parser
        var cpdSource = CpdWriter.Write(regions);
        return Generate(cpdSource, title);
    }

    /// <summary>Get HTML template (same as Calcpad's template.html)</summary>
    static string GetHtmlTemplate(string title)
    {
        // Look for template.html in known locations
        var templatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "doc", "template.html"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Symbolic.Cli", "doc", "template.html"),
            // Relative to CpdToMcdx project
            FindCalcpadTemplate()
        };

        foreach (var path in templatePaths)
        {
            if (path != null && File.Exists(path))
            {
                var template = File.ReadAllText(path);
                // Embed JS files inline
                var docPath = Path.GetDirectoryName(path)! + Path.DirectorySeparatorChar;
                template = EmbedScript(template, "jquery-3.6.3.min.js", docPath);
                template = EmbedScript(template, "calcpad-viz.umd.js", docPath);
                return template;
            }
        }

        // Fallback: minimal HTML template matching Calcpad style
        return $@"<!DOCTYPE html>
<html><head>
<meta charset=""utf-8"">
<title>Created with Calcpad — {System.Web.HttpUtility.HtmlEncode(title)}</title>
<style>
body {{ font-family: 'Segoe UI', Calibri, sans-serif; max-width: 210mm; margin: 1em auto; padding: 0 2em; line-height: 1.4; font-size: 16px; }}
h1 {{ color: #333; border-bottom: 2px solid #ccc; padding-bottom: 0.3em; font-size: 20pt; }}
h2 {{ color: #555; font-size: 16pt; }}
h3 {{ color: #666; font-size: 14pt; }}
.eq {{ margin: 4px 0; }}
.cond {{ margin: 4px 0; }}
table {{ border-collapse: collapse; margin: 8px 0; }}
td, th {{ border: 1px solid #ccc; padding: 4px 8px; text-align: right; }}
th {{ background: #f0f0f0; }}
</style>
</head><body>
";
    }

    /// <summary>Find template.html from Calcpad installation</summary>
    static string? FindCalcpadTemplate()
    {
        // Search up from current directory
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;

            // Check for Symbolic.Cli/doc/template.html
            var candidate = Path.Combine(dir, "Symbolic.Cli", "doc", "template.html");
            if (File.Exists(candidate)) return candidate;

            // Check for Symbolic.Wpf/doc/template.html
            candidate = Path.Combine(dir, "Symbolic.Wpf", "doc", "template.html");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>Replace script src reference with inline script content</summary>
    static string EmbedScript(string html, string fileName, string docPath)
    {
        var scriptTag = $"<script src=\"https://calcpad.local/{fileName}\"></script>";
        var filePath = Path.Combine(docPath, fileName);
        if (File.Exists(filePath))
        {
            var content = File.ReadAllText(filePath);
            return html.Replace(scriptTag, $"<script>{content}</script>");
        }
        return html;
    }
}
