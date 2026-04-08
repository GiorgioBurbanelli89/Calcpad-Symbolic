using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace Calcpad.Core
{
    /// <summary>
    /// $PlotMap — FEM color map supporting arbitrary geometry (triangles/quads).
    ///
    /// Two modes:
    /// 1. Function mode (like $Map but multi-region):
    ///    $PlotMap{f1(x;y) @ x=a:b & y=c:d | f2(x;y) @ x=a2:b2 & y=c2:d2}
    ///
    /// 2. Mesh mode (arbitrary geometry — like Awatif):
    ///    $PlotMap{xj; yj; values}
    ///    Where xj, yj = node coords, values = scalar field per node.
    ///    Colors interpolated per element using Delaunay-like nearest neighbor.
    ///
    /// Uses SAME color palette as $Map (Rainbow + shadows).
    /// </summary>
    internal class PlotMapParser : PlotParser
    {
        private const int NBands = 12;
        private const double D1 = 3d / 5d;
        private const double D2 = 5d / 3d;

        internal PlotMapParser(MathParser parser, PlotSettings settings) : base(parser, settings) { }

        internal override string Parse(ReadOnlySpan<char> script, bool calculate)
        {
            int braceStart = script.IndexOf('{');
            int braceEnd = script.LastIndexOf('}');
            if (braceStart < 0 || braceEnd < 0 || braceEnd <= braceStart)
                throw new MathParserException("$PlotMap syntax error");

            var content = script[(braceStart + 1)..braceEnd].Trim();
            if (!calculate)
                return $"<span class=\"eq\"><span class=\"cond\">$PlotMap</span>{{{content.ToString()}}}</span>";

            var contentStr = content.ToString();

            // Check mode: if contains '@' it's function mode, otherwise mesh mode
            if (contentStr.Contains('@'))
                return ParseFunctionMode(contentStr);
            else
                return ParseMeshMode(contentStr);
        }

        // ===================== MESH MODE (arbitrary geometry) =====================
        private string ParseMeshMode(string contentStr)
        {
            var exprs = SplitTopLevel(contentStr, ';');
            if (exprs.Count < 3)
                throw new MathParserException("$PlotMap mesh mode: $PlotMap{xj; yj; values}");

            // Evaluate vectors
            var xj = EvalVector(exprs[0].Trim());
            var yj = EvalVector(exprs[1].Trim());
            var values = EvalVector(exprs[2].Trim());
            int nj = xj.Length;
            if (yj.Length != nj || values.Length != nj)
                throw new MathParserException("$PlotMap: all vectors must have same length");

            // Optional: connectivity matrix (4th argument)
            int[,] elements = null;
            int ne = 0;
            int nodesPerElem = 4; // default quad
            if (exprs.Count >= 4)
            {
                var eMat = EvalMatrix(exprs[3].Trim());
                if (eMat is not null)
                {
                    ne = eMat.RowCount;
                    nodesPerElem = eMat.ColCount;
                    elements = new int[ne, nodesPerElem];
                    for (int e = 0; e < ne; e++)
                        for (int n = 0; n < nodesPerElem; n++)
                            elements[e, n] = (int)eMat[e, n].D - 1; // 1-based to 0-based
                }
            }

            // Bounds
            double xmin = double.MaxValue, xmax = double.MinValue;
            double ymin = double.MaxValue, ymax = double.MinValue;
            double vmin = double.MaxValue, vmax = double.MinValue;
            for (int i = 0; i < nj; i++)
            {
                if (xj[i] < xmin) xmin = xj[i]; if (xj[i] > xmax) xmax = xj[i];
                if (yj[i] < ymin) ymin = yj[i]; if (yj[i] > ymax) ymax = yj[i];
                if (!double.IsNaN(values[i]) && Math.Abs(values[i]) < 1e10)
                {
                    if (values[i] < vmin) vmin = values[i];
                    if (values[i] > vmax) vmax = values[i];
                }
            }

            double dx = xmax - xmin, dy = ymax - ymin;
            if (dx <= 0 || dy <= 0) throw new MathParserException("$PlotMap: invalid range");

            // Image size
            int imgWidth = (int)Parser.PlotWidth;
            if (imgWidth <= 0) imgWidth = 500;
            int margin = 50, legendWidth = 70;
            int plotWidth = imgWidth - 2 * margin - legendWidth;
            int plotHeight = (int)(plotWidth * dy / dx);
            if (plotHeight < 80) plotHeight = 80;
            int imgHeight = plotHeight + 2 * margin;
            double sx = plotWidth / dx, sy = plotHeight / dy;

            using var bitmap = new SKBitmap(imgWidth, imgHeight);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            if (elements is not null && ne > 0)
            {
                // Pixel-by-pixel rendering: for each pixel, find element, interpolate, color
                // Pre-compute element bounding boxes in pixel coords
                var ebb = new (int pxMin, int pyMin, int pxMax, int pyMax, double[] ex, double[] ey, double[] ev)[ne];
                int validElems = 0;
                for (int e = 0; e < ne; e++)
                {
                    double[] ex2 = new double[nodesPerElem];
                    double[] ey2 = new double[nodesPerElem];
                    double[] ev2 = new double[nodesPerElem];
                    bool valid = true;
                    double pxMn = double.MaxValue, pyMn = double.MaxValue;
                    double pxMx = double.MinValue, pyMx = double.MinValue;
                    for (int nn = 0; nn < nodesPerElem; nn++)
                    {
                        int idx = elements[e, nn];
                        if (idx < 0 || idx >= nj) { valid = false; break; }
                        ex2[nn] = xj[idx]; ey2[nn] = yj[idx]; ev2[nn] = values[idx];
                        double px = (xj[idx] - xmin) * sx;
                        double py = (yj[idx] - ymin) * sy;
                        if (px < pxMn) pxMn = px; if (px > pxMx) pxMx = px;
                        if (py < pyMn) pyMn = py; if (py > pyMx) pyMx = py;
                    }
                    if (!valid) continue;
                    ebb[validElems] = ((int)pxMn, (int)pyMn, (int)pxMx + 1, (int)pyMx + 1, ex2, ey2, ev2);
                    validElems++;
                }

                // Rasterize at plot resolution
                int rasterW = plotWidth, rasterH = plotHeight;
                using var pixBmp = new SKBitmap(rasterW, rasterH);
                for (int py = 0; py < rasterH; py++)
                {
                    double physY = ymin + (rasterH - 1 - py) * dy / rasterH;
                    for (int px = 0; px < rasterW; px++)
                    {
                        double physX = xmin + px * dx / rasterW;

                        // Find element containing this point
                        for (int ei = 0; ei < validElems; ei++)
                        {
                            ref var eb = ref ebb[ei];
                            if (px < eb.pxMin - 1 || px > eb.pxMax + 1 || py < (plotHeight - 1 - eb.pyMax - 1) || py > (plotHeight - 1 - eb.pyMin + 1))
                                continue;

                            // Inverse bilinear: find (s,t) such that bilinear(s,t) = (physX, physY)
                            // Use Newton iteration (2 iterations enough for quads)
                            double s = 0.5, tt = 0.5;
                            for (int iter = 0; iter < 3; iter++)
                            {
                                double fx, fy;
                                if (nodesPerElem == 4)
                                {
                                    fx = (1-s)*(1-tt)*eb.ex[0] + s*(1-tt)*eb.ex[1] + s*tt*eb.ex[2] + (1-s)*tt*eb.ex[3] - physX;
                                    fy = (1-s)*(1-tt)*eb.ey[0] + s*(1-tt)*eb.ey[1] + s*tt*eb.ey[2] + (1-s)*tt*eb.ey[3] - physY;
                                    double dxds = -(1-tt)*eb.ex[0] + (1-tt)*eb.ex[1] + tt*eb.ex[2] - tt*eb.ex[3];
                                    double dxdt = -(1-s)*eb.ex[0] - s*eb.ex[1] + s*eb.ex[2] + (1-s)*eb.ex[3];
                                    double dyds = -(1-tt)*eb.ey[0] + (1-tt)*eb.ey[1] + tt*eb.ey[2] - tt*eb.ey[3];
                                    double dydt = -(1-s)*eb.ey[0] - s*eb.ey[1] + s*eb.ey[2] + (1-s)*eb.ey[3];
                                    double det = dxds*dydt - dxdt*dyds;
                                    if (Math.Abs(det) < 1e-12) break;
                                    s -= (fx*dydt - fy*dxdt) / det;
                                    tt -= (fy*dxds - fx*dyds) / det;
                                }
                                else break;
                            }

                            if (s >= -0.01 && s <= 1.01 && tt >= -0.01 && tt <= 1.01)
                            {
                                double val = (1-s)*(1-tt)*eb.ev[0] + s*(1-tt)*eb.ev[1] + s*tt*eb.ev[2] + (1-s)*tt*eb.ev[3];
                                pixBmp.SetPixel(px, py, GetMapColor(val, vmin, vmax));
                                break;
                            }
                        }
                    }
                }
                canvas.DrawBitmap(pixBmp, new SKRect(margin, margin, margin + plotWidth, margin + plotHeight));

                // Draw element edges
                using var edgePaint = new SKPaint { Color = new SKColor(0, 0, 0, 70), StrokeWidth = 0.7f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                for (int e = 0; e < ne; e++)
                {
                    var path = new SKPath();
                    bool valid = true;
                    for (int nn = 0; nn < nodesPerElem; nn++)
                    {
                        int idx = elements[e, nn];
                        if (idx < 0 || idx >= nj) { valid = false; break; }
                        float px2 = margin + (float)((xj[idx] - xmin) * sx);
                        float py2 = margin + plotHeight - (float)((yj[idx] - ymin) * sy);
                        if (nn == 0) path.MoveTo(px2, py2);
                        else path.LineTo(px2, py2);
                    }
                    if (valid) { path.Close(); canvas.DrawPath(path, edgePaint); }
                }
            }
            else
            {
                // No connectivity — use Voronoi-like cells (colored rectangles at each node)
                double cellDx = dx / Math.Sqrt(nj) * 1.2;
                double cellDy = dy / Math.Sqrt(nj) * 1.2;
                float cw = (float)(cellDx * sx);
                float ch = (float)(cellDy * sy);

                for (int i = 0; i < nj; i++)
                {
                    if (double.IsNaN(values[i]) || Math.Abs(values[i]) > 1e10) continue;
                    float px = margin + (float)((xj[i] - xmin) * sx) - cw / 2;
                    float py = margin + plotHeight - (float)((yj[i] - ymin) * sy) - ch / 2;
                    var color = GetMapColor(values[i], vmin, vmax);
                    using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
                    canvas.DrawRect(px, py, cw, ch, paint);
                }
            }

            // Border and axes
            DrawAxesAndLegend(canvas, margin, plotWidth, plotHeight, imgWidth, imgHeight, legendWidth,
                              xmin, xmax, ymin, ymax, vmin, vmax);

            return BitmapToHtml(bitmap, imgWidth, imgHeight);
        }

        // ===================== FUNCTION MODE (multi-region) =====================
        private string ParseFunctionMode(string contentStr)
        {
            var regionStrs = SplitTopLevel(contentStr, '|');
            var regions = new List<FuncRegion>();
            double globalMin = double.MaxValue, globalMax = double.MinValue;

            foreach (var regStr in regionStrs)
            {
                var reg = ParseFuncRegion(regStr.Trim());
                if (reg != null)
                {
                    EvaluateFuncRegion(reg);
                    if (reg.Min < globalMin) globalMin = reg.Min;
                    if (reg.Max > globalMax) globalMax = reg.Max;
                    regions.Add(reg);
                }
            }

            if (regions.Count == 0)
                throw new MathParserException("$PlotMap: no valid regions");

            double gxMin = double.MaxValue, gxMax = double.MinValue;
            double gyMin = double.MaxValue, gyMax = double.MinValue;
            foreach (var r in regions)
            {
                if (r.X0 < gxMin) gxMin = r.X0; if (r.X1 > gxMax) gxMax = r.X1;
                if (r.Y0 < gyMin) gyMin = r.Y0; if (r.Y1 > gyMax) gyMax = r.Y1;
            }
            double gDx = gxMax - gxMin, gDy = gyMax - gyMin;

            int imgWidth = (int)Parser.PlotWidth;
            if (imgWidth <= 0) imgWidth = 500;
            int margin = 50, legendWidth = 70;
            int plotWidth = imgWidth - 2 * margin - legendWidth;
            int plotHeight = (int)(plotWidth * gDy / gDx);
            if (plotHeight < 80) plotHeight = 80;
            int imgHeight = plotHeight + 2 * margin;
            double sx = plotWidth / gDx, sy = plotHeight / gDy;

            using var bitmap = new SKBitmap(imgWidth, imgHeight);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            foreach (var reg in regions)
            {
                int rx0 = (int)((reg.X0 - gxMin) * sx);
                int ry0 = (int)((gyMax - reg.Y1) * sy);
                int rw = (int)((reg.X1 - reg.X0) * sx);
                int rh = (int)((reg.Y1 - reg.Y0) * sy);

                double range = globalMax - globalMin;
                if (range == 0) range = 1;
                double gradScale = 0.5 * reg.Ny / range;

                for (int jj = 0; jj < reg.Ny; jj++)
                {
                    for (int ii = 0; ii < reg.Nx; ii++)
                    {
                        double val = reg.Grid[ii, jj];
                        if (double.IsNaN(val) || double.IsInfinity(val)) continue;

                        double gx = 0, gy = 0;
                        if (ii > 0 && ii < reg.Nx - 1)
                            gx = (reg.Grid[ii + 1, jj] - reg.Grid[ii - 1, jj]) * gradScale;
                        if (jj > 0 && jj < reg.Ny - 1)
                            gy = (reg.Grid[ii, jj + 1] - reg.Grid[ii, jj - 1]) * gradScale;

                        var color = GetMapColorShadow(val, globalMin, globalMax, gx, gy);
                        float px = margin + rx0 + (float)ii / reg.Nx * rw;
                        float py = margin + ry0 + (float)(reg.Ny - 1 - jj) / reg.Ny * rh;
                        float cw = (float)rw / reg.Nx + 1;
                        float ch = (float)rh / reg.Ny + 1;

                        using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = false };
                        canvas.DrawRect(px, py, cw, ch, paint);
                    }
                }

                using var borderPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                canvas.DrawRect(margin + rx0, margin + ry0, rw, rh, borderPaint);
            }

            DrawAxesAndLegend(canvas, margin, plotWidth, plotHeight, imgWidth, imgHeight, legendWidth,
                              gxMin, gxMax, gyMin, gyMax, globalMin, globalMax);

            return BitmapToHtml(bitmap, imgWidth, imgHeight);
        }

        // ===================== SHARED RENDERING =====================

        /// <summary>Same color as $Map: Rainbow with discrete bands</summary>
        private static SKColor GetMapColor(double value, double vmin, double vmax)
        {
            return GetMapColorShadow(value, vmin, vmax, 0, 0);
        }

        /// <summary>Same color as $Map: Rainbow + shadow lighting (Phong)</summary>
        private static SKColor GetMapColorShadow(double value, double vmin, double vmax, double gradX, double gradY)
        {
            if (vmax <= vmin) return SKColors.Gray;
            double normalized = Math.Clamp((value - vmin) / (vmax - vmin), 0, 1);

            // Discretize to NBands (same as $Map SmoothScale=false)
            int band = (int)(normalized * NBands);
            if (band >= NBands) band = NBands - 1;
            double t = (double)band / (NBands - 1);

            // Rainbow (same as MapPlotter.GetRgb)
            double r, g, b;
            double v4 = t * 4d;
            int n = (int)Math.Floor(v4);
            double f = v4 - n;
            switch (n)
            {
                case 0: r = 0; g = Math.Pow(f, D1); b = 1; break;
                case 1: r = 0; g = 1; b = 1 - Math.Pow(f, D2); break;
                case 2: r = Math.Pow(f, D1); g = 1; b = 0; break;
                case 3: r = 1; g = 1 - Math.Pow(f, D2); b = 0; break;
                default: r = 1; g = 0; b = 0; break;
            }

            // Shadow lighting (same as MapPlotter.SetBitmapBits)
            double k = 255d, s = 0d;
            if (gradX != 0 || gradY != 0)
            {
                const double sqr3 = 0.57735026918962576450914878050196;
                double lx = -sqr3, ly = sqr3, lz = sqr3;
                double z = lz + 1d;
                double slen = Math.Sqrt(lx * lx + ly * ly + z * z);
                double specX = lx / slen, specY = ly / slen, specZ = z / slen;

                double length = Math.Sqrt(gradX * gradX + gradY * gradY + 1d);
                double p = (gradX * lx + gradY * ly + lz) / length;
                if (p < 0) p = 0;
                k = 75d + 180d * p;

                double spec = (gradX * specX + gradY * specY + specZ) / length;
                if (Math.Abs(spec) > 0.98)
                    s = Math.Pow(spec, 200d) * 0.7;
            }

            return new SKColor(
                (byte)Math.Min(255, k * r + (255 - k * r) * s),
                (byte)Math.Min(255, k * g + (255 - k * g) * s),
                (byte)Math.Min(255, k * b + (255 - k * b) * s));
        }

        private void DrawAxesAndLegend(SKCanvas canvas, int margin, int plotWidth, int plotHeight,
            int imgWidth, int imgHeight, int legendWidth,
            double xmin, double xmax, double ymin, double ymax, double vmin, double vmax)
        {
            using var axisPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
            using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 10, IsAntialias = true };

            double dx = xmax - xmin, dy = ymax - ymin;
            int nTicks = 6;
            for (int t = 0; t <= nTicks; t++)
            {
                double v = xmin + t * dx / nTicks;
                float px = margin + (float)(t * plotWidth / (double)nTicks);
                canvas.DrawLine(px, margin + plotHeight, px, margin + plotHeight + 4, axisPaint);
                canvas.DrawText(Fmt(v), px - 15, margin + plotHeight + 16, textPaint);
            }
            for (int t = 0; t <= nTicks; t++)
            {
                double v = ymin + t * dy / nTicks;
                float py = margin + plotHeight - (float)(t * plotHeight / (double)nTicks);
                canvas.DrawLine(margin - 4, py, margin, py, axisPaint);
                canvas.DrawText(Fmt(v), 2, py + 4, textPaint);
            }

            // Legend
            int lx = imgWidth - legendWidth + 5, ly = margin, lh = plotHeight, lw = 18;
            float stripH = (float)lh / NBands;
            for (int c = 0; c < NBands; c++)
            {
                double t = 1.0 - (double)c / (NBands - 1);
                double val = vmin + t * (vmax - vmin);
                var color = GetMapColor(val, vmin, vmax);
                using var p = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
                canvas.DrawRect(lx, ly + c * stripH, lw, stripH + 1, p);
            }
            using var legBorder = new SKPaint { Color = SKColors.Black, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            canvas.DrawRect(lx, ly, lw, lh, legBorder);

            using var legText = new SKPaint { Color = SKColors.Black, TextSize = 9, IsAntialias = true };
            for (int c = 0; c <= NBands; c += 2)
            {
                double t = 1.0 - (double)c / NBands;
                double val = vmin + t * (vmax - vmin);
                canvas.DrawText(Fmt(val), lx + lw + 3, ly + c * stripH + 4, legText);
            }
        }

        private static string BitmapToHtml(SKBitmap bitmap, int w, int h)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            var base64 = Convert.ToBase64String(data.ToArray());
            return $"<img class=\"plot\" src=\"data:image/png;base64,{base64}\" alt=\"PlotMap\" style=\"width:{w}px;height:{h}px;\">";
        }

        /// <summary>Continuous Rainbow for vertex interpolation (no banding)</summary>
        private static SKColor RainbowContinuous(double t)
        {
            t = Math.Clamp(t, 0, 1);
            double r, g, b;
            double v4 = t * 4d;
            int n = (int)Math.Floor(v4);
            double f = v4 - n;
            switch (n)
            {
                case 0: r = 0; g = Math.Pow(f, D1); b = 1; break;
                case 1: r = 0; g = 1; b = 1 - Math.Pow(f, D2); break;
                case 2: r = Math.Pow(f, D1); g = 1; b = 0; break;
                case 3: r = 1; g = 1 - Math.Pow(f, D2); b = 0; break;
                default: r = 1; g = 0; b = 0; break;
            }
            return new SKColor((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private static string Fmt(double v)
        {
            if (Math.Abs(v) >= 1000) return v.ToString("F0");
            if (Math.Abs(v) >= 1) return v.ToString("F1");
            return v.ToString("F2");
        }

        // ===================== FUNCTION REGION HELPERS =====================
        private class FuncRegion
        {
            public double X0, X1, Y0, Y1;
            public int Nx, Ny;
            public double[,] Grid;
            public double Min = double.MaxValue, Max = double.MinValue;
            public Func<IValue> CompiledFunc;
            public Parameter ParamX, ParamY;
        }

        private FuncRegion ParseFuncRegion(string s)
        {
            char[] delimiters = { '@', '=', ':', '&', '=', ':', '\0' };
            var parts = new string[7];
            int idx = 0, start = 0, depth = 0;
            bool inQuote = false;
            for (int i = 0; i < s.Length && idx < 7; i++)
            {
                char c = s[i];
                if (c == '"' || c == '\'') inQuote = !inQuote;
                if (inQuote) continue;
                if (c == '(' || c == '[' || c == '{') depth++;
                if (c == ')' || c == ']' || c == '}') depth--;
                if (depth == 0 && c == delimiters[idx]) { parts[idx] = s[start..i].Trim(); idx++; start = i + 1; }
            }
            if (idx < 7) parts[idx] = s[start..].Trim();
            if (parts[0] == null || parts[1] == null) return null;

            var reg = new FuncRegion();
            Parser.Parse(parts[2]); reg.X0 = Parser.CalculateReal();
            Parser.Parse(parts[3]); reg.X1 = Parser.CalculateReal();
            Parser.Parse(parts[5]); reg.Y0 = Parser.CalculateReal();
            Parser.Parse(parts[6]); reg.Y1 = Parser.CalculateReal();

            ReadOnlySpan<Parameter> parameters = [new(parts[1].Trim()), new(parts[4].Trim())];
            reg.CompiledFunc = Parser.Compile(parts[0], parameters);
            reg.ParamX = parameters[0];
            reg.ParamY = parameters[1];
            return reg;
        }

        private void EvaluateFuncRegion(FuncRegion reg)
        {
            int step = (int)Parser.PlotStep;
            if (step <= 0) step = 8;
            int pw = (int)Parser.PlotWidth;
            if (pw <= 0) pw = 500;
            double aspect = (reg.Y1 - reg.Y0) / (reg.X1 - reg.X0);
            reg.Nx = Math.Max(10, pw / step);
            reg.Ny = Math.Max(10, (int)(reg.Nx * aspect));
            double dxs = (reg.X1 - reg.X0) / reg.Nx;
            double dys = (reg.Y1 - reg.Y0) / reg.Ny;
            reg.Grid = new double[reg.Nx, reg.Ny];

            for (int j = 0; j < reg.Ny; j++)
            {
                double y = reg.Y0 + (j + 0.5) * dys;
                reg.ParamY.Variable.SetNumber(y);
                for (int i = 0; i < reg.Nx; i++)
                {
                    double x = reg.X0 + (i + 0.5) * dxs;
                    reg.ParamX.Variable.SetNumber(x);
                    try
                    {
                        var result = reg.CompiledFunc();
                        double val = result is RealValue rv ? rv.D : double.NaN;
                        reg.Grid[i, j] = val;
                        if (!double.IsNaN(val) && !double.IsInfinity(val))
                        {
                            if (val < reg.Min) reg.Min = val;
                            if (val > reg.Max) reg.Max = val;
                        }
                    }
                    catch { reg.Grid[i, j] = double.NaN; }
                }
            }
        }

        // ===================== VECTOR/MATRIX HELPERS =====================
        private double[] EvalVector(string expr)
        {
            Parser.Parse(expr);
            try { Parser.CalculateReal(); } catch { }
            var result = Parser.ResultValue;
            if (result is Vector vec)
            {
                var arr = new double[vec.Length];
                for (int i = 0; i < vec.Length; i++) arr[i] = vec[i].D;
                return arr;
            }
            throw new MathParserException($"$PlotMap: \"{expr}\" must be a vector");
        }

        private Matrix EvalMatrix(string expr)
        {
            Parser.Parse(expr);
            try { Parser.CalculateReal(); } catch { }
            var result = Parser.ResultValue;
            if (result is Matrix mat) return mat;
            return null;
        }

        private static int FindTopLevelChar(string s, char target)
        {
            int depth = 0; bool inQuote = false;
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
            int depth = 0; bool inQuote = false; int start = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"' || c == '\'') inQuote = !inQuote;
                if (inQuote) continue;
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == delimiter && depth == 0) { result.Add(s[start..i]); start = i + 1; }
            }
            if (start < s.Length) result.Add(s[start..]);
            return result;
        }
    }
}
