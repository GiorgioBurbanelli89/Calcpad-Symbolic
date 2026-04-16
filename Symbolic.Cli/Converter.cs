using Calcpad.OpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Calcpad.Cli
{
    internal class Converter
    {
        private readonly StringBuilder _sb = new();
        private readonly string _htmlWorksheet;
        private readonly bool _isSilent;

        internal Converter(bool isSilent)
        {
            var docPath = $"{Program.AppPath}doc{Path.DirectorySeparatorChar}";
            var templatePath = $"{docPath}template{Program.AddCultureExt("html")}";
            var template = File.ReadAllText(templatePath);

            // Embed JS files inline instead of referencing via file:// (browsers block cross-origin file:// scripts)
            template = EmbedScript(template, "jquery-3.6.3.min.js", docPath);
            template = EmbedScript(template, "calcpad-viz.umd.js", docPath);

            _htmlWorksheet = template;
            _isSilent = isSilent;
        }

        /// <summary>Replace script src reference with inline script content</summary>
        private static string EmbedScript(string html, string fileName, string docPath)
        {
            var scriptTag = $"<script src=\"https://calcpad.local/{fileName}\"></script>";
            var filePath = $"{docPath}{fileName}";
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                return html.Replace(scriptTag, $"<script>{content}</script>");
            }
            // Fallback to file:// URL if file not found
            var appUrl = $"file:///{docPath.Replace("\\", "/")}";
            return html.Replace("https://calcpad.local/", appUrl);
        }

        internal void ToHtml(string html, string path)
        {
            File.WriteAllText(path, HtmlApplyWorksheet(html));
            if (!_isSilent && File.Exists(path))
                Run(path);
        }

        internal void ToOpenXml(string html, string path, List<string> expressions)
        {
            html = GetHtmlData(HtmlApplyWorksheet(html));
            new OpenXmlWriter(expressions).Convert(html, path);
            if (!_isSilent && File.Exists(path))
                Run(path);
        }
        internal void ToPdf(string html, string path)
        {
            var htmlFile = Path.ChangeExtension(path, ".html");
            File.WriteAllText(htmlFile, HtmlApplyWorksheet(html));
            
            string wkhtmltopdfPath;

            if (OperatingSystem.IsWindows())
            {
                wkhtmltopdfPath = Program.AppPath + "wkhtmltopdf.exe";
            }
            else
            {
                wkhtmltopdfPath = "/usr/bin/wkhtmltopdf";
                
                if (!File.Exists("/usr/bin/wkhtmltopdf"))
                {
                    throw new DirectoryNotFoundException("wkhtmltopdf not found.");
                }
            }
            
            var startInfo = new ProcessStartInfo
            {
                FileName = wkhtmltopdfPath
            };
            const string s = " --enable-local-file-access --disable-smart-shrinking --page-size A4  --margin-bottom 15 --margin-left 15 --margin-right 10 --margin-top 15 ";
            if (htmlFile.Contains(' ', StringComparison.Ordinal))
                startInfo.Arguments = s + '\"' + htmlFile + "\" \"" + path + '\"';
            else
                startInfo.Arguments = s + htmlFile + " " + path;

            startInfo.UseShellExecute = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            var process = Process.Start(startInfo);
            process?.WaitForExit();
            
            File.Delete(htmlFile);
            if (!_isSilent && File.Exists(path))
                Run(path);
        }

        private static void Run(string fileName) 
        {
            Process process = new()
            {
                StartInfo = new ProcessStartInfo(fileName)
                {
                    UseShellExecute = true
                }
            };
            process.Start();
        }

        private string HtmlApplyWorksheet(string s)
        {
            
            _sb.Append(_htmlWorksheet);
            _sb.Append(s);
            _sb.Append(" </body></html>");
            return _sb.ToString();
        }

        private static string GetHtmlData(string html)
        {
            var sb = new StringBuilder(500);
            const string header =
@"Version:1.0
StartHTML:0000000001
EndHTML:0000000002
StartFragment:0000000003
EndFragment:0000000004";
            const string startFragmentText = "<!DOCTYPE HTML><!--StartFragment-->";
            const string endFragmentText = "<!--EndFragment-->";
            var startHtml = header.Length;
            var startFragment = startHtml + startFragmentText.Length;
            var endFragment = startFragment + html.Length;
            var endHtml = endFragment + endFragmentText.Length;
            sb.Append(header);
            sb.Replace("0000000001", $"{startHtml,8}");
            sb.Replace("0000000002", $"{endHtml,8}");
            sb.Replace("0000000003", $"{startFragment,8}");
            sb.Replace("0000000004", $"{endFragment,8}");
            sb.Append(startFragmentText);
            sb.Append(html);
            sb.Append(endFragmentText);
            return sb.ToString();
        }
    }
}
