using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CpdToMcdx;

/// <summary>Read a .mcdx file and extract regions</summary>
static class McdxReader
{
    public static List<Region> Read(string mcdxPath)
    {
        var regions = new List<Region>();
        using var zip = ZipFile.OpenRead(mcdxPath);

        var wsEntry = zip.GetEntry("mathcad/worksheet.xml");
        if (wsEntry == null) throw new Exception("No worksheet.xml found in .mcdx");

        using var stream = wsEntry.Open();
        var doc = new XmlDocument();
        doc.Load(stream);

        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("ws", "http://schemas.mathsoft.com/worksheet50");
        nsMgr.AddNamespace("ml", "http://schemas.mathsoft.com/math50");
        nsMgr.AddNamespace("xaml", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");

        var regionNodes = doc.SelectNodes("//ws:regions/ws:region", nsMgr)
                       ?? doc.SelectNodes("//region");
        if (regionNodes == null) return regions;

        foreach (XmlNode rn in regionNodes)
        {
            // Picture region — skip
            var picNode = rn.SelectSingleNode("ws:picture", nsMgr) ?? rn.SelectSingleNode("picture");
            if (picNode != null)
                continue;

            // Text region
            var textNode = rn.SelectSingleNode("ws:text", nsMgr) ?? rn.SelectSingleNode("text");
            if (textNode != null)
            {
                var flowDoc = textNode.SelectSingleNode("xaml:FlowDocument", nsMgr)
                           ?? textNode.SelectSingleNode("*[local-name()='FlowDocument']");
                bool isHeading = false;
                int headingLevel = 0;

                if (flowDoc != null)
                {
                    var fontWeight = flowDoc.Attributes?["FontWeight"]?.Value ?? "Normal";
                    var fontSize = flowDoc.Attributes?["FontSize"]?.Value ?? "14";
                    if (fontWeight == "Bold" && double.TryParse(fontSize,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double fs))
                    {
                        if (fs > 20) { isHeading = true; headingLevel = 1; }
                        else if (fs > 16) { isHeading = true; headingLevel = 2; }
                        else if (fs > 14) { isHeading = true; headingLevel = 3; }
                    }
                }

                // Try to get text from XamlPackage
                var itemRef = textNode.Attributes?["item-idref"]?.Value;
                string text = ExtractFlowDocText(zip, itemRef);

                // Fallback: inline FlowDocument text
                if (string.IsNullOrWhiteSpace(text) && flowDoc != null)
                    text = flowDoc.InnerText?.Trim();

                if (string.IsNullOrWhiteSpace(text))
                    text = "[text region]";

                // Split multi-line text into separate regions
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        regions.Add(new Region(isHeading ? RegionType.Heading : RegionType.Text, trimmed)
                            { HeadingLevel = headingLevel });
                    }
                }
                continue;
            }

            // Math region
            var mathNode = rn.SelectSingleNode("ws:math", nsMgr) ?? rn.SelectSingleNode("math");
            if (mathNode == null)
            {
                // spec-table contains multiple math nodes
                var specTable = rn.SelectSingleNode("ws:spec-table", nsMgr) ?? rn.SelectSingleNode("spec-table");
                if (specTable != null)
                {
                    var mathNodes = specTable.SelectNodes("ws:math", nsMgr) ?? specTable.SelectNodes("math");
                    if (mathNodes != null)
                        foreach (XmlNode mn in mathNodes)
                            ParseMathNode(mn, nsMgr, regions);
                    continue;
                }
                continue;
            }

            ParseMathNode(mathNode, nsMgr, regions);
        }

