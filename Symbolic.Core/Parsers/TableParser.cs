using System;
using System.Collections.Generic;
using System.Text;

namespace Calcpad.Core
{
    /// <summary>
    /// Parses $Table commands to generate HTML tables from vectors and matrices.
    ///
    /// Syntax:
    ///   $Table{v1; v2; v3}                       — columns from vectors, no headers
    ///   $Table{v1; v2; v3 @ "H1"; "H2"; "H3"}   — columns from vectors with headers
    ///   $Table{M}                                 — matrix to table
    ///   $Table{M @ "H1"; "H2"; "H3"}             — matrix with column headers
    ///   $Table{v1; v2 @ "H1"; "H2" & fmt=3}      — with format (decimal places)
    ///
    /// Options after &amp;:
    ///   fmt=N     — number of decimal places (default: 4)
    ///   row=1     — show row numbers (default: 0)
    ///   border=0  — hide borders (default: 1)
    ///   zebra=0   — disable alternating row colors (default: 1)
    /// </summary>
    internal class TableParser : PlotParser
    {
        internal TableParser(MathParser parser, PlotSettings settings) : base(parser, settings) { }

        internal override string Parse(ReadOnlySpan<char> script, bool calculate)
        {
            // Find the { } block
            int braceStart = script.IndexOf('{');
            int braceEnd = script.LastIndexOf('}');
            if (braceStart < 0 || braceEnd < 0 || braceEnd <= braceStart)
                throw new MathParserException("$Table syntax: $Table{data @ headers & options}");

            var content = script[(braceStart + 1)..braceEnd].Trim();

            if (!calculate)
            {
                return $"<span class=\"eq\"><span class=\"cond\">$Table</span>{{{content.ToString()}}}</span>";
            }

            var contentStr = content.ToString();

            // Split by & first (options)
            string optionSection = "";
            int ampIdx = FindTopLevelChar(contentStr, '&');
            if (ampIdx >= 0)
            {
                optionSection = contentStr[(ampIdx + 1)..].Trim();
                contentStr = contentStr[..ampIdx].Trim();
            }

            // Split by @ (headers)
            string headerSection = "";
            string dataSection;
            int atIdx = FindTopLevelChar(contentStr, '@');
            if (atIdx >= 0)
            {
                headerSection = contentStr[(atIdx + 1)..].Trim();
                dataSection = contentStr[..atIdx].Trim();
            }
            else
            {
                dataSection = contentStr.Trim();
            }

            // Parse options
            int decimals = 4;
            bool showRowNumbers = false;
            bool showBorders = true;
            bool zebraRows = true;
            if (!string.IsNullOrEmpty(optionSection))
            {
                foreach (var opt in optionSection.Split('&'))
                {
                    var parts = opt.Trim().Split('=');
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim().ToLowerInvariant();
                        var val = parts[1].Trim();
                        if (key == "fmt" && int.TryParse(val, out int f)) decimals = f;
                        else if (key == "row" && val == "1") showRowNumbers = true;
                        else if (key == "border" && val == "0") showBorders = false;
                        else if (key == "zebra" && val == "0") zebraRows = false;
                    }
                }
            }

            // Parse headers
            var headers = new List<string>();
            if (!string.IsNullOrEmpty(headerSection))
            {
                foreach (var h in SplitTopLevel(headerSection, ';'))
                {
                    headers.Add(h.Trim().Trim('"').Trim('\''));
                }
            }

            // Parse and evaluate data expressions
            var dataExprs = SplitTopLevel(dataSection, ';');
            var columns = new List<double[]>();

            foreach (var expr in dataExprs)
            {
                var trimmed = expr.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                Parser.Parse(trimmed);
                // CalculateReal evaluates the expression (sets ResultValue)
                // It throws if result is not scalar, but we still get ResultValue
                try { Parser.CalculateReal(); } catch { /* vector/matrix - ResultValue still set */ }
                var result = Parser.ResultValue;

                if (result is Matrix mat)
                {
                    // Matrix: each column becomes a table column
                    for (int c = 0; c < mat.ColCount; c++)
                    {
                        var arr = new double[mat.RowCount];
                        for (int r = 0; r < mat.RowCount; r++)
                            arr[r] = mat[r, c].D;
                        columns.Add(arr);
                    }
                }
                else if (result is Vector vec)
                {
                    var arr = new double[vec.Length];
                    for (int i = 0; i < vec.Length; i++)
                        arr[i] = vec[i].D;
                    columns.Add(arr);
                }
                else if (result is RealValue rv)
                {
                    columns.Add(new[] { rv.D });
                }
                else if (result is ComplexValue cv)
                {
                    columns.Add(new[] { cv.A });
                }
                else
                {
                    throw new MathParserException($"$Table: unsupported data type for \"{trimmed}\"");
                }
            }

            if (columns.Count == 0)
                throw new MathParserException("$Table has no data");

            // Find max rows
            int maxRows = 0;
            foreach (var col in columns)
                if (col.Length > maxRows) maxRows = col.Length;

            // Build HTML
            string bdr = showBorders ? "border:1px solid #999;" : "";
            string bdrLight = showBorders ? "border:1px solid #ccc;" : "";

            var sb = new StringBuilder();
            sb.AppendLine($"<table style=\"border-collapse:collapse; margin:8px 0; font-size:0.9em; {bdr}\">");

            // Header row
            if (headers.Count > 0 || showRowNumbers)
            {
                sb.Append("<tr>");
                string thStyle = $"style=\"{bdr} padding:4px 10px; background:#f0f0f0; font-weight:bold; text-align:center;\"";
                if (showRowNumbers) sb.Append($"<th {thStyle}>#</th>");
                for (int c = 0; c < columns.Count; c++)
                {
                    var hdr = c < headers.Count ? headers[c] : $"Col {c + 1}";
                    sb.Append($"<th {thStyle}>{hdr}</th>");
                }
                sb.AppendLine("</tr>");
            }

            // Data rows
            string fmt = $"F{decimals}";
            for (int r = 0; r < maxRows; r++)
            {
                var bgColor = (zebraRows && r % 2 == 1) ? " background:#fafafa;" : "";
                sb.Append("<tr>");
                if (showRowNumbers) sb.Append($"<td style=\"{bdrLight} padding:3px 10px; text-align:center; color:#888;{bgColor}\">{r + 1}</td>");
                for (int c = 0; c < columns.Count; c++)
                {
                    if (r < columns[c].Length)
                        sb.Append($"<td style=\"{bdrLight} padding:3px 10px; text-align:right;{bgColor}\">{columns[c][r].ToString(fmt)}</td>");
                    else
                        sb.Append($"<td style=\"{bdrLight} padding:3px 10px;{bgColor}\"></td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");
            return sb.ToString();
        }

        private static int FindTopLevelChar(string s, char target)
        {
            int depth = 0;
            bool inQuote = false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' || c == '\'') inQuote = !inQuote;
                if (inQuote) continue;
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == target && depth == 0) return i;
            }
            return -1;
        }

        private static List<string> SplitTopLevel(string s, char delimiter)
        {
            var result = new List<string>();
            int depth = 0;
            bool inQuote = false;
            int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' || c == '\'') inQuote = !inQuote;
                if (inQuote) continue;
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == delimiter && depth == 0)
                {
                    result.Add(s[start..i]);
                    start = i + 1;
                }
            }
            if (start < s.Length)
                result.Add(s[start..]);
            return result;
        }
    }
}
