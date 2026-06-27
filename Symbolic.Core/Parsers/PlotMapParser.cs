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

            // Detect separate groups by X-gap (for per-group color scaling)
            // Sort unique X coords, find gap > 2x avg spacing
            var sortedX = new SortedSet<double>();
            for (int i = 0; i < nj; i++) sortedX.Add(Math.Round(xj[i], 1));
            var xList = new List<double>(sortedX);
            double avgSpacing = (xmax - xmin) / Math.Max(1, xList.Count - 1);
            double splitX = double.MaxValue;
            for (int i = 1; i < xList.Count; i++)
            {
                double gap = xList[i] - xList[i - 1];
                if (gap > avgSpacing * 3)
                {
                    splitX = (xList[i] + xList[i - 1]) / 2;
                    break;
                }
            }

            // Per-group min/max for color scaling
            double vmin1 = double.MaxValue, vmax1 = double.MinValue;
            double vmin2 = double.MaxValue, vmax2 = double.MinValue;
            bool hasGroups = splitX < double.MaxValue;
            if (hasGroups)
            {
                for (int i = 0; i < nj; i++)
                {
                    if (double.IsNaN(values[i]) || Math.Abs(values[i]) > 1e10) continue;
                    if (xj[i] < splitX)
                    {
                        if (values[i] < vmin1) vmin1 = values[i];
                        if (values[i] > vmax1) vmax1 = values[i];
                    }
                    else
                    {
                        if (values[i] < vmin2) vmin2 = values[i];
                        if (values[i] > vmax2) vmax2 = values[i];
                    }
                }
            }

            double dx = xmax - xmin, dy = ymax - ymin;
            if (dx <= 0 || dy <= 0) throw new MathParserException("$PlotMap: invalid range");

            // High-DPI rendering at dpr× the logical size, displayed in CSS at the
            // same visual size as $Map (using $Map's exact formula). $Map treats
            // PlotWidth as the *plot area* (not total image) and grows the image
            // around it; the final CSS width comes out to `0.75 * (PlotWidth + 120)`
            // pt. We replicate that so both plots look visually identical.
            const int dpr = 4;
            int userPlotWidth = (int)Parser.PlotWidth;
            if (userPlotWidth <= 0) userPlotWidth = 500;

            // Bitmap layout (in physical pixels, scaled by dpr).
            int plotArea = userPlotWidth * dpr;                  // pure plot region width
            int margin = 50 * dpr;                                // top/bottom & left side
            int legendWidth = (hasGroups ? 100 : 70) * dpr;       // right side reserved for legend
            int imgWidth = plotArea + 2 * margin + legendWidth;   // total bitmap width
            int plotWidth = plotArea;                              // alias for downstream code
            int plotHeight = (int)(plotWidth * dy / dx);
            if (plotHeight < 80 * dpr) plotHeight = 80 * dpr;
            int imgHeight = plotHeight + 2 * margin;
            // CSS visual size matches $Map: dw = 0.75 * (PlotArea + L + R) where
            // L+R ≈ 120 pt total. For userPlotWidth=300 ⇒ cssWidth ≈ 315pt.
            int cssWidth = (int)(0.75 * (userPlotWidth + 120));
            int cssHeight = (int)(cssWidth * (double)imgHeight / imgWidth);
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
                                // Gradient for shadow lighting
                                double dvds = -(1-tt)*eb.ev[0] + (1-tt)*eb.ev[1] + tt*eb.ev[2] - tt*eb.ev[3];
                                double dvdt = -(1-s)*eb.ev[0] - s*eb.ev[1] + s*eb.ev[2] + (1-s)*eb.ev[3];
                                double dxds = -(1-tt)*eb.ex[0] + (1-tt)*eb.ex[1] + tt*eb.ex[2] - tt*eb.ex[3];
                                double dxdt = -(1-s)*eb.ex[0] - s*eb.ex[1] + s*eb.ex[2] + (1-s)*eb.ex[3];
                                double dyds = -(1-tt)*eb.ey[0] + (1-tt)*eb.ey[1] + tt*eb.ey[2] - tt*eb.ey[3];
                                double dydt = -(1-s)*eb.ey[0] - s*eb.ey[1] + s*eb.ey[2] + (1-s)*eb.ey[3];
                                double detJ2 = dxds*dydt - dxdt*dyds;
                                double gradX = 0, gradY = 0;
                                if (Math.Abs(detJ2) > 1e-12)
                                {
                                    // Physical gradient dv/dx, dv/dy
                                    double dvdx = (dvds*dydt - dvdt*dyds) / detJ2;
                                    double dvdy = (dvdt*dxds - dvds*dxdt) / detJ2;
                                    // Scale to pixel-space: gradient per pixel
                                    gradX = dvdx * dx / rasterW;
                                    gradY = -dvdy * dy / rasterH; // negative: Y flipped
                                }
                                // Use per-group min/max if groups detected
                                double lmin = vmin, lmax = vmax;
                                if (hasGroups)
                                {
                                    if (physX < splitX) { lmin = vmin1; lmax = vmax1; }
                                    else { lmin = vmin2; lmax = vmax2; }
                                }
                                pixBmp.SetPixel(px, py, GetMapColorShadow(val, lmin, lmax, gradX, gradY));
                                break;
                            }
                        }
                    }
                }
                canvas.DrawBitmap(pixBmp, new SKRect(margin, margin, margin + plotWidth, margin + plotHeight));

                // Draw element edges
                using var edgePaint = new SKPaint { Color = new SKColor(0, 0, 0, 70), StrokeWidth = 0.7f * dpr, Style = SKPaintStyle.Stroke, IsAntialias = true };
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
            if (hasGroups)
                DrawAxesAndDualLegend(canvas, margin, plotWidth, plotHeight, imgWidth, imgHeight, legendWidth,
                                      xmin, xmax, ymin, ymax, vmin1, vmax1, vmin2, vmax2, dpr);
            else
                DrawAxesAndLegend(canvas, margin, plotWidth, plotHeight, imgWidth, imgHeight, legendWidth,
                                  xmin, xmax, ymin, ymax, vmin, vmax, dpr);

            return BitmapToHtml(bitmap, cssWidth, cssHeight);
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

            // High-DPI render matching $Map's CSS size (see PlotFromInputs).
            const int dpr = 4;
            int userPlotWidth = (int)Parser.PlotWidth;
            if (userPlotWidth <= 0) userPlotWidth = 500;
            int plotArea = userPlotWidth * dpr;
            int margin = 50 * dpr, legendWidth = 70 * dpr;
            int imgWidth = plotArea + 2 * margin + legendWidth;
            int plotWidth = plotArea;
            int plotHeight = (int)(plotWidth * gDy / gDx);
            if (plotHeight < 80 * dpr) plotHeight = 80 * dpr;
            int imgHeight = plotHeight + 2 * margin;
            int cssWidth = (int)(0.75 * (userPlotWidth + 120));
            int cssHeight = (int)(cssWidth * (double)imgHeight / imgWidth);
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

                // Pixel-by-pixel rasterisation with bilinear interpolation +
                // Phong-style shadow lighting. Replaces the previous chunky
                // "one SKRect per grid cell" approach which produced visible
                // mosaic blocks. We render to a temp bitmap matching the plot
                // area (rw × rh physical px) then composite onto the main canvas.
                using var pixBmp = new SKBitmap(rw, rh);
                int Nx = reg.Nx, Ny = reg.Ny;
                for (int py = 0; py < rh; py++)
                {
                    // Y physical position → grid coordinate (top→bottom flipped)
                    double gyN = ((double)(rh - 1 - py) / (rh - 1)) * (Ny - 1);
                    int gj = (int)gyN;
                    if (gj >= Ny - 1) gj = Ny - 2;
                    double ty = gyN - gj;
                    for (int px = 0; px < rw; px++)
                    {
                        double gxN = ((double)px / (rw - 1)) * (Nx - 1);
                        int gi = (int)gxN;
                        if (gi >= Nx - 1) gi = Nx - 2;
                        double tx = gxN - gi;
                        // 4 grid corners — skip pixel if any is invalid.
                        double v00 = reg.Grid[gi, gj];
                        double v10 = reg.Grid[gi + 1, gj];
                        double v01 = reg.Grid[gi, gj + 1];
                        double v11 = reg.Grid[gi + 1, gj + 1];
                        if (double.IsNaN(v00) || double.IsNaN(v10) || double.IsNaN(v01) || double.IsNaN(v11)) continue;
                        // Bilinear interpolation.
                        double v0 = v00 + (v10 - v00) * tx;
                        double v1 = v01 + (v11 - v01) * tx;
                        double val = v0 + (v1 - v0) * ty;
                        // Local pixel-space gradient for Phong shadow. The scale
                        // factor below is what gives the characteristic "3D ring"
                        // shading of $Map — too small and the Phong term gets lost,
                        // too large and the highlights blow out to white. The value
                        // 8.0 was tuned to match $Map's MapPlotter output for a
                        // typical 60×60 grid spanning ~14 units of range.
                        double dvdx_grid = ((v10 - v00) * (1 - ty) + (v11 - v01) * ty);
                        double dvdy_grid = ((v01 - v00) * (1 - tx) + (v11 - v10) * tx);
                        double range = globalMax - globalMin;
                        double normFactor = range > 0 ? 8.0 / range : 1.0;
                        double gradX = dvdx_grid * normFactor;
                        double gradY = -dvdy_grid * normFactor;   // flipped Y
                        pixBmp.SetPixel(px, py, GetMapColorShadow(val, globalMin, globalMax, gradX, gradY));
                    }
                }
                canvas.DrawBitmap(pixBmp, new SKRect(margin + rx0, margin + ry0,
                                                    margin + rx0 + rw, margin + ry0 + rh));

                // Light grid overlay (10×10 cells) — matches $Map's look.
                using var gridPaint = new SKPaint
                {
                    Color = new SKColor(100, 100, 100, 80),
                    StrokeWidth = 0.5f * dpr,
                    Style = SKPaintStyle.Stroke,
                    IsAntialias = true
                };
                const int gridDivs = 10;
                for (int gi = 1; gi < gridDivs; gi++)
                {
                    float gx = margin + rx0 + (float)gi / gridDivs * rw;
                    float gy = margin + ry0 + (float)gi / gridDivs * rh;
                    canvas.DrawLine(gx, margin + ry0, gx, margin + ry0 + rh, gridPaint);
                    canvas.DrawLine(margin + rx0, gy, margin + rx0 + rw, gy, gridPaint);
                }

                using var borderPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 1.5f * dpr, Style = SKPaintStyle.Stroke, IsAntialias = true };
                canvas.DrawRect(margin + rx0, margin + ry0, rw, rh, borderPaint);
            }

            DrawAxesAndLegend(canvas, margin, plotWidth, plotHeight, imgWidth, imgHeight, legendWidth,
                              gxMin, gxMax, gyMin, gyMax, globalMin, globalMax, dpr);

            return BitmapToHtml(bitmap, cssWidth, cssHeight);
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
            // Discrete bands like $Map's non-SmoothScale default. The band index is
            // the value's level (0..NBands-1) and `t` is normalised band-center for
            // the rainbow mapping. The Phong shadow below modulates lightness
            // continuously inside each band — that produces the characteristic
            // "concentric rings with 3D shading" look of $Map.
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
            double xmin, double xmax, double ymin, double ymax, double vmin, double vmax, int dpr = 1)
        {
            using var axisPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 1 * dpr, Style = SKPaintStyle.Stroke, IsAntialias = true };
            using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 10 * dpr, IsAntialias = true };

            double dx = xmax - xmin, dy = ymax - ymin;
            int nTicks = 6;
            for (int t = 0; t <= nTicks; t++)
            {
                double v = xmin + t * dx / nTicks;
                float px = margin + (float)(t * plotWidth / (double)nTicks);
                canvas.DrawLine(px, margin + plotHeight, px, margin + plotHeight + 4 * dpr, axisPaint);
                canvas.DrawText(Fmt(v), px - 15 * dpr, margin + plotHeight + 16 * dpr, textPaint);
            }
            for (int t = 0; t <= nTicks; t++)
            {
                double v = ymin + t * dy / nTicks;
                float py = margin + plotHeight - (float)(t * plotHeight / (double)nTicks);
                canvas.DrawLine(margin - 4 * dpr, py, margin, py, axisPaint);
                canvas.DrawText(Fmt(v), 2 * dpr, py + 4 * dpr, textPaint);
            }

            // Legend
            int lx = imgWidth - legendWidth + 5 * dpr, ly = margin, lh = plotHeight, lw = 18 * dpr;
            float stripH = (float)lh / NBands;
            for (int c = 0; c < NBands; c++)
            {
                double t = 1.0 - (double)c / (NBands - 1);
                double val = vmin + t * (vmax - vmin);
                var color = GetMapColor(val, vmin, vmax);
                using var p = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
                canvas.DrawRect(lx, ly + c * stripH, lw, stripH + 1, p);
            }
            using var legBorder = new SKPaint { Color = SKColors.Black, StrokeWidth = 1 * dpr, Style = SKPaintStyle.Stroke };
            canvas.DrawRect(lx, ly, lw, lh, legBorder);

            using var legText = new SKPaint { Color = SKColors.Black, TextSize = 9 * dpr, IsAntialias = true };
            for (int c = 0; c <= NBands; c += 2)
            {
                double t = 1.0 - (double)c / NBands;
                double val = vmin + t * (vmax - vmin);
                canvas.DrawText(Fmt(val), lx + lw + 3 * dpr, ly + c * stripH + 4 * dpr, legText);
            }
        }

        private void DrawAxesAndDualLegend(SKCanvas canvas, int margin, int plotWidth, int plotHeight,
            int imgWidth, int imgHeight, int legendWidth,
            double xmin, double xmax, double ymin, double ymax,
            double vmin1, double vmax1, double vmin2, double vmax2, int dpr = 1)
        {
            // Axes
            using var axisPaint = new SKPaint { Color = SKColors.Black, StrokeWidth = 1 * dpr, Style = SKPaintStyle.Stroke, IsAntialias = true };
            using var textPaint = new SKPaint { Color = SKColors.Black, TextSize = 10 * dpr, IsAntialias = true };
            double dx = xmax - xmin, dy = ymax - ymin;
            int nTicks = 6;
            for (int t = 0; t <= nTicks; t++)
            {
                double v = xmin + t * dx / nTicks;
                float px = margin + (float)(t * plotWidth / (double)nTicks);
                canvas.DrawLine(px, margin + plotHeight, px, margin + plotHeight + 4 * dpr, axisPaint);
                canvas.DrawText(Fmt(v), px - 15 * dpr, margin + plotHeight + 16 * dpr, textPaint);
            }
            for (int t = 0; t <= nTicks; t++)
            {
                double v = ymin + t * dy / nTicks;
                float py = margin + plotHeight - (float)(t * plotHeight / (double)nTicks);
                canvas.DrawLine(margin - 4 * dpr, py, margin, py, axisPaint);
                canvas.DrawText(Fmt(v), 2 * dpr, py + 4 * dpr, textPaint);
            }

            // Dual legend SIDE BY SIDE: Legend1 | Legend2
            int lx1 = imgWidth - legendWidth + 2 * dpr;
            int lx2 = lx1 + 35 * dpr;
            int ly = margin, lh = plotHeight, lw = 16 * dpr;
            float stripH = (float)lh / NBands;
            using var labelPaint = new SKPaint { Color = SKColors.DarkBlue, TextSize = 8 * dpr, IsAntialias = true, FakeBoldText = true };
            using var legBorder = new SKPaint { Color = SKColors.Black, StrokeWidth = 1 * dpr, Style = SKPaintStyle.Stroke };
            using var legText = new SKPaint { Color = SKColors.Black, TextSize = 8 * dpr, IsAntialias = true };

            // Legend 1 (left)
            canvas.DrawText("Zap1", lx1, ly - 3 * dpr, labelPaint);
            for (int c = 0; c < NBands; c++)
            {
                double t = 1.0 - (double)c / (NBands - 1);
                double val = vmin1 + t * (vmax1 - vmin1);
                using var p = new SKPaint { Color = GetMapColor(val, vmin1, vmax1), Style = SKPaintStyle.Fill };
                canvas.DrawRect(lx1, ly + c * stripH, lw, stripH + 1, p);
            }
            canvas.DrawRect(lx1, ly, lw, lh, legBorder);
            canvas.DrawText(Fmt(vmax1), lx1 - 2 * dpr, ly - 12 * dpr, legText);
            canvas.DrawText(Fmt(vmin1), lx1 - 2 * dpr, ly + lh + 10 * dpr, legText);

            // Legend 2 (right)
            canvas.DrawText("Zap2", lx2, ly - 3 * dpr, labelPaint);
            for (int c = 0; c < NBands; c++)
            {
                double t = 1.0 - (double)c / (NBands - 1);
                double val = vmin2 + t * (vmax2 - vmin2);
                using var p = new SKPaint { Color = GetMapColor(val, vmin2, vmax2), Style = SKPaintStyle.Fill };
                canvas.DrawRect(lx2, ly + c * stripH, lw, stripH + 1, p);
            }
            canvas.DrawRect(lx2, ly, lw, lh, legBorder);
            canvas.DrawText(Fmt(vmax2), lx2 + lw + 2 * dpr, ly + 7 * dpr, legText);
            canvas.DrawText(Fmt(vmin2), lx2 + lw + 2 * dpr, ly + lh, legText);
        }

        private static string BitmapToHtml(SKBitmap bitmap, int w, int h)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            var base64 = Convert.ToBase64String(data.ToArray());
            // Emit only `width` in pt (height auto from PNG aspect ratio) — matches
            // exactly how $Map / MapPlotter emits its image. Setting both width and
            // height forces an explicit aspect that may not match the PNG, and the
            // image ends up visually narrower than $Map for the same logical
            // PlotWidth. With only width, the browser scales the PNG keeping its
            // intrinsic aspect, identical to the $Map reference.
            return $"<img class=\"plot\" src=\"data:image/png;base64,{base64}\" alt=\"PlotMap\" style=\"width:{w}pt;\">";
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
            public Unit XUnits, YUnits;     // units of the range; applied to ParamX/Y when evaluating
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
            // Parse range bounds; capture units so we can re-apply them when
            // evaluating the function at each grid point. Matches $Map's
            // lenient policy: if one side is 0 (unitless) and the other has
            // units, adopt the unit'd side for the whole range.
            Parser.Parse(parts[2]); reg.X0 = Parser.CalculateReal(); var ux0 = Parser.Units;
            Parser.Parse(parts[3]); reg.X1 = Parser.CalculateReal(); var ux1 = Parser.Units;
            Parser.Parse(parts[5]); reg.Y0 = Parser.CalculateReal(); var uy0 = Parser.Units;
            Parser.Parse(parts[6]); reg.Y1 = Parser.CalculateReal(); var uy1 = Parser.Units;

            // X axis: adopt non-null side; reconcile units.
            if (ux0 is null && ux1 is not null) reg.XUnits = ux1;
            else if (ux0 is not null && ux1 is null) reg.XUnits = ux0;
            else if (ux0 is not null && ux1 is not null && ux0.IsConsistent(ux1))
            {
                reg.XUnits = ux0;
                reg.X1 *= ux1.ConvertTo(ux0);
            }
            // Y axis
            if (uy0 is null && uy1 is not null) reg.YUnits = uy1;
            else if (uy0 is not null && uy1 is null) reg.YUnits = uy0;
            else if (uy0 is not null && uy1 is not null && uy0.IsConsistent(uy1))
            {
                reg.YUnits = uy0;
                reg.Y1 *= uy1.ConvertTo(uy0);
            }

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
                // Assign value with the captured range units so the function under
                // $PlotMap sees x/y with their intended physical units.
                if (reg.YUnits is not null)
                    reg.ParamY.Variable.Assign(new RealValue(y, reg.YUnits));
                else
                    reg.ParamY.Variable.SetNumber(y);
                for (int i = 0; i < reg.Nx; i++)
                {
                    double x = reg.X0 + (i + 0.5) * dxs;
                    if (reg.XUnits is not null)
                        reg.ParamX.Variable.Assign(new RealValue(x, reg.XUnits));
                    else
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
                    catch (Exception ex)
                    {
                        reg.Grid[i, j] = double.NaN;
                        if (i == 0 && j == 0)
                            System.Diagnostics.Debug.WriteLine($"PlotMap eval error at ({x},{y}): {ex.Message}");
                    }
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