        return PostProcess(regions);
    }

    /// <summary>Post-process: clean up expressions, convert unsupported features to comments</summary>
    static string DecodeHtmlEntities(string s)
    {
        if (s == null) return s;
        return s.Replace("&#241;", "ñ").Replace("&#209;", "Ñ")
                .Replace("&#233;", "é").Replace("&#225;", "á")
                .Replace("&#237;", "í").Replace("&#243;", "ó")
                .Replace("&#250;", "ú").Replace("&amp;", "&")
                .Replace("&lt;", "<").Replace("&gt;", ">");
    }

    static List<Region> PostProcess(List<Region> regions)
    {
        var result = new List<Region>();
        foreach (var r in regions)
        {
            // Decode HTML entities in ALL region types
            var region = r with { Content = DecodeHtmlEntities(r.Content) };

            if (region.Type == RegionType.Math || region.Type == RegionType.DisplayEq)
            {
                var cleaned = CleanExpression(region.Content);

                // Fix MathCad unit definitions: "tn = tonnef" → "tn = 1tonf"
                // Also: "Fuerza = tn", "Longitud = m", "ORIGIN" (standalone)
                cleaned = FixMathCadUnitDefs(cleaned);

                // Lines with pdiff():
                if (cleaned.Contains("pdiff("))
                {
                    bool isDefinition = cleaned.Contains("=") &&
                        (cleaned.Contains("[") || cleaned.IndexOf("pdiff") < cleaned.IndexOf("="));
                    if (isDefinition)
                        result.Add(new Region(RegionType.Directive, $"#sym {cleaned}"));
                    else
                        result.Add(region with { Content = cleaned });
                }
                // Multi-line program output (ConvertProgram generates \n-separated lines)
                else if (cleaned.Contains('\n'))
                {
                    foreach (var line in cleaned.Split('\n'))
                    {
                        var trimLine = line.TrimEnd();
                        if (string.IsNullOrWhiteSpace(trimLine)) continue;
                        if (trimLine.StartsWith("#"))
                            result.Add(new Region(RegionType.Directive, trimLine));
                        else
                            result.Add(new Region(RegionType.Math, trimLine));
                    }
                }
                // Lines with '[program] → comment (fallback)
                else if (cleaned.Contains("'[program]"))
                {
                    var varName = region.VarName ?? "?";
                    result.Add(new Region(RegionType.Comment,
                        $"'{varName} = [MathCad program — requiere conversion manual]"));
                }
                else
                {
                    result.Add(region with { Content = cleaned });
                }
            }
            else
            {
                result.Add(region);
            }
        }
        return result;
    }

    /// <summary>Clean redundant outer parentheses from expressions</summary>
    static string CleanExpression(string expr)
    {
        // Remove redundant double parens: ((x)) → (x)
        while (expr.Contains("((") && expr.Contains("))"))
        {
            var before = expr;
            expr = Regex.Replace(expr, @"\(\(([^()]*)\)\)", "($1)");
            if (expr == before) break;
        }
        // Fix HTML entities in variable names
        expr = expr.Replace("&#241;", "ñ").Replace("&#209;", "Ñ");
        expr = expr.Replace("&#233;", "é").Replace("&#225;", "á")
                   .Replace("&#237;", "í").Replace("&#243;", "ó")
                   .Replace("&#250;", "ú");
        // Fix matrix inverse: M^-1 → M^(-1)
        expr = Regex.Replace(expr, @"\^-(\d+)", "^(-$1)");
        return expr;
    }

    /// <summary>Map MathCad unit names to Calcpad equivalents</summary>
    static readonly Dictionary<string, string> UnitMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tn"] = "tonf",
        ["tonnef"] = "tonf",
        ["tonf"] = "tonf",
        ["kgf"] = "kgf",
        ["lbf"] = "lbf",
        ["kip"] = "kip",
        ["psi"] = "psi",
        ["Pa"] = "Pa",
        ["MPa"] = "MPa",
        ["GPa"] = "GPa",
        ["kPa"] = "kPa",
    };

    static string MapUnit(string unit)
    {
        if (UnitMap.TryGetValue(unit, out var mapped)) return mapped;
        // Handle compound units: tn/m^2 → tonf/m^2
        foreach (var kv in UnitMap)
            if (unit.Contains(kv.Key))
                unit = unit.Replace(kv.Key, kv.Value);
        return unit;
    }

    /// <summary>Fix MathCad unit definition patterns</summary>
    static string FixMathCadUnitDefs(string expr)
    {
        if (string.IsNullOrEmpty(expr)) return expr;
        var trimmed = expr.Trim();

        // "ORIGIN" standalone → comment (Calcpad always uses 1-based)
        if (trimmed == "ORIGIN" || trimmed == "ORIGIN = 1")
            return "'ORIGIN = 1 (Calcpad siempre usa indices desde 1)";
        if (Regex.IsMatch(trimmed, @"^ORIGIN\s*=\s*\d+$"))
            return $"'{trimmed} (Calcpad siempre usa indices desde 1)";

        // Known MathCad unit names used as bare identifiers in assignments
        // "tn = tonnef" → "tn = 1tonf"
        // "Fuerza = tn" → "'Fuerza = tn (unidad MathCad)"
        var knownUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tonnef", "tn", "kgf", "lbf", "kip", "N", "kN", "MN",
            "m", "cm", "mm", "km", "in", "ft", "yd",
            "Pa", "kPa", "MPa", "GPa", "psi",
            "s", "min", "hr",
        };

        var match = Regex.Match(trimmed, @"^(\w+)\s*=\s*(\w+)$");
        if (match.Success)
        {
            var lhs = match.Groups[1].Value;
            var rhs = match.Groups[2].Value;
            // If RHS is a known unit name → unit alias definition
            if (knownUnits.Contains(rhs))
            {
                var mapped = MapUnit(rhs);
                return $"{lhs} = 1{mapped}";
            }
        }

        // Replace bare "tonnef" anywhere in expression
        expr = Regex.Replace(expr, @"\btonnef\b", "tonf");
        // Replace bare "tn" when used as unit (after number or in unit context)
        // Be careful: "tn" could be a variable name. Only replace in unit positions.
        expr = Regex.Replace(expr, @"(\d)tn\b", "${1}tonf");
        expr = Regex.Replace(expr, @"tn/", "tonf/");
        expr = Regex.Replace(expr, @"tn\*", "tonf*");

        return expr;
    }

    static void ParseMathNode(XmlNode mathNode, XmlNamespaceManager nsMgr, List<Region> regions)
    {
        var defineNode = mathNode.SelectSingleNode("ml:define", nsMgr);
        if (defineNode != null)
        {
            var (varName, funcArgs) = ExtractDefineLhs(defineNode, nsMgr);
            var rhs = defineNode.ChildNodes.Count > 1 ? defineNode.ChildNodes[defineNode.ChildNodes.Count - 1] : null;
            string expr = rhs != null ? MathNodeToExpr(rhs, nsMgr) : "";

            // If RHS is a program (multi-line), emit the variable name as comment, then the program lines
            if (expr.Contains('\n'))
            {
                // Program block — emit lines separately.
                // The variable is assigned inside the program via localDefine.
                // Don't emit variable name as comment — the program body handles assignments
                // Emit each program line as its own region
                foreach (var line in expr.Split('\n'))
                {
                    var trimLine = line.TrimEnd();
                    if (string.IsNullOrWhiteSpace(trimLine)) continue;
                    if (trimLine.StartsWith("#"))
                        regions.Add(new Region(RegionType.Directive, trimLine));
                    else
                        regions.Add(new Region(RegionType.Math, trimLine));
                }
            }
            else
            {
                string content = funcArgs != null ? $"{varName}({funcArgs}) = {expr}" : $"{varName} = {expr}";
                regions.Add(new Region(RegionType.Math, content)
                    { VarName = varName, Expression = expr, FuncArgs = funcArgs });
            }
        }
        else
        {
            // Evaluation or standalone expression
            var firstChild = mathNode.FirstChild;
            if (firstChild != null)
            {
                string expr = MathNodeToExpr(firstChild, nsMgr);
                regions.Add(new Region(RegionType.Math, expr) { Expression = expr });
            }
        }
    }

    static (string varName, string? funcArgs) ExtractDefineLhs(XmlNode defineNode, XmlNamespaceManager nsMgr)
    {
        var first = defineNode.FirstChild;
        if (first == null) return ("?", null);

        // Function: <ml:function><ml:id>name</ml:id><ml:boundVars>...</ml:boundVars></ml:function>
        if (first.LocalName == "function")
        {
            var idNode = first.SelectSingleNode("ml:id", nsMgr);
            var name = ExtractIdText(idNode);
            var boundVars = first.SelectSingleNode("ml:boundVars", nsMgr);
            if (boundVars != null)
            {
                var args = new List<string>();
                foreach (XmlNode bv in boundVars.ChildNodes)
                    args.Add(ExtractIdText(bv));
                return (name, string.Join("; ", args));
            }
            return (name, null);
        }

        // Variable: <ml:id>name</ml:id>
        if (first.LocalName == "id")
            return (ExtractIdText(first), null);

        // Indexed: <ml:apply><ml:indexer/>...
        return (MathNodeToExpr(first, nsMgr), null);
    }

    static string ExtractIdText(XmlNode? node)
    {
        if (node == null) return "?";
        // Check for XAML Span with subscripts (MathCad Prime uses pw:Subscript)
        var innerXml = node.InnerXml;
        if (innerXml.Contains("Subscript"))
        {
            var sb = new StringBuilder();
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Text)
                    sb.Append(child.Value?.Trim());
                else if (child.LocalName == "Span")
                {
                    foreach (XmlNode spanChild in child.ChildNodes)
                    {
                        if (spanChild.NodeType == XmlNodeType.Text)
                            sb.Append(spanChild.Value?.Trim());
                        else if (spanChild.LocalName == "Subscript")
                            sb.Append("_" + spanChild.InnerText.Trim());
                    }
                }
            }
            return sb.ToString();
        }
        return node.InnerText.Trim();
    }

    /// <summary>Convert MathCad ml: XML node to Calcpad expression string</summary>
    static string MathNodeToExpr(XmlNode node, XmlNamespaceManager nsMgr)
    {
        switch (node.LocalName)
        {
            case "real":
                return node.InnerText.Trim();

            case "id":
                return ExtractIdText(node);

            case "str":
                return $"\"{node.InnerText}\"";

            case "matrix":
                return MatrixToExpr(node, nsMgr);

            case "apply":
                return ApplyToExpr(node, nsMgr);

            case "define":
                var (vn, fa) = ExtractDefineLhs(node, nsMgr);
                var rhsNode = node.ChildNodes.Count > 1 ? node.ChildNodes[node.ChildNodes.Count - 1] : null;
                var rhsExpr = rhsNode != null ? MathNodeToExpr(rhsNode, nsMgr) : "";
                return fa != null ? $"{vn}({fa}) = {rhsExpr}" : $"{vn} = {rhsExpr}";

            case "range":
                return RangeToExpr(node, nsMgr);

            case "sequence":
                var seqParts = new List<string>();
                foreach (XmlNode child in node.ChildNodes)
                    seqParts.Add(MathNodeToExpr(child, nsMgr));
                return string.Join("; ", seqParts);

            case "parens":
                // Extract content inside parens
                if (node.ChildNodes.Count == 1)
                    return $"({MathNodeToExpr(node.ChildNodes[0]!, nsMgr)})";
                if (node.ChildNodes.Count == 0)
                    return "()";
                var parenParts = new List<string>();
                foreach (XmlNode child in node.ChildNodes)
                    parenParts.Add(MathNodeToExpr(child, nsMgr));
                return $"({string.Join("; ", parenParts)})";

            case "eval":
                return EvalToExpr(node, nsMgr);

            case "placeholder":
                return "?"; // MathCad placeholder = empty slot

            case "lambda":
                return LambdaToExpr(node, nsMgr);

            case "degree":
                // degree node in derivatives — usually contains placeholder or number
                if (node.ChildNodes.Count == 1)
                {
                    var deg = MathNodeToExpr(node.ChildNodes[0]!, nsMgr);
                    return deg == "?" ? "1" : deg; // placeholder = first derivative
                }
                return "1";

            case "boundVars":
                var bvParts = new List<string>();
                foreach (XmlNode child in node.ChildNodes)
                    bvParts.Add(MathNodeToExpr(child, nsMgr));
                return string.Join("; ", bvParts);

            case "unitOverride":
                // Unit override after eval — usually placeholder
                if (node.ChildNodes.Count == 1 && node.ChildNodes[0]!.LocalName == "placeholder")
                    return ""; // no unit
                if (node.ChildNodes.Count == 1)
                    return MathNodeToExpr(node.ChildNodes[0]!, nsMgr);
                return "";

            case "program":
                return ConvertProgram(node, nsMgr);

            case "lowerBound":
                if (node.ChildNodes.Count == 1)
                    return MathNodeToExpr(node.ChildNodes[0]!, nsMgr);
                return "1";

            case "upperBound":
                if (node.ChildNodes.Count == 1)
                    return MathNodeToExpr(node.ChildNodes[0]!, nsMgr);
                return "n";

            default:
                return $"'[{node.LocalName}]"; // prefix with comment marker
        }
    }

    /// <summary>Handle ml:eval — evaluate with optional unit override</summary>
    static string EvalToExpr(XmlNode node, XmlNamespaceManager nsMgr)
    {
        // <ml:eval>
        //   <ml:apply>...</ml:apply>          — expression
        //   <ml:unitOverride>...</ml:unitOverride> — optional unit
        // </ml:eval>
        string expr = "0";
        string unit = "";
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.LocalName == "unitOverride")
            {
                // MathCad unitOverride = display in specific unit — skip for Calcpad
                // (Calcpad doesn't have unit display override)
            }
            else
            {
                expr = MathNodeToExpr(child, nsMgr);
            }
        }
        return expr + unit;
    }

    /// <summary>Handle ml:lambda — used in derivatives</summary>
    static string LambdaToExpr(XmlNode node, XmlNamespaceManager nsMgr)
    {
        // <ml:lambda>
        //   <ml:boundVars><ml:id>x</ml:id></ml:boundVars>
        //   <ml:apply>...</ml:apply>  — body expression
        // </ml:lambda>
        string? varName = null;
        string body = "?";
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.LocalName == "boundVars")
            {
                var parts = new List<string>();
                foreach (XmlNode bv in child.ChildNodes)
                    parts.Add(ExtractIdText(bv));
                varName = string.Join("; ", parts);
            }
            else
            {
                body = MathNodeToExpr(child, nsMgr);
            }
        }
        // For derivatives, we want the body (the function call)
        return body;
    }

    static string ApplyToExpr(XmlNode node, XmlNamespaceManager nsMgr)
    {
        if (node.ChildNodes.Count < 1) return "?";
        var op = node.ChildNodes[0]!;
        var operands = new List<string>();
        for (int i = 1; i < node.ChildNodes.Count; i++)
            operands.Add(MathNodeToExpr(node.ChildNodes[i]!, nsMgr));

        var opName = op.LocalName;

        // Function call: <ml:apply><ml:id labels="FUNCTION">f</ml:id><args...></ml:apply>
        if (opName == "id")
        {
            var funcName = ExtractIdText(op);
            // Map MathCad function names to Calcpad equivalents
            funcName = funcName switch
            {
                "rows" => "len",
                "cols" => "n_cols",
                "length" => "len",
                "eigenvals" => "eigenvalues",
                "eigenvecs" => "eigenvectors",
                "Re" => "re",
                "Im" => "im",
                "ceil" => "ceiling",
                "mod" => "remainder",   // mod(a,b) → a - floor(a/b)*b
                "geninv" => "pseudoinverse",
                _ => funcName
            };
            // If single sequence argument, expand it
            if (operands.Count == 1 && operands[0].StartsWith("[") == false)
                return $"{funcName}({operands[0]})";
            return $"{funcName}({string.Join("; ", operands)})";
        }

        return opName switch
        {
            // Arithmetic — avoid redundant parens on simple operands
            "plus" when operands.Count == 1 => operands[0],
            "plus" => $"{operands[0]} + {operands[1]}",
            "minus" when operands.Count == 1 => $"-{WrapIfComplex(operands[0])}",
            "minus" => $"{operands[0]} - {WrapIfNeedsParen(operands[1], "+-")}",
            "mult" => $"{WrapIfNeedsParen(operands[0], "+-")}*{WrapIfNeedsParen(operands[1], "+-")}",
            "div" => $"{operands[0]}/{WrapIfNeedsParen(operands[1], "+-*/")}",
            "pow" => $"{WrapIfComplex(operands[0])}^{WrapIfComplex(operands[1])}",
            "neg" => $"-{WrapIfComplex(operands[0])}",

            // Comparison
            "equal" => $"{operands[0]} = {operands[1]}",
            "lessThan" => $"{operands[0]} < {operands[1]}",
            "greaterThan" => $"{operands[0]} > {operands[1]}",
            "lessOrEqual" => $"{operands[0]} ≤ {operands[1]}",
            "greaterOrEqual" => $"{operands[0]} ≥ {operands[1]}",

            // Indexing
            "indexer" when operands.Count == 2 && !operands[1].Contains(";") => $"{operands[0]}.{operands[1]}",
            "indexer" when operands.Count == 2 && operands[1].Contains(";") => $"{operands[0]}.({operands[1]})",
            "indexer" => $"{operands[0]}.({string.Join("; ", operands.Skip(1))})",

            // Parens (explicit grouping in apply)
            "parens" when operands.Count >= 1 => $"({operands[0]})",
            "parens" => "()",

            // Absolute value
            "absval" => $"abs({operands[0]})",

            // Units
            // Units: MathCad scale → Calcpad: value+unit (no & operator)
            // Units: value + unit — need separator if value ends with digit and unit starts with digit
            "scale" when operands.Count >= 2 && operands[1] != "?" => FormatScale(operands[0], operands[1]),
            "scale" => operands.Count > 0 ? operands[0] : "0",

            // Roots
            "nthRoot" when operands.Count >= 2 && operands[1] == "2" => $"sqr({operands[0]})",
            "nthRoot" when operands.Count >= 2 => $"{operands[0]}^(1/{operands[1]})",

            // Partial derivatives: ∂f/∂x
            // Calcpad doesn't have diff() as function — use ∂ notation for display
            "partDerivative" or "partialdiff" => ConvertPartialDerivative(operands, node, nsMgr),

            // Summation, product, integral — parse from XML children directly
            "summation" => ConvertSummation(node, nsMgr),
            "product" => ConvertSummation(node, nsMgr, "product"),
            "integral" => $"integral({string.Join("; ", operands)})",

            // Matrix operations
            "transpose" => $"transp({operands[0]})",
            "determinant" => $"det({operands[0]})",
            "inverse" => $"{WrapIfComplex(operands[0])}^(-1)",
            "matmult" => $"{operands[0]}*{operands[1]}",
            "vectorize" => operands[0],

            // MathCad built-in functions → Calcpad equivalents
            "augment" => $"augment({string.Join("; ", operands)})",
            "stack" => $"stack({string.Join("; ", operands)})",
            "rows" => $"len({operands[0]})",       // rows(v) → len(v) for vectors
            "cols" when operands.Count >= 1 => $"n_cols({operands[0]})",
            "identity" => $"identity({operands[0]})",
            "submatrix" => $"submatrix({string.Join("; ", operands)})",
            "max" when operands.Count >= 2 => $"max({operands[0]}; {operands[1]})",
            "min" when operands.Count >= 2 => $"min({operands[0]}; {operands[1]})",

            // Eval
            "eval" when operands.Count >= 1 => operands[0],

            // Lambda, degree, placeholder — handled in main switch
            "lambda" => operands.Count >= 1 ? operands[^1] : "?",
            "degree" => operands.Count >= 1 && operands[0] != "?" ? operands[0] : "1",
            "placeholder" => "?",

            // Unknown — mark as comment
            _ => $"'{opName}({string.Join("; ", operands)})"
        };
    }

    /// <summary>Convert partDerivative to Calcpad diff() syntax</summary>
    static string ConvertPartialDerivative(List<string> operands, XmlNode applyNode, XmlNamespaceManager nsMgr)
    {
        // Structure: <ml:apply><ml:partDerivative/>
        //   <ml:lambda><ml:boundVars><ml:id>ξ</ml:id></ml:boundVars>
        //     <ml:apply><ml:id>x</ml:id><ml:sequence><ml:id>ξ</ml:id><ml:id>η</ml:id></ml:sequence></ml:apply>
        //   </ml:lambda>
        //   <ml:degree><ml:placeholder/></ml:degree>
        // </ml:apply>
        //
        // → diff(x(ξ; η); ξ)

        string funcExpr = "?";
        string diffVar = "?";

        // Find lambda node
        for (int i = 1; i < applyNode.ChildNodes.Count; i++)
        {
            var child = applyNode.ChildNodes[i]!;
            if (child.LocalName == "lambda")
            {
                // Extract bound variable (the differentiation variable)
                var boundVars = child.SelectSingleNode("ml:boundVars", nsMgr)
                             ?? child.SelectSingleNode("*[local-name()='boundVars']");
                if (boundVars != null && boundVars.ChildNodes.Count > 0)
                    diffVar = ExtractIdText(boundVars.ChildNodes[0]);

                // Extract function body
                foreach (XmlNode lambdaChild in child.ChildNodes)
                {
                    if (lambdaChild.LocalName != "boundVars")
                    {
                        funcExpr = MathNodeToExpr(lambdaChild, nsMgr);
                        break;
                    }
                }
            }
        }

        // Use #sym pdiff() syntax — will be handled as separate line by PostProcess
        if (diffVar != "?")
            return $"pdiff({funcExpr}; {diffVar})";

        // Fallback
        return operands.Count >= 2
            ? $"pdiff({operands[0]}; {operands[1]})"
            : $"pdiff({string.Join("; ", operands)})";
    }

    /// <summary>Format value with unit: 5m, 0*1/m, etc.</summary>
    static string FormatScale(string value, string unit)
    {
        if (string.IsNullOrEmpty(unit) || unit == "?") return value;
        unit = MapUnit(unit);
        // If value ends with digit/paren and unit starts with digit, need * separator
        char lastVal = value.Length > 0 ? value[^1] : ' ';
        char firstUnit = unit.Length > 0 ? unit[0] : ' ';
        bool needsSep = (char.IsDigit(lastVal) || lastVal == ')') && (char.IsDigit(firstUnit) || firstUnit == '(');
        return needsSep ? $"{value}*{unit}" : $"{value}{unit}";
    }

    /// <summary>Convert summation/product with lambda+bounds to Calcpad sum/product</summary>
    static string ConvertSummation(XmlNode applyNode, XmlNamespaceManager nsMgr, string funcName = "sum")
    {
        // Structure: <ml:apply><ml:summation/>
        //   <ml:lambda><ml:boundVars><ml:id>i</ml:id></ml:boundVars>
        //     <ml:apply>...body...</ml:apply>
        //   </ml:lambda>
        //   <ml:lowerBound><ml:real>1</ml:real></ml:lowerBound>
        //   <ml:upperBound><ml:id>N_a</ml:id></ml:upperBound>
        // </ml:apply>
        //
        // → sum(body; lower; upper)  — Calcpad: $Sum{body @ var = lower : upper}

        string body = "?";
        string iterVar = "i";
        string lower = "1";
        string upper = "n";

        for (int i = 1; i < applyNode.ChildNodes.Count; i++)
        {
            var child = applyNode.ChildNodes[i]!;
            switch (child.LocalName)
            {
                case "lambda":
                    var boundVars = child.SelectSingleNode("ml:boundVars", nsMgr)
                                 ?? child.SelectSingleNode("*[local-name()='boundVars']");
                    if (boundVars != null && boundVars.ChildNodes.Count > 0)
                        iterVar = ExtractIdText(boundVars.ChildNodes[0]);
                    foreach (XmlNode lambdaChild in child.ChildNodes)
                    {
                        if (lambdaChild.LocalName != "boundVars")
                        {
                            body = MathNodeToExpr(lambdaChild, nsMgr);
                            break;
                        }
                    }
                    break;
                case "lowerBound":
                    lower = MathNodeToExpr(child.ChildNodes.Count > 0 ? child.ChildNodes[0]! : child, nsMgr);
                    break;
                case "upperBound":
                    upper = MathNodeToExpr(child.ChildNodes.Count > 0 ? child.ChildNodes[0]! : child, nsMgr);
                    break;
            }
        }

        // Calcpad doesn't have built-in sum with iterator, use #for loop comment
        // For now output as: sum(body; lower; upper)
        return $"{funcName}({body}; {lower}; {upper})";
    }

    /// <summary>Wrap expression in parens if it contains given operators at top level</summary>
    static string WrapIfNeedsParen(string expr, string ops)
    {
        if (string.IsNullOrEmpty(expr)) return expr;
        // Already parenthesized
        if (expr.StartsWith("(") && expr.EndsWith(")") && IsBalanced(expr, 1, expr.Length - 2))
            return expr;
        // Check for top-level operators
        int depth = 0;
        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == '(' || c == '[') depth++;
            else if (c == ')' || c == ']') depth--;
            else if (depth == 0 && ops.Contains(c) && c != '-' && i > 0)
                return $"({expr})";
            else if (depth == 0 && c == '-' && i > 0 && ops.Contains('-'))
                return $"({expr})";
        }
        return expr;
    }

    /// <summary>Wrap if expression is complex (has operators)</summary>
    static string WrapIfComplex(string expr)
    {
        if (string.IsNullOrEmpty(expr)) return expr;
        if (expr.StartsWith("(") && expr.EndsWith(")")) return expr;
        // Simple: number, identifier, function call
        if (Regex.IsMatch(expr, @"^[\w.]+$")) return expr;
        if (Regex.IsMatch(expr, @"^[\w.]+\(.*\)$")) return expr;
        if (Regex.IsMatch(expr, @"^-?[\d.]+$")) return expr;
        // Has operators
        int depth = 0;
        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == '(' || c == '[') depth++;
            else if (c == ')' || c == ']') depth--;
            else if (depth == 0 && "+-*/".Contains(c) && i > 0)
                return $"({expr})";
        }
        return expr;
    }

    static bool IsBalanced(string expr, int start, int end)
    {
        int depth = 0;
        for (int i = start; i <= end; i++)
        {
            if (expr[i] == '(' || expr[i] == '[') depth++;
            else if (expr[i] == ')' || expr[i] == ']') depth--;
            if (depth < 0) return false;
        }
        return depth == 0;
    }

    static string MatrixToExpr(XmlNode node, XmlNamespaceManager nsMgr)
    {
        int rows = int.Parse(node.Attributes?["rows"]?.Value ?? "1");
        int cols = int.Parse(node.Attributes?["cols"]?.Value ?? "1");
        var elements = new string[rows, cols];

        // Column-major order in MathCad!
        int idx = 0;
        foreach (XmlNode child in node.ChildNodes)
        {
            int c = idx / rows;
            int r = idx % rows;
            if (r < rows && c < cols)
                elements[r, c] = MathNodeToExpr(child, nsMgr);
            idx++;
        }

        // Vector (single column) → [a; b; c]
        if (cols == 1)
        {
            var sb = new StringBuilder("[");
            for (int r = 0; r < rows; r++)
            {
                if (r > 0) sb.Append("; ");
                sb.Append(elements[r, 0] ?? "0");
            }
            sb.Append("]");
            return sb.ToString();
        }

        // Matrix → [r1c1; r1c2|r2c1; r2c2]
        {
            var sb = new StringBuilder("[");
            for (int r = 0; r < rows; r++)
            {
                if (r > 0) sb.Append("|");
                for (int c = 0; c < cols; c++)
                {
                    if (c > 0) sb.Append("; ");
                    sb.Append(elements[r, c] ?? "0");
                }
            }
            sb.Append("]");
            return sb.ToString();
        }
    }

    static string RangeToExpr(XmlNode node, XmlNamespaceManager nsMgr)
    {
        var parts = new List<string>();
        foreach (XmlNode child in node.ChildNodes)
            parts.Add(MathNodeToExpr(child, nsMgr));
        return string.Join(" : ", parts);
    }

    static string? ExtractFlowDocText(ZipArchive zip, string? itemRef)
    {
        if (itemRef == null) return null;
        // Find in worksheet.xml.rels
        var relsEntry = zip.GetEntry("mathcad/_rels/worksheet.xml.rels");
        if (relsEntry == null) return null;
        using var relsStream = relsEntry.Open();
        var relsDoc = new XmlDocument();
        relsDoc.Load(relsStream);
        foreach (XmlNode rel in relsDoc.DocumentElement!.ChildNodes)
        {
            if (rel.Attributes?["Id"]?.Value == itemRef)
            {
                var target = rel.Attributes?["Target"]?.Value;
                if (target == null) return null;
                // Target can be relative or absolute
                var entryPath = target.TrimStart('/');
                if (!entryPath.StartsWith("mathcad/") && !entryPath.Contains("/"))
                    entryPath = "mathcad/" + entryPath;

                var xamlEntry = zip.GetEntry(entryPath);
                if (xamlEntry == null)
                {
                    // Try without mathcad/ prefix
                    xamlEntry = zip.GetEntry(target.TrimStart('/'));
                }
                if (xamlEntry == null) return null;

                // XamlPackage is a ZIP itself containing Document.xaml
                using var xamlStream = xamlEntry.Open();
                using var ms = new MemoryStream();
                xamlStream.CopyTo(ms);
                ms.Position = 0;
                try
                {
                    using var innerZip = new ZipArchive(ms, ZipArchiveMode.Read);
                    var docEntry = innerZip.GetEntry("Document.xaml")
                                ?? innerZip.GetEntry("Documento.xaml")
                                ?? innerZip.Entries.FirstOrDefault(e => e.Name.EndsWith(".xaml"));
                    if (docEntry == null) return null;
                    using var docStream = docEntry.Open();
                    var docXml = new XmlDocument();
                    docXml.Load(docStream);
                    return ExtractTextFromFlowDoc(docXml.DocumentElement);
                }
                catch { return null; }
            }
        }
        return null;
    }

    /// <summary>Recursively extract text from FlowDocument XML</summary>
    static string? ExtractTextFromFlowDoc(XmlNode? node)
    {
        if (node == null) return null;
        var sb = new StringBuilder();
        ExtractTextRecursive(node, sb);
        var text = sb.ToString().Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    static void ExtractTextRecursive(XmlNode node, StringBuilder sb)
    {
        if (node.NodeType == XmlNodeType.Text)
        {
            sb.Append(node.Value);
            return;
        }

        var localName = node.LocalName;

        // Paragraph break
        if (localName == "Paragraph" && sb.Length > 0)
            sb.Append('\n');

        // LineBreak
        if (localName == "LineBreak")
        {
            sb.Append('\n');
            return;
        }

        foreach (XmlNode child in node.ChildNodes)
            ExtractTextRecursive(child, sb);
    }

    /// <summary>Convert MathCad program block to Calcpad code lines</summary>
    /// <remarks>
    /// MathCad programs contain: for, if/else/elseif, localDefine, return, etc.
    /// We convert them to multi-line Calcpad code separated by newlines.
    /// The caller (PostProcess) will split these into separate Region lines.
    /// </remarks>
    static string ConvertProgram(XmlNode progNode, XmlNamespaceManager nsMgr)
    {
        var lines = new List<string>();
        ConvertProgramLines(progNode, nsMgr, lines, 0);
        if (lines.Count == 0) return "'[program vacío]";
        return string.Join("\n", lines);
    }

    static void ConvertProgramLines(XmlNode node, XmlNamespaceManager nsMgr, List<string> lines, int indent)
    {
        var pad = new string(' ', indent * 4);
        foreach (XmlNode child in node.ChildNodes)
        {
            switch (child.LocalName)
            {
                case "localDefine":
                case "define":
                {
                    // localDefine has same structure as define: lhs = rhs
                    var defNode = child.LocalName == "localDefine" ? child : child;
                    if (defNode.ChildNodes.Count >= 2)
                    {
                        var lhs = MathNodeToExpr(defNode.ChildNodes[0]!, nsMgr);
                        var rhs = MathNodeToExpr(defNode.ChildNodes[1]!, nsMgr);
                        lines.Add($"{pad}{lhs} = {rhs}");
                    }
                    break;
                }
                case "for":
                {
                    // <ml:for><ml:id>j</ml:id><ml:range>...</ml:range><ml:program>...</ml:program></ml:for>
                    string iterVar = "i";
                    string lower = "1", upper = "n";
                    XmlNode bodyNode = null;
                    foreach (XmlNode fc in child.ChildNodes)
                    {
                        switch (fc.LocalName)
                        {
                            case "id":
                                iterVar = ExtractIdText(fc);
                                break;
                            case "range":
                                if (fc.ChildNodes.Count >= 1)
                                    lower = MathNodeToExpr(fc.ChildNodes[0]!, nsMgr);
                                if (fc.ChildNodes.Count >= 2)
                                    upper = MathNodeToExpr(fc.ChildNodes[1]!, nsMgr);
                                // If 3 children: start, step, end
                                if (fc.ChildNodes.Count >= 3)
                                    upper = MathNodeToExpr(fc.ChildNodes[2]!, nsMgr);
                                break;
                            case "program":
                                bodyNode = fc;
                                break;
                        }
                    }
                    lines.Add($"{pad}#for {iterVar} = {lower} : {upper}");
                    if (bodyNode != null)
                        ConvertProgramLines(bodyNode, nsMgr, lines, indent + 1);
                    lines.Add($"{pad}#loop");
                    break;
                }
                case "if":
                {
                    // <ml:if><ml:apply>condition</ml:apply><ml:then><ml:program>...</ml:program></ml:then>
                    //   [<ml:else><ml:program>...</ml:program></ml:else>]
                    //   [<ml:elseif><ml:apply>cond</ml:apply><ml:then>...</ml:then></ml:elseif>]
                    // </ml:if>
                    bool first = true;
                    foreach (XmlNode ifChild in child.ChildNodes)
                    {
                        switch (ifChild.LocalName)
                        {
                            case "apply" when first:
                                var cond = MathNodeToExpr(ifChild, nsMgr);
                                // MathCad uses = for comparison, Calcpad uses ≡
                                cond = cond.Replace(" = ", " ≡ ");
                                lines.Add($"{pad}#if {cond}");
                                first = false;
                                break;
                            case "then":
                                if (ifChild.ChildNodes.Count > 0)
                                {
                                    var thenNode = ifChild.ChildNodes[0]!;
                                    if (thenNode.LocalName == "program")
                                        ConvertProgramLines(thenNode, nsMgr, lines, indent + 1);
                                    else
                                        lines.Add($"{pad}    {MathNodeToExpr(thenNode, nsMgr)}");
                                }
                                break;
                            case "elseif":
                            {
                                var eiCond = "true";
                                foreach (XmlNode eiChild in ifChild.ChildNodes)
                                {
                                    if (eiChild.LocalName == "apply" || eiChild.LocalName == "test")
                                    {
                                        eiCond = MathNodeToExpr(eiChild.LocalName == "test" && eiChild.ChildNodes.Count > 0 ? eiChild.ChildNodes[0]! : eiChild, nsMgr);
                                        eiCond = eiCond.Replace(" = ", " ≡ ");
                                    }
                                    else if (eiChild.LocalName == "then")
                                    {
                                        lines.Add($"{pad}#else if {eiCond}");
                                        if (eiChild.ChildNodes.Count > 0)
                                        {
                                            var tn = eiChild.ChildNodes[0]!;
                                            if (tn.LocalName == "program")
                                                ConvertProgramLines(tn, nsMgr, lines, indent + 1);
                                            else
                                                lines.Add($"{pad}    {MathNodeToExpr(tn, nsMgr)}");
                                        }
                                    }
                                }
                                break;
                            }
                            case "else":
                                lines.Add($"{pad}#else");
                                if (ifChild.ChildNodes.Count > 0)
                                {
                                    var elseNode = ifChild.ChildNodes[0]!;
                                    if (elseNode.LocalName == "program")
                                        ConvertProgramLines(elseNode, nsMgr, lines, indent + 1);
                                    else
                                        lines.Add($"{pad}    {MathNodeToExpr(elseNode, nsMgr)}");
                                }
                                break;
                        }
                    }
                    lines.Add($"{pad}#end if");
                    break;
                }
                case "apply":
                    // Standalone expression (e.g., return value)
                    lines.Add($"{pad}{MathNodeToExpr(child, nsMgr)}");
                    break;
                case "eval":
                    if (child.ChildNodes.Count > 0)
                        lines.Add($"{pad}{MathNodeToExpr(child.ChildNodes[0]!, nsMgr)}");
                    break;
                case "program":
                    // Nested program — recurse
                    ConvertProgramLines(child, nsMgr, lines, indent);
                    break;
            }
        }
    }
}
