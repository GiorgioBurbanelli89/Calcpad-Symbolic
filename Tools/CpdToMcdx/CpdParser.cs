using System.Text.RegularExpressions;

namespace CpdToMcdx;

/// <summary>Parse .cpd text into a list of Regions</summary>
static class CpdParser
{
    public static List<Region> Parse(string cpdText)
    {
        var regions = new List<Region>();
        var lines = cpdText.Split('\n');
        bool visible = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r').TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Directives
            if (line.TrimStart().StartsWith("#hide", StringComparison.OrdinalIgnoreCase))
            { visible = false; continue; }
            if (line.TrimStart().StartsWith("#show", StringComparison.OrdinalIgnoreCase))
            { visible = true; continue; }

            // Skip loops, conditionals (not convertible to MathCad)
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#for ") || trimmed.StartsWith("#loop") ||
                trimmed.StartsWith("#if ") || trimmed.StartsWith("#else") ||
                trimmed.StartsWith("#end if") || trimmed.StartsWith("#repeat") ||
                trimmed.StartsWith("#while ") || trimmed.StartsWith("#break"))
            {
                regions.Add(new Region(RegionType.Comment, $"' {line}"));
                continue;
            }

            // Display equations
            if (trimmed.StartsWith("#deq ", StringComparison.OrdinalIgnoreCase))
            {
                var expr = trimmed[5..].Trim();
                var (vn, ex, fa) = SplitAssignment(expr);
                regions.Add(new Region(RegionType.DisplayEq, expr)
                    { VarName = vn, Expression = ex, FuncArgs = fa });
                continue;
            }

            // Symbolic (#sym) — convert to comment
            if (trimmed.StartsWith("#sym", StringComparison.OrdinalIgnoreCase))
            {
                regions.Add(new Region(RegionType.Comment, $"' {line}"));
                continue;
            }

            // Python/Maxima blocks — skip
            if (trimmed.StartsWith("#python") || trimmed.StartsWith("#maxima") ||
                trimmed.StartsWith("#end python") || trimmed.StartsWith("#end maxima") ||
                trimmed.StartsWith("#pip ") || trimmed.StartsWith("#function") ||
                trimmed.StartsWith("#end function"))
            {
                regions.Add(new Region(RegionType.Comment, $"' {line}"));
                continue;
            }

            // Headings: "Title
            if (trimmed.StartsWith("\""))
            {
                var title = trimmed[1..].Trim();
                int level = 1;
                // "1. ... or "1.1 ...
                if (Regex.IsMatch(title, @"^\d+\.\d+")) level = 2;
                regions.Add(new Region(RegionType.Heading, title) { HeadingLevel = level });
                continue;
            }

            // Text lines: 'Text or '<html>
            if (trimmed.StartsWith("'"))
            {
                var text = trimmed[1..];
                // Handle inline expressions: 'text 'expr' text
                regions.Add(new Region(RegionType.Text, text));
                continue;
            }

            // Plot commands
            if (trimmed.StartsWith("$Plot{", StringComparison.OrdinalIgnoreCase))
            {
                regions.Add(new Region(RegionType.Plot, trimmed));
                continue;
            }
            if (trimmed.StartsWith("$Map{", StringComparison.OrdinalIgnoreCase))
            {
                regions.Add(new Region(RegionType.Map, trimmed));
                continue;
            }
            if (trimmed.StartsWith("$PlotMap{", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("$Table{", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("$Fem", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("$Mesh{", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("$Chart{", StringComparison.OrdinalIgnoreCase))
            {
                regions.Add(new Region(RegionType.Comment, $"' {trimmed}"));
                continue;
            }

            // Settings (PlotWidth, PlotHeight, etc.)
            if (trimmed.StartsWith("PlotWidth") || trimmed.StartsWith("PlotHeight") ||
                trimmed.StartsWith("PlotStep") || trimmed.StartsWith("PlotSVG") ||
                trimmed.StartsWith("PlotShadows"))
            {
                regions.Add(new Region(RegionType.Comment, $"' {trimmed}"));
                continue;
            }

            // Math expressions (everything else with =)
            // Handle multi-expression lines: a = 5','b = 10
            var parts = SplitMultiExpr(trimmed);
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (string.IsNullOrEmpty(p)) continue;

                if (p.Contains('='))
                {
                    var (vn, ex, fa) = SplitAssignment(p);
                    if (vn != null)
                    {
                        regions.Add(new Region(RegionType.Math, p)
                            { VarName = vn, Expression = ex, FuncArgs = fa });
                    }
                    else
                    {
                        // Expression without clear assignment
                        regions.Add(new Region(RegionType.Math, p) { Expression = p });
                    }
                }
                else
                {
                    // Standalone expression (evaluation)
                    regions.Add(new Region(RegionType.Math, p) { Expression = p });
                }
            }
        }

        return regions;
    }

    /// <summary>Split "a = expr" into (varName, expression, funcArgs?)</summary>
    static (string? varName, string? expression, string? funcArgs) SplitAssignment(string line)
    {
        // Find first = that's not inside brackets/parens
        int depth = 0;
        int eqPos = -1;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            else if (c == '=' && depth == 0 && (i == 0 || line[i - 1] is not ('≤' or '≥' or '!' or '<' or '>')))
            {
                eqPos = i;
                break;
            }
        }

        if (eqPos < 1) return (null, line, null);

        var left = line[..eqPos].Trim();
        var right = line[(eqPos + 1)..].Trim();

        // Check if left side is function: f(x;y)
        var funcMatch = Regex.Match(left, @"^(\w[\w.]*)\(([^)]*)\)$");
        if (funcMatch.Success)
        {
            return (funcMatch.Groups[1].Value, right, funcMatch.Groups[2].Value);
        }

        // Check indexed: M.(i; j)
        var idxMatch = Regex.Match(left, @"^(\w[\w.]*)\.\((.+)\)$");
        if (idxMatch.Success)
        {
            return (left, right, null); // Keep as-is
        }

        return (left, right, null);
    }

    /// <summary>Split multi-expression lines separated by ','</summary>
    static List<string> SplitMultiExpr(string line)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < line.Length - 2; i++)
        {
            char c = line[i];
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            else if (depth == 0 && i + 2 < line.Length && line[i] == '\'' && line[i + 1] == ',' && line[i + 2] == '\'')
            {
                result.Add(line[start..i]);
                start = i + 3;
                i += 2;
            }
        }
        result.Add(line[start..]);
        return result;
    }
}
