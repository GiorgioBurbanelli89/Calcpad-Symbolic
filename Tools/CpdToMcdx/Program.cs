using System;
using System.IO;

namespace CpdToMcdx;

class Program
{
    static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        string input = args[0];
        string? output = null;
        string version = "9.0";
        bool htmlPreview = false;
        string? htmlOutput = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--version" or "-v" when i + 1 < args.Length:
                    version = args[++i];
                    break;
                case "--html" when i + 1 < args.Length:
                    htmlPreview = true;
                    htmlOutput = args[++i];
                    break;
                case "--html":
                    htmlPreview = true;
                    break;
                default:
                    if (output == null) output = args[i];
                    break;
            }
        }

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Error: File not found: {input}");
            return 1;
        }

        string ext = Path.GetExtension(input).ToLowerInvariant();

        try
        {
            if (ext == ".cpd")
            {
                // CPD → MCDX conversion
                var regions = CpdParser.Parse(File.ReadAllText(input));
                Console.WriteLine($"Parsed {regions.Count} regions from CPD");

                // Always generate HTML preview and open in browser
                string htmlPath = htmlOutput ?? Path.ChangeExtension(input, "_preview.html");
                string html = HtmlPreview.Generate(regions, Path.GetFileNameWithoutExtension(input));
                File.WriteAllText(htmlPath, html);
                Console.WriteLine($"HTML preview: {htmlPath}");
                OpenInBrowser(htmlPath);

                string mcdxPath = output ?? Path.ChangeExtension(input, ".mcdx");
                McdxWriter.Write(mcdxPath, regions, version);
                Console.WriteLine($"MCDX written: {mcdxPath} (MathCad Prime {version})");
            }
            else if (ext == ".mcdx")
            {
                // MCDX → CPD conversion
                var regions = McdxReader.Read(input);
                Console.WriteLine($"Parsed {regions.Count} regions from MCDX");

                // Always generate HTML preview and open in browser
                string htmlPath = htmlOutput ?? Path.ChangeExtension(input, "_preview.html");
                string html = HtmlPreview.Generate(regions, Path.GetFileNameWithoutExtension(input));
                File.WriteAllText(htmlPath, html);
                Console.WriteLine($"HTML preview: {htmlPath}");
                OpenInBrowser(htmlPath);

                string cpdPath = output ?? Path.ChangeExtension(input, ".cpd");
                string cpd = CpdWriter.Write(regions);
                File.WriteAllText(cpdPath, cpd);
                Console.WriteLine($"CPD written: {cpdPath}");
            }
            else
            {
                Console.Error.WriteLine($"Error: Unsupported file type: {ext}");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        return 0;
    }

    static void OpenInBrowser(string htmlPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(htmlPath);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            Console.WriteLine($"Opened in browser: {fullPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not open browser: {ex.Message}");
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine(@"
CpdToMcdx — Bidirectional converter: Calcpad (.cpd) ↔ MathCad Prime (.mcdx)

Usage:
  CpdToMcdx input.cpd [output.mcdx] [--version 9.0] [--html preview.html]
  CpdToMcdx input.mcdx [output.cpd] [--html preview.html]

Options:
  --version, -v  MathCad Prime version (7.0, 8.0, 9.0, 10.0). Default: 9.0
  --html         Generate HTML preview of the document
  -h, --help     Show this help

Examples:
  CpdToMcdx zapata.cpd                           → zapata.mcdx (Prime 9.0)
  CpdToMcdx zapata.cpd zapata.mcdx --version 10.0
  CpdToMcdx clase04.mcdx                         → clase04.cpd
  CpdToMcdx zapata.cpd --html zapata.html         → HTML preview only
  CpdToMcdx clase04.mcdx --html clase04.html      → HTML from MCDX

Author: Jorge Burbano — Hekatan Calc
");
    }
}
