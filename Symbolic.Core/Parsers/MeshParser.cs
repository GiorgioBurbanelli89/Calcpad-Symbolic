using System;
using System.Text;

namespace Calcpad.Core
{
    /// <summary>
    /// $Mesh{x_j; y_j; e_j; s_j; f_j @ w = 400}
    /// Generates SVG visualization of a FEM mesh.
    ///   x_j, y_j = joint coordinate vectors (already defined variables)
    ///   e_j = element connectivity matrix (n_e × 3 or 4)
    ///   s_j = supported joints vector (optional)
    ///   f_j = loaded joints vector (optional, negative = lateral)
    ///   w = SVG width in points (default 400)
    ///
    /// $MeshResults{x_j; y_j; e_j; values @ w = 400}
    ///   values = result vector for color map
    /// </summary>
    internal class MeshParser : PlotParser
    {
        internal MeshParser(MathParser parser, PlotSettings settings) : base(parser, settings) { }

        internal override string Parse(ReadOnlySpan<char> script, bool calculate)
        {
            var isResults = script.StartsWith("$MeshR", StringComparison.OrdinalIgnoreCase);

            int braceStart = script.IndexOf('{');
            int braceEnd = script.LastIndexOf('}');
            if (braceStart < 0 || braceEnd < 0 || braceEnd <= braceStart)
                return "<span class=\"err\">$Mesh: missing braces {}</span>";

            var content = script.Slice(braceStart + 1, braceEnd - braceStart - 1);

            // Split by @
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

            var args = paramsPart.Split(';');

            if (!calculate)
            {
                return $"<span class=\"eq\"><span class=\"cond\">{(isResults ? "$MeshResults" : "$Mesh")}</span>{{{paramsPart}}}</span>";
            }

            try
            {
                if (args.Length < 3)
                    return "<span class=\"err\">$Mesh requires at least 3 arguments: x_j; y_j; e_j</span>";

                // Get variables by name from parser scope
                var xVal = GetVarValue(args[0].Trim());
                var yVal = GetVarValue(args[1].Trim());
                var eVal = GetVarValue(args[2].Trim());

                double[] xj = ExtractDoubleArray(xVal);
                double[] yj = ExtractDoubleArray(yVal);
                if (xj == null || yj == null || xj.Length != yj.Length)
                    return "<span class=\"err\">$Mesh: x_j and y_j must be vectors of same length</span>";

                int[,] ej = ExtractIntMatrix(eVal);
                if (ej == null)
                    return "<span class=\"err\">$Mesh: e_j must be a matrix</span>";

                int[] sj = null;
                if (args.Length > 3)
                {
                    var sVal = GetVarValue(args[3].Trim());
                    sj = ExtractIntArray(sVal);
                }

                int[] fj = null;
                double[] fdir = null; // force direction: >0 = down, <0 = lateral
                if (args.Length > 4 && !isResults)
                {
                    var fVal = GetVarValue(args[4].Trim());
                    var fd = ExtractDoubleArray(fVal);
                    if (fd != null)
                    {
                        fj = new int[fd.Length];
                        fdir = new double[fd.Length];
                        for (int i = 0; i < fd.Length; i++)
                        {
                            fj[i] = Math.Abs((int)fd[i]);
                            fdir[i] = fd[i] < 0 ? -1 : 1; // negative = lateral
                        }
                    }
                }

                double[] values = null;
                if (isResults && args.Length > 3)
                {
                    var vVal = GetVarValue(args[3].Trim());
                    values = ExtractDoubleArray(vVal);
                }

                double svgWidth = 400;
                if (!string.IsNullOrEmpty(optionsPart))
                {
                    var optParts = optionsPart.Split('=');
                    if (optParts.Length == 2)
                        double.TryParse(optParts[1].Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out svgWidth);
                }

                return GenerateSvg(xj, yj, ej, sj, fj, fdir, values, svgWidth, isResults);
            }
            catch (Exception ex)
            {
                return $"<span class=\"err\">$Mesh error: {ex.Message}</span>";
            }
        }

        private IValue GetVarValue(string name)
        {
            // Try as variable name first
            var v = Parser.GetVariableRef(name);
            if (v != null) return v.Value;
            // Try evaluating as expression
            Parser.Parse(name);
            Parser.CalculateReal();
            return null;
        }

