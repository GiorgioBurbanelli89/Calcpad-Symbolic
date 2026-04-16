using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CpdToMcdx;

/// <summary>Convert Calcpad expression strings to MathCad Prime ml: XML</summary>
static class ExpressionConverter
{
    static readonly HashSet<string> MathFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sin","cos","tan","asin","acos","atan","atan2",
        "sinh","cosh","tanh","exp","ln","log","log2",
        "sqrt","sqr","abs","sign","round","floor","ceil","trunc",
        "min","max","mod","re","im","phase",
        "det","transp","inv","eigenvalues","eigenvectors",
        "lsolve","clsolve","symmetric","vector","matrix",
        "spline","submatrix","add","copy","diag","diagonal",
        "rows","cols","length","sum","product","integral"
    };

    static readonly HashSet<string> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        "m","cm","mm","km","in","ft","yd","mi",
        "m²","m³","cm²","cm³","mm²","mm³",
        "kg","g","mg","lb","oz","ton","tf","tonf",
        "s","ms","min","hr","day",
        "N","kN","MN","kgf","lbf","kip",
        "Pa","kPa","MPa","GPa","psi","ksi",
        "J","kJ","MJ","W","kW","MW","hp",
        "rad","deg","°",
        "m/s","m/s²","rpm",
        "tf/m","tf/m²","tf/m³","kN/m","kN/m²","kN/m³",
        "tonf/m","tonf/m²","tonf/m³",
        "mm/mm"
    };

    /// <summary>Convert a Calcpad expression to ml: XML fragment</summary>
    public static void WriteExpression(XmlWriter w, string expr)
    {
        expr = expr.Trim();
        if (string.IsNullOrEmpty(expr)) return;

        // Try to parse as number
        if (double.TryParse(expr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double val))
        {
            w.WriteStartElement("ml", "real", null);
            w.WriteString(val.ToString(System.Globalization.CultureInfo.InvariantCulture));
            w.WriteEndElement();
            return;
        }

        // Matrix literal: [1;2|3;4]
        if (expr.StartsWith("[") && expr.EndsWith("]"))
        {
            WriteMatrix(w, expr);
            return;
        }

        // Unit expression with &: value & unit
        int ampPos = FindOperator(expr, '&');
        if (ampPos > 0)
        {
            var valExpr = expr[..ampPos].Trim();
            var unitExpr = expr[(ampPos + 1)..].Trim();
            w.WriteStartElement("ml", "apply", null);
            w.WriteStartElement("ml", "scale", null); w.WriteEndElement();
            WriteExpression(w, valExpr);
            WriteUnit(w, unitExpr);
            w.WriteEndElement(); // apply
            return;
        }

        // Binary operators: +, -, *, /, ^
        // Order of precedence (lowest first): +/-, */÷, ^
        int opPos;

        // Addition/subtraction (lowest precedence)
        opPos = FindBinaryOp(expr, new[] { '+', '-' });
        if (opPos > 0)
        {
            char op = expr[opPos];
            var left = expr[..opPos].Trim();
            var right = expr[(opPos + 1)..].Trim();
            w.WriteStartElement("ml", "apply", null);
            w.WriteStartElement("ml", op == '+' ? "plus" : "minus", null); w.WriteEndElement();
            WriteExpression(w, left);
            WriteExpression(w, right);
            w.WriteEndElement();
            return;
        }

        // Multiplication/division
        opPos = FindBinaryOp(expr, new[] { '*', '/' });
        if (opPos > 0)
        {
            char op = expr[opPos];
            var left = expr[..opPos].Trim();
            var right = expr[(opPos + 1)..].Trim();

            // Check for ÷ (Unicode division)
            w.WriteStartElement("ml", "apply", null);
            w.WriteStartElement("ml", op == '*' ? "mult" : "div", null); w.WriteEndElement();
            WriteExpression(w, left);
            WriteExpression(w, right);
            w.WriteEndElement();
            return;
        }

        // Power
        opPos = FindBinaryOp(expr, new[] { '^' });
        if (opPos > 0)
        {
            var baseExpr = expr[..opPos].Trim();
            var expExpr = expr[(opPos + 1)..].Trim();
            w.WriteStartElement("ml", "apply", null);
            w.WriteStartElement("ml", "pow", null); w.WriteEndElement();
            WriteExpression(w, baseExpr);
            WriteExpression(w, expExpr);
            w.WriteEndElement();
            return;
        }

        // Unary minus
        if (expr.StartsWith("-"))
        {
            w.WriteStartElement("ml", "apply", null);
            w.WriteStartElement("ml", "neg", null); w.WriteEndElement();
            WriteExpression(w, expr[1..].Trim());
            w.WriteEndElement();
            return;
        }

        // Parenthesized expression
        if (expr.StartsWith("(") && FindMatchingParen(expr, 0) == expr.Length - 1)
        {
            WriteExpression(w, expr[1..^1].Trim());
            return;
        }

        // Function call: name(args)
        var funcMatch = Regex.Match(expr, @"^(\w[\w.]*)\((.+)\)$", RegexOptions.Singleline);
        if (funcMatch.Success)
        {
            var fname = funcMatch.Groups[1].Value;
            var argsStr = funcMatch.Groups[2].Value;
            var args = SplitArgs(argsStr);

            w.WriteStartElement("ml", "apply", null);
            WriteId(w, fname, MathFunctions.Contains(fname) ? null : "VARIABLE");
            foreach (var arg in args)
                WriteExpression(w, arg.Trim());
            w.WriteEndElement();
            return;
        }

        // Indexed access: v.i or M.(i;j)
        if (expr.Contains('.') && !expr.StartsWith("0.") && !char.IsDigit(expr[0]))
        {
            int dotPos = expr.IndexOf('.');
            var objName = expr[..dotPos];
            var idx = expr[(dotPos + 1)..];
            if (idx.StartsWith("(") && idx.EndsWith(")"))
                idx = idx[1..^1];

            w.WriteStartElement("ml", "apply", null);
            w.WriteStartElement("ml", "indexer", null); w.WriteEndElement();
            WriteId(w, objName, "VARIABLE");
            var indices = SplitArgs(idx);
            foreach (var ix in indices)
                WriteExpression(w, ix.Trim());
            w.WriteEndElement();
            return;
        }

        // Simple identifier
        WriteId(w, expr, IsUnit(expr) ? "UNIT" : "VARIABLE");
    }

    /// <summary>Write ml:id element</summary>
    static void WriteId(XmlWriter w, string name, string? label)
    {
        w.WriteStartElement("ml", "id", null);
        if (label != null) w.WriteAttributeString("labels", label);
        w.WriteAttributeString("xml", "space", null, "preserve");

        // Handle subscripts: name_sub or name.sub → XAML subscript
        var subMatch = Regex.Match(name, @"^(\w+)[_](\w+)$");
        if (subMatch.Success)
        {
            w.WriteRaw($"<Span xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                $"xmlns:pw=\"clr-namespace:Ptc.Wpf;assembly=Ptc.Core\">" +
                $"{subMatch.Groups[1].Value}<pw:Subscript>{subMatch.Groups[2].Value}</pw:Subscript></Span>");
        }
        else
        {
            // Handle Greek letters
            w.WriteString(ToGreek(name));
        }
        w.WriteEndElement();
    }

    /// <summary>Write unit identifier</summary>
    static void WriteUnit(XmlWriter w, string unit)
    {
        unit = unit.Trim();
        // Compound units: tf/m², kN*m, etc.
        int divPos = FindOperator(unit, '/');
        if (divPos > 0)
        {
            w.WriteStartElement("ml", "apply", null);
            w.WriteStartElement("ml", "div", null); w.WriteEndElement();
            WriteUnit(w, unit[..divPos].Trim());
            WriteUnit(w, unit[(divPos + 1)..].Trim());
            w.WriteEndElement();
            return;
        }
        int mulPos = FindOperator(unit, '*');
        if (mulPos > 0)
        {
            w.WriteStartElement("ml", "apply", null);
            w.WriteStartElement("ml", "mult", null); w.WriteEndElement();
            WriteUnit(w, unit[..mulPos].Trim());
            WriteUnit(w, unit[(mulPos + 1)..].Trim());
            w.WriteEndElement();
            return;
        }
        // Power: m^2, m²
        var powMatch = Regex.Match(unit, @"^(\w+)\^(\d+)$");
        if (powMatch.Success)
        {
            w.WriteStartElement("ml", "apply", null);
            w.WriteStartElement("ml", "pow", null); w.WriteEndElement();
            w.WriteStartElement("ml", "id", null);
            w.WriteAttributeString("labels", "UNIT");
            w.WriteString(powMatch.Groups[1].Value);
            w.WriteEndElement();
            w.WriteStartElement("ml", "real", null); w.WriteString(powMatch.Groups[2].Value); w.WriteEndElement();
            w.WriteEndElement();
            return;
        }
        // Simple unit
        w.WriteStartElement("ml", "id", null);
        w.WriteAttributeString("labels", "UNIT");
        w.WriteAttributeString("xml", "space", null, "preserve");
        w.WriteString(MapUnit(unit));
        w.WriteEndElement();
    }

    /// <summary>Write matrix/vector literal</summary>
    static void WriteMatrix(XmlWriter w, string expr)
    {
        var inner = expr[1..^1]; // Remove [ ]
        var rows = inner.Split('|');
        int nRows = rows.Length;
        var firstRow = SplitArgs(rows[0]);
        int nCols = firstRow.Count;

        // Parse all elements row-major
        var elements = new string[nRows, nCols];
        for (int r = 0; r < nRows; r++)
        {
            var cols = SplitArgs(rows[r]);
            for (int c = 0; c < Math.Min(cols.Count, nCols); c++)
                elements[r, c] = cols[c].Trim();
        }

        w.WriteStartElement("ml", "matrix", null);
        w.WriteAttributeString("rows", nRows.ToString());
        w.WriteAttributeString("cols", nCols.ToString());

        // MathCad stores column-major!
        for (int c = 0; c < nCols; c++)
            for (int r = 0; r < nRows; r++)
            {
                var elem = elements[r, c] ?? "0";
                if (double.TryParse(elem, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double v))
                {
                    w.WriteStartElement("ml", "real", null);
                    w.WriteString(v.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    w.WriteEndElement();
                }
                else
                    WriteExpression(w, elem);
            }

        w.WriteEndElement(); // matrix
    }

    /// <summary>Map Calcpad unit names to MathCad unit names</summary>
    static string MapUnit(string unit) => unit switch
    {
        "tf" or "tonf" => "tonnef",
        "kgf" => "kgf",
        "MPa" => "MPa",
        "GPa" => "GPa",
        "kPa" => "kPa",
        _ => unit
    };

    /// <summary>Convert common abbreviations to Greek Unicode</summary>
    static string ToGreek(string name) => name switch
    {
        "alpha" or "α" => "α", "beta" or "β" => "β", "gamma" or "γ" => "γ",
        "delta" or "δ" => "δ", "epsilon" or "ε" => "ε", "zeta" or "ζ" => "ζ",
        "eta" or "η" => "η", "theta" or "θ" => "θ",
        "kappa" or "κ" => "κ", "lambda" or "λ" => "λ",
        "mu" or "μ" => "μ", "nu" or "ν" => "ν", "xi" or "ξ" => "ξ",
        "pi" or "π" => "π", "rho" or "ρ" => "ρ", "sigma" or "σ" => "σ",
        "tau" or "τ" => "τ", "phi" or "φ" => "φ", "psi" or "ψ" => "ψ",
        "omega" or "ω" => "ω", "νc" => "νc",
        _ => name
    };

    static bool IsUnit(string name) => Units.Contains(name);

    /// <summary>Find binary operator at lowest precedence level (right-to-left for same level)</summary>
    static int FindBinaryOp(string expr, char[] ops)
    {
        int depth = 0;
        int lastPos = -1;
        for (int i = expr.Length - 1; i >= 1; i--)
        {
            char c = expr[i];
            if (c is ')' or ']' or '}') depth++;
            else if (c is '(' or '[' or '{') depth--;
            else if (depth == 0 && Array.IndexOf(ops, c) >= 0)
            {
                // Skip unary minus (after operator or at start)
                if (c == '-' && (i == 0 || expr[i - 1] is '(' or '[' or ',' or ';' or '+' or '-' or '*' or '/' or '^' or '=' or '&'))
                    continue;
                return i;
            }
        }
        return -1;
    }

    static int FindOperator(string expr, char op)
    {
        int depth = 0;
        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            else if (depth == 0 && c == op) return i;
        }
        return -1;
    }

    static int FindMatchingParen(string expr, int openPos)
    {
        int depth = 0;
        for (int i = openPos; i < expr.Length; i++)
        {
            if (expr[i] is '(' or '[') depth++;
            else if (expr[i] is ')' or ']') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    /// <summary>Split arguments separated by ; (respecting nesting)</summary>
    public static List<string> SplitArgs(string argsStr)
    {
        var result = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < argsStr.Length; i++)
        {
            char c = argsStr[i];
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            else if (depth == 0 && c == ';')
            {
                result.Add(argsStr[start..i]);
                start = i + 1;
            }
        }
        result.Add(argsStr[start..]);
        return result;
    }
}
