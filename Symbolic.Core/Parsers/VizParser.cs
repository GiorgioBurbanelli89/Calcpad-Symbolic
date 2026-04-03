using System;
using System.Globalization;
using System.Text;

namespace Calcpad.Core
{
    /// <summary>
    /// Parser for interactive visualization commands using calcpad-viz.js bundle.
    ///
    /// $Fem2D{x_j; y_j; e_j; s_j; values @ w=600 &amp; h=400 &amp; palette=jet &amp; scale=200 &amp; title=My Plot}
    /// $Fem3D{x_j; y_j; z_j; e_j; values @ w=600 &amp; h=400 &amp; palette=jet &amp; scale=1}
    /// $Chart{x1; y1; x2; y2 @ type=line &amp; title=My Chart &amp; xlabel=X &amp; ylabel=Y}
    ///
    /// All commands extract variables from CalcpadCE parser scope
    /// and generate HTML + JS that calls the calcpad-viz.js bundle.
    /// </summary>
    internal class VizParser : PlotParser
    {
        private static int _vizCounter = 0; // unique ID per visualization

        internal VizParser(MathParser parser, PlotSettings settings) : base(parser, settings) { }

        internal override string Parse(ReadOnlySpan<char> script, bool calculate)
        {
            // Detect command type: $Fem2D, $Fem3D, or $Chart
            string cmdType;
            if (script.StartsWith("$fem3d", StringComparison.OrdinalIgnoreCase))
                cmdType = "fem3d";
            else if (script.StartsWith("$fem2d", StringComparison.OrdinalIgnoreCase))
                cmdType = "fem2d";
            else if (script.StartsWith("$chart", StringComparison.OrdinalIgnoreCase))
                cmdType = "chart";
            else
                return "<span class=\"err\">Unknown viz command</span>";

            // Extract content between braces
            int braceStart = script.IndexOf('{');
            int braceEnd = script.LastIndexOf('}');
            if (braceStart < 0 || braceEnd < 0 || braceEnd <= braceStart)
                return $"<span class=\"err\">${cmdType}: missing braces {{}}</span>";

            var content = script.Slice(braceStart + 1, braceEnd - braceStart - 1);

            // Split by @ into params and options
            int atIdx = content.IndexOf('@');
            string paramsPart;
            string optionsPart = "";
            if (atIdx >= 0)
            {
                paramsPart = content.Slice(0, atIdx).ToString().Trim();
                optionsPart = content.Slice(atIdx + 1).ToString().Trim();
            }
            else
            {
                paramsPart = content.ToString().Trim();
            }

            // If not calculating, show equation preview
            if (!calculate)
                return $"<span class=\"eq\"><span class=\"cond\">${cmdType}</span>{{{paramsPart}}}</span>";

            try
            {
                // Parse options (key=value pairs separated by &)
                var options = ParseOptions(optionsPart);

                // Dispatch to specific handler
                return cmdType switch
                {
                    "fem2d" => GenerateFem2D(paramsPart, options),
                    "fem3d" => GenerateFem3D(paramsPart, options),
                    "chart" => GenerateChart(paramsPart, options),
                    _ => "<span class=\"err\">Unknown viz command</span>"
                };
            }
            catch (Exception ex)
            {
                return $"<span class=\"err\">${cmdType} error: {ex.Message}</span>";
            }
        }

        // ============================================================
        // $Fem2D{x_j; y_j; e_j; s_j; values @ options}
        // ============================================================
        private string GenerateFem2D(string paramsPart, VizOptions opts)
        {
            var args = paramsPart.Split(';');
            if (args.Length < 3)
                return "<span class=\"err\">$Fem2D requires at least 3 args: x_j; y_j; e_j</span>";

            // Extract vectors/matrices from parser
            double[] xj = GetDoubleArray(args[0].Trim());
            double[] yj = GetDoubleArray(args[1].Trim());
            int[,] ej = GetIntMatrix(args[2].Trim());

            if (xj == null || yj == null)
                return "<span class=\"err\">$Fem2D: x_j and y_j must be vectors</span>";
            if (ej == null)
                return "<span class=\"err\">$Fem2D: e_j must be a matrix</span>";

            // 4th arg = values (color data), supports via option "supports=varname"
            double[] values = args.Length > 3 ? GetDoubleArray(args[3].Trim()) : null;
            int[] sj = opts.Has("supports") ? GetIntArray(opts.Get("supports")) : null;

            // Build JSON
            var sb = new StringBuilder(2048);
            var id = $"cviz_{_vizCounter++}";

            sb.Append($"<div id=\"{id}\" style=\"display:inline-block\"></div>");
            sb.Append("<script>");
            sb.Append($"CalcpadViz.fem2d(\"{id}\",{{");

            // nodes: [[x1,y1],[x2,y2],...]
            sb.Append("nodes:[");
            for (int i = 0; i < xj.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"[{F(xj[i])},{F(yj[i])}]");
            }
            sb.Append("],");