        private string GenerateSvg(double[] xj, double[] yj, int[,] ej,
            int[] sj, int[] fj, double[] fdir, double[] values,
            double svgWidth, bool isResults)
        {
            int nj = xj.Length;
            int ne = ej.GetLength(0);
            int npn = ej.GetLength(1);

            double xmin = double.MaxValue, xmax = double.MinValue;
            double ymin = double.MaxValue, ymax = double.MinValue;
            for (int i = 0; i < nj; i++)
            {
                if (xj[i] < xmin) xmin = xj[i];
                if (xj[i] > xmax) xmax = xj[i];
                if (yj[i] < ymin) ymin = yj[i];
                if (yj[i] > ymax) ymax = yj[i];
            }

            double dx = xmax - xmin;
            double dy = ymax - ymin;
            if (dx < 1e-10) dx = 1;
            if (dy < 1e-10) dy = 1;

            double pad = 25;
            double scale = (svgWidth - 2 * pad) / dx;
            double svgHeight = dy * scale + 2 * pad;
            double r = Math.Max(2, scale * dx * 0.006);

            double Tx(double x) => (x - xmin) * scale + pad;
            double Ty(double y) => svgHeight - pad - (y - ymin) * scale;

            var sb = new StringBuilder(4096);
            sb.AppendLine($"<svg viewBox=\"0 0 {F(svgWidth)} {F(svgHeight)}\" xmlns=\"http://www.w3.org/2000/svg\" style=\"font-family:Segoe UI;font-size:9px;width:{F(svgWidth)}pt;height:{F(svgHeight)}pt\">");
            sb.AppendLine("<style>.el{stroke:#2d8a4e;stroke-width:1;fill:#90ee90;fill-opacity:0.15}.jt{fill:orangeRed}.sp{stroke:brown;stroke-width:1.5;fill:lightpink}.ld{stroke:blue;stroke-width:2}.lb{fill:#555;text-anchor:middle;font-size:8px}.nl{fill:#888;text-anchor:start;font-size:7px}</style>");

            // Elements
            if (isResults && values != null)
            {
                double vmin = double.MaxValue, vmax = double.MinValue;
                foreach (var v in values) { if (v < vmin) vmin = v; if (v > vmax) vmax = v; }
                double vrange = Math.Abs(vmax - vmin);
                if (vrange < 1e-15) vrange = 1;

                for (int e = 0; e < ne; e++)
                {
                    double avg = 0;
                    int cnt = 0;
                    for (int k = 0; k < npn; k++)
                    {
                        int j = ej[e, k] - 1;
                        if (j >= 0 && j < values.Length) { avg += values[j]; cnt++; }
                    }
                    if (cnt > 0) avg /= cnt;
                    string color = ValColor(Math.Clamp((avg - vmin) / vrange, 0, 1));
                    sb.Append($"<polygon points=\"{ElemPoints(ej, e, npn, nj, xj, yj, Tx, Ty)}\" fill=\"{color}\" fill-opacity=\"0.7\" stroke=\"#666\" stroke-width=\"0.5\"/>");
                }
                // Legend
                double lx = svgWidth - 25, ly = pad, lh = svgHeight - 2 * pad;
                for (int i = 0; i < 20; i++)
                {
                    string c = ValColor(1.0 - i / 19.0);
                    sb.Append($"<rect x=\"{F(lx)}\" y=\"{F(ly + i * lh / 20)}\" width=\"12\" height=\"{F(lh / 20 + 1)}\" fill=\"{c}\"/>");
                }
                sb.Append($"<text x=\"{F(lx - 2)}\" y=\"{F(ly + 4)}\" style=\"font-size:7px;text-anchor:end\">{vmax:G3}</text>");
                sb.Append($"<text x=\"{F(lx - 2)}\" y=\"{F(ly + lh)}\" style=\"font-size:7px;text-anchor:end\">{vmin:G3}</text>");
            }
            else
            {
                for (int e = 0; e < ne; e++)
                {
                    sb.Append($"<polygon points=\"{ElemPoints(ej, e, npn, nj, xj, yj, Tx, Ty)}\" class=\"el\"/>");
                    double cx = 0, cy = 0;
                    int cnt = 0;
                    for (int k = 0; k < npn; k++)
                    {
                        int j = ej[e, k] - 1;
                        if (j >= 0 && j < nj) { cx += Tx(xj[j]); cy += Ty(yj[j]); cnt++; }
                    }
                    if (cnt > 0) { cx /= cnt; cy /= cnt; }
                    sb.Append($"<text x=\"{F(cx)}\" y=\"{F(cy)}\" class=\"lb\">{e + 1}</text>");
                }
            }

            // Supports
            if (sj != null)
            {
                foreach (int s in sj)
                {
                    if (s < 1 || s > nj) continue;
                    double sx = Tx(xj[s - 1]), sy = Ty(yj[s - 1]);
                    double sz = r * 1.8;
                    // Pin triangle
                    sb.Append($"<polygon points=\"{F(sx)},{F(sy)} {F(sx - sz)},{F(sy + 1.5 * sz)} {F(sx + sz)},{F(sy + 1.5 * sz)}\" class=\"sp\"/>");
                    sb.Append($"<line x1=\"{F(sx - 1.3 * sz)}\" y1=\"{F(sy + 1.5 * sz)}\" x2=\"{F(sx + 1.3 * sz)}\" y2=\"{F(sy + 1.5 * sz)}\" stroke=\"brown\" stroke-width=\"1.5\"/>");
                }
            }

            // Loads (arrows): positive = down, negative node index = lateral (right)
            if (fj != null && fdir != null)
            {
                for (int i = 0; i < fj.Length; i++)
                {
                    int f = fj[i];
                    if (f < 1 || f > nj) continue;
                    double fx = Tx(xj[f - 1]), fy = Ty(yj[f - 1]);
                    double len = r * 5;
                    if (fdir[i] > 0)
                    {
                        // Downward arrow
                        sb.Append($"<line x1=\"{F(fx)}\" y1=\"{F(fy - len)}\" x2=\"{F(fx)}\" y2=\"{F(fy)}\" class=\"ld\"/>");
                        sb.Append($"<polygon points=\"{F(fx)},{F(fy)} {F(fx - 3)},{F(fy - 7)} {F(fx + 3)},{F(fy - 7)}\" fill=\"blue\"/>");
                    }
                    else
                    {
                        // Lateral arrow (right)
                        sb.Append($"<line x1=\"{F(fx - len)}\" y1=\"{F(fy)}\" x2=\"{F(fx)}\" y2=\"{F(fy)}\" class=\"ld\"/>");
                        sb.Append($"<polygon points=\"{F(fx)},{F(fy)} {F(fx - 7)},{F(fy - 3)} {F(fx - 7)},{F(fy + 3)}\" fill=\"blue\"/>");
                    }
                }
            }

            // Joints
            for (int i = 0; i < nj; i++)
            {
                double jx = Tx(xj[i]), jy = Ty(yj[i]);
                sb.Append($"<circle cx=\"{F(jx)}\" cy=\"{F(jy)}\" r=\"{F(r)}\" class=\"jt\"/>");
                sb.Append($"<text x=\"{F(jx + 1.5 * r)}\" y=\"{F(jy - r)}\" class=\"nl\">{i + 1}</text>");
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static string F(double v) => v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        private static string ElemPoints(int[,] ej, int e, int npn, int nj,
            double[] xj, double[] yj, Func<double, double> Tx, Func<double, double> Ty)
        {
            var sb = new StringBuilder();
            for (int k = 0; k < npn; k++)
            {
                int j = ej[e, k] - 1;
                if (j >= 0 && j < nj)
                {
                    if (k > 0) sb.Append(' ');
                    sb.Append($"{F(Tx(xj[j]))},{F(Ty(yj[j]))}");
                }
            }
            return sb.ToString();
        }

        private static string ValColor(double t)
        {
            t = Math.Clamp(t, 0, 1);
            int r, g, b;
            if (t < 0.25) { r = 0; g = (int)(t * 4 * 255); b = 255; }
            else if (t < 0.5) { r = 0; g = 255; b = (int)((0.5 - t) * 4 * 255); }
            else if (t < 0.75) { r = (int)((t - 0.5) * 4 * 255); g = 255; b = 0; }
            else { r = 255; g = (int)((1 - t) * 4 * 255); b = 0; }
            return $"rgb({r},{g},{b})";
        }

        private double[] ExtractDoubleArray(IValue val)
        {
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

        private int[] ExtractIntArray(IValue val)
        {
            var d = ExtractDoubleArray(val);
            if (d == null) return null;
            var a = new int[d.Length];
            for (int i = 0; i < d.Length; i++) a[i] = (int)Math.Round(d[i]);
            return a;
        }

        private int[,] ExtractIntMatrix(IValue val)
        {
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
    }
}
