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
            // Detect command type: $Fem2D, $Fem3D, $Chart, or $Frame
            string cmdType;
            if (script.StartsWith("$fem3d", StringComparison.OrdinalIgnoreCase))
                cmdType = "fem3d";
            else if (script.StartsWith("$fem2d", StringComparison.OrdinalIgnoreCase))
                cmdType = "fem2d";
            else if (script.StartsWith("$chart", StringComparison.OrdinalIgnoreCase))
                cmdType = "chart";
            else if (script.StartsWith("$frame", StringComparison.OrdinalIgnoreCase))
                cmdType = "frame";
            else if (script.StartsWith("$struct", StringComparison.OrdinalIgnoreCase))
                cmdType = "struct";
            else if (script.StartsWith("$draw", StringComparison.OrdinalIgnoreCase))
                cmdType = "draw";
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
                    "frame" => GenerateFrame(paramsPart, options),
                    "struct" => GenerateStruct(paramsPart, options),
                    "draw" => GenerateDraw(paramsPart, options),
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

                // Label from option: label1=, label2=, or labels=A,B,C
                string labelKey = $"label{s + 1}";
                if (opts.Has(labelKey))
                    sb.Append($",label:\"{EscapeJs(opts.Get(labelKey))}\"");
                else if (opts.Has("labels"))
                {
                    var lbls = opts.Get("labels").Split(',');
                    if (s < lbls.Length) sb.Append($",label:\"{EscapeJs(lbls[s].Trim())}\"");
                }

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
        // $Frame{nodes; elements; supports; deformed; moments @ options}
        // nodes = matrix Nx2 or Nx3, elements = matrix Mx2
        // supports = vector, deformed = matrix Nx2/Nx3, moments = matrix Mx2
        // Options: w, h, defScale, diagScale, title, loads, shears, normals
        // ============================================================
        private string GenerateFrame(string paramsPart, VizOptions opts)
        {
            var args = paramsPart.Split(';');
            if (args.Length < 2)
                return "<span class=\"err\">$Frame requires at least 2 args: nodes; elements</span>";

            // nodes: matrix Nx2 or Nx3
            var nodesVal = GetVarValue(args[0].Trim());
            if (nodesVal is not Matrix nodesMat)
                return "<span class=\"err\">$Frame: nodes must be a matrix</span>";

            // elements: matrix Mx2
            int[,] ej = GetIntMatrix(args[1].Trim());
            if (ej == null)
                return "<span class=\"err\">$Frame: elements must be a matrix</span>";

            var sb = new StringBuilder(4096);
            var id = $"cviz_{_vizCounter++}";
            sb.Append($"<div id=\"{id}\" style=\"display:inline-block\"></div>");
            sb.Append("<script>");
            sb.Append($"CalcpadViz.frameViewer(\"{id}\",{{");

            // nodes: [[x,y,z],...]
            sb.Append("nodes:[");
            for (int i = 0; i < nodesMat.RowCount; i++)
            {
                if (i > 0) sb.Append(',');
                double x = nodesMat[i, 0].D;
                double y = nodesMat.ColCount > 1 ? nodesMat[i, 1].D : 0;
                double z = nodesMat.ColCount > 2 ? nodesMat[i, 2].D : 0;
                sb.Append($"[{F(x)},{F(y)},{F(z)}]");
            }
            sb.Append("],");

            // elements: [[n1,n2],...] (convert 1-indexed to 0-indexed)
            int ne = ej.GetLength(0);
            sb.Append("elements:[");
            for (int e = 0; e < ne; e++)
            {
                if (e > 0) sb.Append(',');
                sb.Append($"[{ej[e, 0] - 1},{ej[e, 1] - 1}]");
            }
            sb.Append("],");

            // supports (arg 2): vector of node indices (1-indexed)
            if (args.Length > 2)
            {
                int[] sj = GetIntArray(args[2].Trim());
                if (sj != null)
                {
                    sb.Append("supports:[");
                    for (int i = 0; i < sj.Length; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append(sj[i] - 1);
                    }
                    sb.Append("],");
                }
            }

            // deformed (arg 3): matrix Nx2 or Nx3
            if (args.Length > 3)
            {
                var defVal = GetVarValue(args[3].Trim());
                if (defVal is Matrix defMat)
                {
                    sb.Append("deformed:[");
                    for (int i = 0; i < defMat.RowCount; i++)
                    {
                        if (i > 0) sb.Append(',');
                        double dx = defMat[i, 0].D;
                        double dy = defMat.ColCount > 1 ? defMat[i, 1].D : 0;
                        double dz = defMat.ColCount > 2 ? defMat[i, 2].D : 0;
                        sb.Append($"[{F(dx)},{F(dy)},{F(dz)}]");
                    }
                    sb.Append("],");
                }
            }

            // moments (arg 4): matrix Mx2 [Mi, Mj]
            if (args.Length > 4)
            {
                var momVal = GetVarValue(args[4].Trim());
                if (momVal is Matrix momMat)
                {
                    sb.Append("moments:[");
                    for (int i = 0; i < momMat.RowCount; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append($"[{F(momMat[i, 0].D)},{F(momMat[i, 1].D)}]");
                    }
                    sb.Append("],");
                }
            }

            // shears from option: shears=varname
            if (opts.Has("shears"))
            {
                var shVal = GetVarValue(opts.Get("shears"));
                if (shVal is Matrix shMat)
                {
                    sb.Append("shears:[");
                    for (int i = 0; i < shMat.RowCount; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append($"[{F(shMat[i, 0].D)},{F(shMat[i, 1].D)}]");
                    }
                    sb.Append("],");
                }
            }

            // loads from option: loads=varname (matrix Nx4: [node,Fx,Fy,Fz])
            if (opts.Has("loads"))
            {
                var ldVal = GetVarValue(opts.Get("loads"));
                if (ldVal is Matrix ldMat && ldMat.ColCount >= 3)
                {
                    sb.Append("loads:[");
                    for (int i = 0; i < ldMat.RowCount; i++)
                    {
                        if (i > 0) sb.Append(',');
                        int node = (int)System.Math.Round(ldMat[i, 0].D) - 1;
                        double fx = ldMat[i, 1].D;
                        double fy = ldMat[i, 2].D;
                        double fz = ldMat.ColCount > 3 ? ldMat[i, 3].D : 0;
                        sb.Append($"[{node},{F(fx)},{F(fy)},{F(fz)}]");
                    }
                    sb.Append("],");
                }
            }

            // options
            sb.Append("options:{");
            sb.Append($"width:{opts.GetInt("w", 800)},");
            sb.Append($"height:{opts.GetInt("h", 500)},");
            if (opts.Has("defscale")) sb.Append($"defScale:{opts.Get("defscale")},");
            if (opts.Has("diagscale")) sb.Append($"diagScale:{opts.Get("diagscale")},");
            if (opts.Has("title")) sb.Append($"title:\"{EscapeJs(opts.Get("title"))}\",");
            sb.Append('}');

            sb.Append("});</script>");
            return sb.ToString();
        }

        // ============================================================
        // $Struct{elem1,x1,y1,x2,y2,text : elem2,x,y,text : ... @ title=Title : w=700 : h=300}
        // Element format: type,x1,y1[,x2,y2][,text][,extra]
        // Types: spring, bar, beam, node, support, force, moment, label, dim
        // ============================================================
        private string GenerateStruct(string paramsPart, VizOptions opts)
        {
            var sb = new StringBuilder(2048);
            var id = $"cviz_{_vizCounter++}";
            sb.Append($"<div id=\"{id}\" style=\"display:inline-block\"></div>");
            sb.Append("<script>");
            sb.Append($"CalcpadViz.structDraw(\"{id}\",{{elements:[");

            // Parse elements separated by '|' (pipe) or ':' (colon)
            // Each element params separated by spaces or commas
            var separator = paramsPart.Contains('|') ? '|' : ':';
            var elems = paramsPart.Split(separator);
            bool first = true;
            foreach (var elem in elems)
            {
                // Split by spaces or commas, removing empty entries
                var parts = elem.Trim().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var type = parts[0].Trim().ToLowerInvariant();

                if (!first) sb.Append(',');
                first = false;

                switch (type)
                {
                    case "spring":
                    case "bar":
                    case "beam":
                        // type,x1,y1,x2,y2[,text]
                        if (parts.Length < 5) break;
                        sb.Append($"{{type:\"{type}\",x1:{Eval(parts[1])},y1:{Eval(parts[2])},x2:{Eval(parts[3])},y2:{Eval(parts[4])}");
                        if (parts.Length > 5) sb.Append($",text:\"{EscapeJs(parts[5].Trim())}\"");
                        sb.Append('}');
                        break;

                    case "node":
                        // node,x,y[,text]
                        sb.Append($"{{type:\"node\",x:{Eval(parts[1])},y:{Eval(parts[2])}");
                        if (parts.Length > 3) sb.Append($",text:\"{EscapeJs(parts[3].Trim())}\"");
                        sb.Append('}');
                        break;

                    case "pin":
                    case "fixed":
                    case "roller":
                        // pin,x,y  or fixed,x,y  or roller,x,y
                        sb.Append($"{{type:\"support\",x:{Eval(parts[1])},y:{Eval(parts[2])},supportType:\"{type}\"}}");
                        break;

                    case "force":
                        // force,x,y,dir[,text]
                        if (parts.Length < 4) break;
                        sb.Append($"{{type:\"force\",x:{Eval(parts[1])},y:{Eval(parts[2])},dir:\"{parts[3].Trim()}\"");
                        if (parts.Length > 4) sb.Append($",text:\"{EscapeJs(parts[4].Trim())}\"");
                        sb.Append('}');
                        break;

                    case "moment":
                        // moment,x,y,value[,text]
                        sb.Append($"{{type:\"moment\",x:{Eval(parts[1])},y:{Eval(parts[2])},value:{Eval(parts[3])}");
                        if (parts.Length > 4) sb.Append($",text:\"{EscapeJs(parts[4].Trim())}\"");
                        sb.Append('}');
                        break;

                    case "label":
                        // label,x,y,text
                        if (parts.Length < 4) break;
                        sb.Append($"{{type:\"label\",x:{Eval(parts[1])},y:{Eval(parts[2])},text:\"{EscapeJs(parts[3].Trim())}\"}}");
                        break;

                    case "dim":
                        // dim,x1,y1,x2,y2,text
                        if (parts.Length < 6) break;
                        sb.Append($"{{type:\"dim\",x1:{Eval(parts[1])},y1:{Eval(parts[2])},x2:{Eval(parts[3])},y2:{Eval(parts[4])},text:\"{EscapeJs(parts[5].Trim())}\"}}");
                        break;
                }
            }

            sb.Append("],options:{");
            sb.Append($"width:{opts.GetInt("w", 600)},");
            sb.Append($"height:{opts.GetInt("h", 250)},");
            if (opts.Has("title")) sb.Append($"title:\"{EscapeJs(opts.Get("title"))}\",");
            sb.Append("}});</script>");
            return sb.ToString();
        }

        // ============================================================
        // $Draw{line,x1,y1,x2,y2,color,lw : rect,x1,y1,x2,y2 : circle,x,y,r : ... @ w=600 : h=400 : title=Title : flipY=true : grid=true}
        // General-purpose 2D drawing with pan/zoom interaction.
        // Element types: line, dashed, rect, fillrect, circle, ellipse, arc,
        //   arrow, darrow, polyline, polygon, text, dim, hdim, vdim, hatch
        // ============================================================
        private string GenerateDraw(string paramsPart, VizOptions opts)
        {
            var sb = new StringBuilder(4096);
            var id = $"cviz_{_vizCounter++}";
            sb.Append($"<div id=\"{id}\" style=\"display:inline-block\"></div>");
            sb.Append("<script>");
            sb.Append($"CalcpadViz.draw(\"{id}\",{{elements:[");

            // Parse elements separated by '|' (pipe) or ':' (colon)
            // Each element params separated by spaces or commas
            var separator = paramsPart.Contains('|') ? '|' : ':';
            var elems = paramsPart.Split(separator);
            bool first = true;
            foreach (var elem in elems)
            {
                // Split by spaces or commas, removing empty entries
                var parts = elem.Trim().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var type = parts[0].Trim().ToLowerInvariant();

                if (!first) sb.Append(',');
                first = false;

                switch (type)
                {
                    case "line":
                    case "dashed":
                        // type,x1,y1,x2,y2[,color[,lw[,dashLen]]]
                        if (parts.Length < 5) { first = true; break; }
                        sb.Append($"{{type:\"{type}\",x1:{Eval(parts[1])},y1:{Eval(parts[2])},x2:{Eval(parts[3])},y2:{Eval(parts[4])}");
                        if (parts.Length > 5) sb.Append($",color:\"{parts[5].Trim()}\"");
                        if (parts.Length > 6) sb.Append($",lw:{Eval(parts[6])}");
                        if (type == "dashed" && parts.Length > 7) sb.Append($",dashLen:{Eval(parts[7])}");
                        sb.Append('}');
                        break;

                    case "rect":
                    case "fillrect":
                        // type,x1,y1,x2,y2[,color/fill[,lw]]
                        if (parts.Length < 5) { first = true; break; }
                        sb.Append($"{{type:\"{type}\",x1:{Eval(parts[1])},y1:{Eval(parts[2])},x2:{Eval(parts[3])},y2:{Eval(parts[4])}");
                        if (parts.Length > 5) sb.Append($",{(type == "fillrect" ? "fill" : "color")}:\"{parts[5].Trim()}\"");
                        if (type == "fillrect" && parts.Length > 6) sb.Append($",color:\"{parts[6].Trim()}\"");
                        if (parts.Length > 7) sb.Append($",lw:{Eval(parts[7])}");
                        sb.Append('}');
                        break;

                    case "circle":
                        // circle,x,y,r[,color[,fill[,lw]]]
                        if (parts.Length < 4) { first = true; break; }
                        sb.Append($"{{type:\"circle\",x:{Eval(parts[1])},y:{Eval(parts[2])},r:{Eval(parts[3])}");
                        if (parts.Length > 4) sb.Append($",color:\"{parts[4].Trim()}\"");
                        if (parts.Length > 5) sb.Append($",fill:\"{parts[5].Trim()}\"");
                        if (parts.Length > 6) sb.Append($",lw:{Eval(parts[6])}");
                        sb.Append('}');
                        break;

                    case "ellipse":
                        // ellipse,x,y,rx,ry[,color[,fill[,lw]]]
                        if (parts.Length < 5) { first = true; break; }
                        sb.Append($"{{type:\"ellipse\",x:{Eval(parts[1])},y:{Eval(parts[2])},rx:{Eval(parts[3])},ry:{Eval(parts[4])}");
                        if (parts.Length > 5) sb.Append($",color:\"{parts[5].Trim()}\"");
                        if (parts.Length > 6) sb.Append($",fill:\"{parts[6].Trim()}\"");
                        if (parts.Length > 7) sb.Append($",lw:{Eval(parts[7])}");
                        sb.Append('}');
                        break;

                    case "arc":
                        // arc,x,y,r,startAngle,endAngle[,color[,lw]]
                        if (parts.Length < 6) { first = true; break; }
                        sb.Append($"{{type:\"arc\",x:{Eval(parts[1])},y:{Eval(parts[2])},r:{Eval(parts[3])},startAngle:{Eval(parts[4])},endAngle:{Eval(parts[5])}");
                        if (parts.Length > 6) sb.Append($",color:\"{parts[6].Trim()}\"");
                        if (parts.Length > 7) sb.Append($",lw:{Eval(parts[7])}");
                        sb.Append('}');
                        break;

                    case "arrow":
                    case "darrow":
                        // arrow,x1,y1,x2,y2[,color[,lw[,headSize]]]
                        if (parts.Length < 5) { first = true; break; }
                        sb.Append($"{{type:\"{type}\",x1:{Eval(parts[1])},y1:{Eval(parts[2])},x2:{Eval(parts[3])},y2:{Eval(parts[4])}");
                        if (parts.Length > 5) sb.Append($",color:\"{parts[5].Trim()}\"");
                        if (parts.Length > 6) sb.Append($",lw:{Eval(parts[6])}");
                        if (parts.Length > 7) sb.Append($",headSize:{Eval(parts[7])}");
                        sb.Append('}');
                        break;

                    case "polyline":
                    case "polygon":
                        // polyline,x1,y1,x2,y2,...,xn,yn[,color[,fill[,lw]]]
                        // Detect where coords end: find first non-numeric arg
                        {
                            var coords = new System.Collections.Generic.List<string>();
                            int pi = 1;
                            while (pi < parts.Length)
                            {
                                var trimmed = parts[pi].Trim();
                                if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ||
                                    GetVarValue(trimmed) is RealValue)
                                    coords.Add(Eval(trimmed));
                                else
                                    break;
                                pi++;
                            }
                            sb.Append($"{{type:\"{type}\",points:[{string.Join(",", coords)}]");
                            if (pi < parts.Length) sb.Append($",color:\"{parts[pi].Trim()}\"");
                            if (pi + 1 < parts.Length && type == "polygon") sb.Append($",fill:\"{parts[pi + 1].Trim()}\"");
                            if (pi + 2 < parts.Length) sb.Append($",lw:{Eval(parts[pi + 2])}");
                            sb.Append('}');
                        }
                        break;

                    case "text":
                        // text,x,y,content[,fontSize[,color[,anchor[,fontWeight]]]]
                        if (parts.Length < 4) { first = true; break; }
                        sb.Append($"{{type:\"text\",x:{Eval(parts[1])},y:{Eval(parts[2])},text:\"{EscapeJs(parts[3].Trim())}\"");
                        if (parts.Length > 4) sb.Append($",fontSize:{Eval(parts[4])}");
                        if (parts.Length > 5) sb.Append($",color:\"{parts[5].Trim()}\"");
                        if (parts.Length > 6) sb.Append($",textAnchor:\"{parts[6].Trim()}\"");
                        if (parts.Length > 7) sb.Append($",fontWeight:\"{parts[7].Trim()}\"");
                        sb.Append('}');
                        break;

                    case "dim":
                    case "hdim":
                    case "vdim":
                        // dim,x1,y1,x2,y2,offset,text
                        if (parts.Length < 7) { first = true; break; }
                        sb.Append($"{{type:\"{type}\",x1:{Eval(parts[1])},y1:{Eval(parts[2])},x2:{Eval(parts[3])},y2:{Eval(parts[4])},offset:{Eval(parts[5])},text:\"{EscapeJs(parts[6].Trim())}\"}}");
                        break;

                    case "hatch":
                        // hatch,x1,y1,x2,y2[,color]
                        if (parts.Length < 5) { first = true; break; }
                        sb.Append($"{{type:\"hatch\",x1:{Eval(parts[1])},y1:{Eval(parts[2])},x2:{Eval(parts[3])},y2:{Eval(parts[4])}");
                        if (parts.Length > 5) sb.Append($",color:\"{parts[5].Trim()}\"");
                        sb.Append('}');
                        break;

                    default:
                        first = true; // skip unknown, don't add comma
                        break;
                }
            }

            sb.Append("],options:{");
            sb.Append($"width:{opts.GetInt("w", 600)},");
            sb.Append($"height:{opts.GetInt("h", 400)},");
            if (opts.Has("title")) sb.Append($"title:\"{EscapeJs(opts.Get("title"))}\",");
            if (opts.Has("bg")) sb.Append($"bg:\"{opts.Get("bg")}\",");
            if (opts.Has("flipy")) sb.Append($"flipY:{opts.Get("flipy").ToLowerInvariant()},");
            if (opts.Has("grid")) sb.Append("grid:true,");
            if (opts.Has("gridstep")) sb.Append($"gridStep:{opts.Get("gridstep")},");
            if (opts.Has("padding")) sb.Append($"padding:{opts.Get("padding")},");
            sb.Append("}});</script>");
            return sb.ToString();
        }

        private string Eval(string expr)
        {
            expr = expr.Trim();
            if (double.TryParse(expr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
                return F(num);
            // Try evaluating as Calcpad variable first
            try
            {
                var val = GetVarValue(expr);
                if (val is RealValue rv) return F(rv.D);
            }
            catch { }
            // Try evaluating as Calcpad math expression (e.g. L/2, h+1.5)
            try
            {
                Parser.Parse(expr);
                double d = Parser.CalculateReal();
                return F(d);
            }
            catch { }
            return expr;
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
        // Separator: ':' between key=value pairs (& conflicts with Calcpad units operator)
        private static VizOptions ParseOptions(string optionsPart)
        {
            var opts = new VizOptions();
            if (string.IsNullOrEmpty(optionsPart)) return opts;
            // Support both ':' (preferred) and '&' as separators
            var pairs = optionsPart.Contains(':') && !optionsPart.Contains('&')
                ? optionsPart.Split(':')
                : optionsPart.Split('&');
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