            // elements: [[n1,n2,n3,n4],...] (0-indexed)
            sb.Append("elements:[");
            int ne = ej.GetLength(0), npn = ej.GetLength(1);
            for (int e = 0; e < ne; e++)
            {
                if (e > 0) sb.Append(',');
                sb.Append('[');
                for (int k = 0; k < npn; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append(ej[e, k] - 1); // convert 1-indexed to 0-indexed
                }
                sb.Append(']');
            }
            sb.Append("],");

            // supports (0-indexed, filter invalid)
            if (sj != null)
            {
                sb.Append("supports:[");
                bool first = true;
                for (int i = 0; i < sj.Length; i++)
                {
                    if (sj[i] < 1 || sj[i] > xj.Length) continue; // skip invalid
                    if (!first) sb.Append(',');
                    sb.Append(sj[i] - 1);
                    first = false;
                }
                sb.Append("],");
            }

            // values
            if (values != null)
            {
                sb.Append("values:[");
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(F(values[i]));
                }
                sb.Append("],");
            }

            // loads: option "loads=varname" → matrix Nx3 [[node,fx,fy],...]
            if (opts.Has("loads"))
            {
                var loadVal = GetVarValue(opts.Get("loads"));
                if (loadVal is Matrix loadMat && loadMat.ColCount >= 3)
                {
                    sb.Append("loads:[");
                    for (int i = 0; i < loadMat.RowCount; i++)
                    {
                        if (i > 0) sb.Append(',');
                        int node = (int)Math.Round(loadMat[i, 0].D) - 1; // 0-indexed
                        sb.Append($"[{node},{F(loadMat[i, 1].D)},{F(loadMat[i, 2].D)}]");
                    }
                    sb.Append("],");
                }
            }

            // deformed: option "deformed=varname" → vector of [dx,dy] pairs
            // or two separate vectors "defx=varname & defy=varname"
            if (opts.Has("deformed"))
            {
                var defName = opts.Get("deformed");
                var defVal = GetDoubleArray(defName);
                if (defVal != null && defVal.Length == xj.Length * 2)
                {
                    // Interleaved: [u1,v1,u2,v2,...] (DOF vector)
                    sb.Append("deformed:[");
                    for (int i = 0; i < xj.Length; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append($"[{F(defVal[i * 2])},{F(defVal[i * 2 + 1])}]");
                    }
                    sb.Append("],");
                }
            }
            else if (opts.Has("defx") && opts.Has("defy"))
            {
                var defx = GetDoubleArray(opts.Get("defx"));
                var defy = GetDoubleArray(opts.Get("defy"));
                if (defx != null && defy != null && defx.Length == xj.Length)
                {
                    sb.Append("deformed:[");
                    for (int i = 0; i < xj.Length; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append($"[{F(defx[i])},{F(defy[i])}]");
                    }
                    sb.Append("],");
                }
            }

            // options
            sb.Append("options:{");
            sb.Append($"width:{opts.GetInt("w", 600)},");
            sb.Append($"height:{opts.GetInt("h", 400)},");
            if (opts.Has("palette")) sb.Append($"palette:\"{opts.Get("palette")}\",");
            if (opts.Has("title")) sb.Append($"title:\"{EscapeJs(opts.Get("title"))}\",");
            if (opts.Has("labels")) sb.Append("showLabels:true,");
            if (opts.Has("elemnums")) sb.Append("showElements:true,");
            if (opts.Has("scale")) sb.Append($"scale:{opts.Get("scale")},");
            sb.Append('}');

            sb.Append("});</script>");
            return sb.ToString();
        }

        // ============================================================
        // $Fem3D{x_j; y_j; z_j; e_j; values @ options}
        // ============================================================
        private string GenerateFem3D(string paramsPart, VizOptions opts)
        {
            var args = paramsPart.Split(';');
            if (args.Length < 4)
                return "<span class=\"err\">$Fem3D requires at least 4 args: x_j; y_j; z_j; e_j</span>";

            double[] xj = GetDoubleArray(args[0].Trim());
            double[] yj = GetDoubleArray(args[1].Trim());
            double[] zj = GetDoubleArray(args[2].Trim());
            int[,] ej = GetIntMatrix(args[3].Trim());

            if (xj == null || yj == null || zj == null)
                return "<span class=\"err\">$Fem3D: x_j, y_j, z_j must be vectors</span>";
            if (ej == null)
                return "<span class=\"err\">$Fem3D: e_j must be a matrix</span>";

            double[] values = args.Length > 4 ? GetDoubleArray(args[4].Trim()) : null;

            var sb = new StringBuilder(4096);
            var id = $"cviz_{_vizCounter++}";

            sb.Append($"<div id=\"{id}\" style=\"display:inline-block\"></div>");
            sb.Append("<script>");
            sb.Append($"CalcpadViz.fem3d(\"{id}\",{{");

            // nodes: [[x,y,z],...]
            sb.Append("nodes:[");
            for (int i = 0; i < xj.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"[{F(xj[i])},{F(yj[i])},{F(zj[i])}]");
            }
            sb.Append("],");

            // elements (0-indexed)
            sb.Append("elements:[");
            int ne = ej.GetLength(0), npn = ej.GetLength(1);
            for (int e = 0; e < ne; e++)
            {
                if (e > 0) sb.Append(',');
                sb.Append('[');
                for (int k = 0; k < npn; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append(ej[e, k] - 1);
                }
                sb.Append(']');
            }
            sb.Append("],");

            // values
            if (values != null)
            {
                sb.Append("values:[");
                for (int i = 0; i < values.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(F(values[i]));
                }
                sb.Append("],");
            }

            // deformed: option "deformed=varname" → matrix Nx3 [[dx,dy,dz],...]
            if (opts.Has("deformed"))
            {
                var defVal = GetVarValue(opts.Get("deformed"));
                if (defVal is Matrix defMat && defMat.ColCount >= 3)
                {
                    sb.Append("deformed:[");
                    for (int i = 0; i < defMat.RowCount; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append($"[{F(defMat[i, 0].D)},{F(defMat[i, 1].D)},{F(defMat[i, 2].D)}]");
                    }
                    sb.Append("],");
                }
            }

            // options
            sb.Append("options:{");
            sb.Append($"width:{opts.GetInt("w", 600)},");
            sb.Append($"height:{opts.GetInt("h", 400)},");
            if (opts.Has("palette")) sb.Append($"palette:\"{opts.Get("palette")}\",");
            if (opts.Has("title")) sb.Append($"title:\"{EscapeJs(opts.Get("title"))}\",");
            if (opts.Has("scale")) sb.Append($"scale:{opts.Get("scale")},");
            sb.Append('}');

            sb.Append("});</script>");
            return sb.ToString();
        }

        // ============================================================
        // $Chart{x1; y1; x2; y2 @ type=line & title=Title}
        // Pairs of x;y vectors. Each pair is a series.
        // ============================================================
        private string GenerateChart(string paramsPart, VizOptions opts)
        {
            var args = paramsPart.Split(';');
            if (args.Length < 2 || args.Length % 2 != 0)
                return "<span class=\"err\">$Chart requires pairs of x;y vectors</span>";

            int nSeries = args.Length / 2;
            var sb = new StringBuilder(2048);
            var id = $"cviz_{_vizCounter++}";

            sb.Append($"<div id=\"{id}\" style=\"display:inline-block\"></div>");
            sb.Append("<script>");
            sb.Append($"CalcpadViz.chart(\"{id}\",{{");

            // series
            sb.Append("series:[");
            string chartType = opts.Get("type", "line");
            for (int s = 0; s < nSeries; s++)
            {
                double[] xd = GetDoubleArray(args[s * 2].Trim());
                double[] yd = GetDoubleArray(args[s * 2 + 1].Trim());
                if (xd == null || yd == null) continue;

                if (s > 0) sb.Append(',');
                sb.Append("{x:[");
                for (int i = 0; i < xd.Length; i++) { if (i > 0) sb.Append(','); sb.Append(F(xd[i])); }
                sb.Append("],y:[");
                for (int i = 0; i < yd.Length; i++) { if (i > 0) sb.Append(','); sb.Append(F(yd[i])); }
                sb.Append($"],type:\"{chartType}\"");

                // Label from option: label1, label2, etc.
                string labelKey = $"label{s + 1}";
                if (opts.Has(labelKey)) sb.Append($",label:\"{EscapeJs(opts.Get(labelKey))}\"");

                sb.Append('}');
            }
            sb.Append("],");

            // options
            sb.Append("options:{");
            sb.Append($"width:{opts.GetInt("w", 600)},");
            sb.Append($"height:{opts.GetInt("h", 350)},");
            if (opts.Has("title")) sb.Append($"title:\"{EscapeJs(opts.Get("title"))}\",");
            if (opts.Has("xlabel")) sb.Append($"xLabel:\"{EscapeJs(opts.Get("xlabel"))}\",");
            if (opts.Has("ylabel")) sb.Append($"yLabel:\"{EscapeJs(opts.Get("ylabel"))}\",");
            sb.Append('}');

            sb.Append("});</script>");
            return sb.ToString();
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static string F(double v) =>
            v.ToString("G10", CultureInfo.InvariantCulture);

        private static string EscapeJs(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\\'");

        private IValue GetVarValue(string name)
        {
            var v = Parser.GetVariableRef(name);
            if (v != null) return v.Value;
            Parser.Parse(name);
            Parser.CalculateReal();
            return null;
        }

        private double[] GetDoubleArray(string name)
        {
            var val = GetVarValue(name);
            if (val is Vector vec)
            {
                var arr = new double[vec.Length];
                for (int i = 0; i < vec.Length; i++) arr[i] = vec[i].D;
                return arr;
            }
            if (val is Matrix mat && (mat.ColCount == 1 || mat.RowCount == 1))
            {
                int n = Math.Max(mat.RowCount, mat.ColCount);
                var arr = new double[n];
                for (int i = 0; i < n; i++)
                    arr[i] = mat.ColCount == 1 ? mat[i, 0].D : mat[0, i].D;
                return arr;
            }
            return null;
        }

        private int[] GetIntArray(string name)
        {
            var d = GetDoubleArray(name);
            if (d == null) return null;
            var a = new int[d.Length];
            for (int i = 0; i < d.Length; i++) a[i] = (int)Math.Round(d[i]);
            return a;
        }

        private int[,] GetIntMatrix(string name)
        {
            var val = GetVarValue(name);
            if (val is Matrix mat)
            {
                int rows = mat.RowCount, cols = mat.ColCount;
                var m = new int[rows, cols];
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        m[i, j] = (int)Math.Round(mat[i, j].D);
                return m;
            }
            return null;
        }

        // Simple key=value option parser
        private static VizOptions ParseOptions(string optionsPart)
        {
            var opts = new VizOptions();
            if (string.IsNullOrEmpty(optionsPart)) return opts;
            var pairs = optionsPart.Split('&');
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                string key = kv[0].Trim().ToLowerInvariant();
                string val = kv.Length > 1 ? kv[1].Trim() : "true";
                opts.Set(key, val);
            }
            return opts;
        }
    }

    /// <summary>
    /// Simple key-value store for visualization options
    /// </summary>
    internal class VizOptions
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _opts = new();

        internal void Set(string key, string val) => _opts[key] = val;
        internal bool Has(string key) => _opts.ContainsKey(key);
        internal string Get(string key, string def = "") =>
            _opts.TryGetValue(key, out var v) ? v : def;
        internal int GetInt(string key, int def) =>
            _opts.TryGetValue(key, out var v) && int.TryParse(v, out int n) ? n : def;
    }
}
