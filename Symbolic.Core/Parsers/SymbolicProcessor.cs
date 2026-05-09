using System;
using System.Collections.Generic;
using System.Linq;
using AngouriMath;
using static AngouriMath.MathS;

namespace Calcpad.Core
{
    /// <summary>
    /// Wrapper around AngouriMath for symbolic math operations.
    /// Returns SymResult with expression parts for rendering.
    /// Parts prefixed with special tags are rendered by ParseKeywordSym using HtmlWriter.
    /// Plain parts are rendered through MathParser.Parse()+ToHtml().
    /// </summary>
    internal static class SymbolicProcessor
    {
        // Prefixes for special rendering (handled by ParseKeywordSym)
        internal const string TAG_NARY  = "\x01NARY:";      // ∫, ∑, ∏ with limits
        internal const string TAG_FRAC  = "\x01FRAC:";     // fraction a/b
        internal const string TAG_DERIV = "\x01DERIV:";    // d/dx (body) — fraction + body
        internal const string TAG_HTML   = "\x01HTML:";     // raw HTML passthrough w/ {CALCPAD:} markers
        internal const string TAG_TAYLOR = "\x01TAYLOR:";  // Taylor(body)|n
        internal const string TAG_SOLVE  = "\x01SOLVE:";   // solve result: var|val1|val2|...
        internal const string TAG_NABLA  = "\x01NABLA:";   // ∇ operator: type|body (grad/div/curl/lap)

        internal readonly struct SymResult
        {
            internal string[] Parts { get; }
            internal string Error { get; }
            internal bool IsError => Error != null;
            internal SymResult(params string[] parts) { Parts = parts; Error = null; }
            internal SymResult(string error, bool isError) { Parts = null; Error = error; }
        }

        // Does this raw expression look like one of the symbolic/CAS operations
        // dispatched by Process()? Used by cell-level renderers (e.g. #blk) to
        // decide whether to route a cell through the symbolic pipeline instead
        // of the standard numeric parser.
        internal static bool IsSymbolicOp(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;
            var s = command.TrimStart();
            var pi = s.IndexOf('(');
            if (pi <= 0) return false;
            var op = s[..pi].Trim().ToLowerInvariant();
            return op switch
            {
                "diff" or "derivative" or
                "integrate" or "integral" or "int" or
                "simplify" or "simp" or "expand" or "factor" or "solve" or
                "limit" or "lim" or "series" or "taylor" or
                "eval" or "subs" or "substitute" or
                "jacobian" or "jac" or
                "gradient" or "grad" or "nabla" or
                "divergence" or "div" or "curl" or "rot" or
                "laplacian" or "lap" or "hessian" or "hess" or
                "pdiff" or "partial" or
                "det" or "determinant" or "inv" or "inverse" or
                "eigen" or "eigenvalues" or "transp" or "transpose" or
                "laplace" or "lap_t" or "ilaplace" or "ilt" or
                "ode" or "ode1" or "ode2" or
                "strain" or "epsilon" or "stress" or "sigma" or
                "voigt" or "invariants" or "inv_t" or
                "dyadic" or "outer" => true,
                _ => false
            };
        }

        internal static SymResult Process(string command)
        {
            try
            {
                // Convert variable names like u1 to u_1 BEFORE AngouriMath parses them
                // Otherwise AngouriMath interprets u2 as u^2 (u squared)
                command = PreConvertVariableNames(command);

                // If command contains pdiff() inside a larger expression (matrix, assignment),
                // expand pdiff symbolically and return as Calcpad expression
                var trimmed = command.Trim();
                if (trimmed.Contains("pdiff(") && !trimmed.StartsWith("pdiff(", StringComparison.OrdinalIgnoreCase))
                {
                    // Try to expand pdiff symbolically into Calcpad expression
                    var symExpanded = ExpandPdiffSymbolic(trimmed);
                    if (symExpanded.HasValue)
                        return symExpanded.Value;
                    // Fallback to HTML notation
                    return ExpandPdiffNotation(trimmed);
                }

                var (op, args) = ParseCommand(trimmed);

                // AngouriMath's parser doesn't understand Calcpad's diff(x;y;n)
                // with semicolons. Resolve each nested diff(...) inside every
                // arg using safe in-process Differentiate. The top-level "diff"
                // op handler (Diff) still receives raw args and uses the proper
                // rendering path.
                if (!op.Equals("diff", StringComparison.OrdinalIgnoreCase) &&
                    !op.Equals("derivative", StringComparison.OrdinalIgnoreCase))
                {
                    for (int i = 0; i < args.Length; i++)
                        args[i] = ExpandNestedDiffCalls(args[i]);
                }

                return op.ToLowerInvariant() switch
                {
                    "diff" or "derivative" => Diff(args),
                    "integrate" or "integral" or "int" => Integrate(args),
                    "simplify" or "simp" => Simplify(args),
                    "expand" => Expand(args),
                    "factor" => Factor(args),
                    "solve" => Solve(args),
                    "limit" or "lim" => Limit(args),
                    "series" or "taylor" => Series(args),
                    "eval" => Eval(args),
                    "subs" or "substitute" => Substitute(args),
                    // Vector calculus / FEM operators
                    "jacobian" or "jac" or "J" => Jacobian(args),
                    "gradient" or "grad" or "nabla" => Gradient(args),
                    "divergence" or "div" => Divergence(args),
                    "curl" or "rot" => Curl(args),
                    "laplacian" or "lap" => Laplacian(args),
                    "hessian" or "hess" => Hessian(args),
                    "pdiff" or "partial" => PartialDiff(args),
                    // Matrix symbolic operations
                    "det" or "determinant" => Det(args),
                    "inv" or "inverse" => Inv(args),
                    "eigen" or "eigenvalues" => Eigen(args),
                    "transp" or "transpose" => Transp(args),
                    // Laplace transform (table-based, covers engineering cases)
                    "laplace" or "lap_t" => LaplaceTransform(args),
                    "ilaplace" or "ilt" => InverseLaplace(args),
                    // ODE solver (1st/2nd order linear, constant coefficients)
                    "ode" or "ode1" => ODE1(args),
                    "ode2" => ODE2(args),
                    // Tensor calculus
                    "strain" or "epsilon" => StrainTensor(args),
                    "stress" or "sigma" => StressTensor(args),
                    "voigt" => Voigt(args),
                    "invariants" or "inv_t" => TensorInvariants(args),
                    "dyadic" or "outer" => Dyadic(args),
                    _ => Expression(command.Trim())
                };
            }
            catch (Exception ex)
            {
                return new SymResult(ex.Message, true);
            }
        }

        // ─── Parsing ───────────────────────────────────────────────

        private static (string op, string[] args) ParseCommand(string s)
        {
            var pi = s.IndexOf('(');
            if (pi < 0) return (s, []);
            var op = s[..pi].Trim();
            var ci = FindClose(s, pi);
            return (op, Split(s[(pi + 1)..ci]));
        }

        private static int FindClose(string s, int open)
        {
            int d = 1;
            for (int i = open + 1; i < s.Length; i++)
            {
                if (s[i] == '(') d++;
                else if (s[i] == ')') { d--; if (d == 0) return i; }
            }
            return s.Length - 1;
        }

        private static string[] Split(string s)
        {
            var r = new List<string>();
            int d = 0, st = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c is '(' or '[' or '{') d++;
                else if (c is ')' or ']' or '}') d--;
                else if (c == ';' && d == 0) { r.Add(s[st..i].Trim()); st = i + 1; }
            }
            r.Add(s[st..].Trim());
            return r.ToArray();
        }

        // ─── Operations ────────────────────────────────────────────

        // Recursively resolve nested symbolic function calls in an expression
        // string and return the actual AngouriMath Entity. Allows callers like
        // diff(diff(f; x); x) to compute correctly instead of failing because
        // the inner `diff(...)` is not AngouriMath syntax.
        private static Entity ParseExpressionResolvingNested(string s)
        {
            var t = s.Trim();
            if (string.IsNullOrEmpty(t)) return t;

            // Cheap prefix dispatch — only inspect strings that look like a call
            var pi = t.IndexOf('(');
            if (pi <= 0 || !t.EndsWith(")"))
                return t;
            var op = t[..pi].Trim().ToLowerInvariant();
            var ci = FindClose(t, pi);
            if (ci != t.Length - 1)
                return t; // not a single wrapping call (e.g. "diff(...) + 1")
            var inner = t[(pi + 1)..ci];
            var args = Split(inner);

            switch (op)
            {
                case "diff":
                case "derivative":
                {
                    if (args.Length < 2) return t;
                    var e = ParseExpressionResolvingNested(args[0]);
                    var v = Var(args[1]);
                    int n = args.Length >= 3 && int.TryParse(args[2], out var nn) ? nn : 1;
                    Entity r = e;
                    for (int i = 0; i < n; i++) r = r.Differentiate(v);
                    return r.Simplify();
                }
                case "simplify":
                case "simp":
                    return args.Length >= 1
                        ? ParseExpressionResolvingNested(args[0]).Simplify()
                        : (Entity)t;
                case "expand":
                    return args.Length >= 1
                        ? ParseExpressionResolvingNested(args[0]).Expand()
                        : (Entity)t;
                case "factor":
                    return args.Length >= 1
                        ? ParseExpressionResolvingNested(args[0]).Factorize()
                        : (Entity)t;
                default:
                    return t;
            }
        }

        private static SymResult Diff(string[] a)
        {
            if (a.Length < 2) return Err("diff(expr; var)");
            // Resolve nested symbolic calls in the body so diff(diff(...)) works
            Entity e = ParseExpressionResolvingNested(a[0]);
            var v = Var(a[1]);
            int n = a.Length >= 3 && int.TryParse(a[2], out var nn) ? nn : 1;
            Entity r = e;
            for (int i = 0; i < n; i++) r = r.Differentiate(v);
            r = r.Simplify();

            // TAG_DERIV: num|den|body — renders as fraction(d/dx) followed by (body)
            string num, den;
            if (n == 1) { num = "d"; den = $"d{a[1]}"; }
            else { num = $"d^{n}"; den = $"d{a[1]}^{n}"; }

            // For nested calls, show the resolved inner entity instead of the
            // raw "diff(...)" string so the displayed body is readable.
            var bodyStr = a[0].TrimStart().StartsWith("diff(", StringComparison.OrdinalIgnoreCase)
                       || a[0].TrimStart().StartsWith("derivative(", StringComparison.OrdinalIgnoreCase)
                ? e.ToString()
                : a[0];
            return new SymResult($"{TAG_DERIV}{num}|{den}|{bodyStr}", TC(r));
        }

        private static SymResult Integrate(string[] a)
        {
            if (a.Length < 2) return Err("integrate(expr; var)");

            // AngouriMath's symbolic integrator can hang (infinite recursion) or
            // stack-overflow on certain polynomials with symbolic parameters (e.g.
            // Hermite shape functions with free length L). StackOverflowException
            // is uncatchable in .NET, so we isolate each call in a child process
            // (SymSandbox) with a hard timeout. On timeout/crash we surface a
            // readable error instead of killing the host (WPF / CLI).

            // Nested diff(...) calls inside a[0] have already been expanded in
            // Process() before dispatch — AngouriMath in the sandbox sees pure
            // polynomial expressions it can parse.
            //
            // Strategy: prefer Maxima when available — it handles polynomials
            // with free parameters (e.g. Hermite shape functions in L) that
            // AngouriMath gets stuck on, and is just as fast for trivial ones.
            // Fall back to the AngouriMath sandbox otherwise.
            bool preferMaxima = MaximaRunner.IsAvailable();
            if (a.Length >= 4)
            {
                // Definite: ∫_a^b f dx
                var label = $"{TAG_NARY}\u222B|{a[2]}|{a[3]}|{a[0]}*d{a[1]}";
                if (preferMaxima)
                {
                    var mx = MaximaRunner.IntegrateDefinite(a[0], a[1], a[2], a[3]);
                    if (mx.ok) return new SymResult(label, mx.output);
                }
                var defRes = SandboxRunner.Run(
                    new[] { "integrate_def", a[1], a[2], a[3] },
                    a[0]);
                if (defRes.ok) return new SymResult(label, defRes.output);
                return new SymResult(label, $"<span class=\"err\">integrate: {defRes.output}</span>");
            }
            else
            {
                var label = $"{TAG_NARY}\u222B||0|{a[0]}*d{a[1]}";
                string rs = null;
                string fallbackErr = null;
                if (preferMaxima)
                {
                    var mx = MaximaRunner.Integrate(a[0], a[1]);
                    if (mx.ok) rs = mx.output;
                    else fallbackErr = mx.output;
                }
                if (rs == null)
                {
                    var res = SandboxRunner.Run(new[] { "integrate", a[1] }, a[0]);
                    if (!res.ok)
                    {
                        var msg = preferMaxima
                            ? $"integrate: {res.output} (maxima: {fallbackErr})"
                            : $"integrate: {res.output}";
                        return new SymResult(label, $"<span class=\"err\">{msg}</span>");
                    }
                    rs = res.output;
                }
                // Integration constant 'C' — only append if not already present
                bool hasC = System.Text.RegularExpressions.Regex.IsMatch(rs, @"(?<![a-zA-Z])C(?![a-zA-Z])");
                if (!hasC) rs += " + C";
                return new SymResult(label, rs);
            }
        }

        // Replace each diff(expr; var; n) call with its computed derivative so
        // AngouriMath's parser (which only understands commas) doesn't choke on
        // semicolons. Differentiation itself is safe and always terminates.
        private static string ExpandNestedDiffCalls(string s)
        {
            if (s.IndexOf("diff(", StringComparison.Ordinal) < 0) return s;

            var sb = new System.Text.StringBuilder();
            int pos = 0;
            while (pos < s.Length)
            {
                int idx = s.IndexOf("diff(", pos, StringComparison.Ordinal);
                if (idx < 0) { sb.Append(s, pos, s.Length - pos); break; }
                // Reject matches that are part of a longer identifier (pdiff, Xdiff, …).
                if (idx > 0 && (char.IsLetterOrDigit(s[idx - 1]) || s[idx - 1] == '_'))
                {
                    sb.Append(s, pos, idx - pos + 5);
                    pos = idx + 5;
                    continue;
                }
                sb.Append(s, pos, idx - pos);

                int open = idx + 4; // index of '('
                int depth = 1, ci = open + 1;
                while (ci < s.Length && depth > 0)
                {
                    if (s[ci] == '(') depth++;
                    else if (s[ci] == ')') depth--;
                    if (depth == 0) break;
                    ci++;
                }
                if (ci >= s.Length)
                {
                    sb.Append(s, idx, s.Length - idx);
                    break;
                }
                var inner = s[(open + 1)..ci];
                var parts = Split(inner);
                if (parts.Length >= 2)
                {
                    try
                    {
                        // Inner expr may itself contain diff — expand recursively.
                        var innerExpr = ExpandNestedDiffCalls(parts[0].Trim());
                        Entity e = innerExpr;
                        var v = Var(parts[1].Trim());
                        int n = parts.Length >= 3 && int.TryParse(parts[2].Trim(), out var nn) ? nn : 1;
                        for (int i = 0; i < n; i++) e = e.Differentiate(v);
                        e = e.Simplify();
                        sb.Append('(').Append(TC(e)).Append(')');
                    }
                    catch
                    {
                        // Fall back to raw text if differentiation fails; sandbox
                        // will then surface a parse error for that line.
                        sb.Append(s, idx, ci - idx + 1);
                    }
                }
                else
                {
                    sb.Append(s, idx, ci - idx + 1);
                }
                pos = ci + 1;
            }
            return sb.ToString();
        }

        private static SymResult Simplify(string[] a)
        {
            if (a.Length < 1) return Err("simplify(expr)");
            return new SymResult(a[0], TC(((Entity)a[0]).Simplify()));
        }

        private static SymResult Expand(string[] a)
        {
            if (a.Length < 1) return Err("expand(expr)");
            return new SymResult(a[0], TC(((Entity)a[0]).Expand().Simplify()));
        }

        private static SymResult Factor(string[] a)
        {
            if (a.Length < 1) return Err("factor(expr)");
            return new SymResult(a[0], TC(((Entity)a[0]).Factorize().Simplify()));
        }

        private static SymResult Solve(string[] a)
        {
            if (a.Length < 2) return Err("solve(expr; var)");
            Entity e = a[0];
            var v = Var(a[1]);
            var sol = e.SolveEquation(v);

            // TAG_SOLVE: var|val1|val2|...
            var solStr = TC(sol);
            var vals = solStr.Trim('{', '}', ' ').Split(',');
            var solTag = $"{TAG_SOLVE}{a[1]}";
            foreach (var val in vals)
                solTag += "|" + val.Trim();

            // 3 parts: expression = 0 → solution set
            return new SymResult(a[0], "0", solTag);
        }

        private static SymResult Limit(string[] a)
        {
            if (a.Length < 3) return Err("limit(expr; var; valor)");
            Entity e = a[0];
            var v = Var(a[1]);
            Entity val = a[2];
            var r = e.Limit(v, val).Simplify();
            // TAG_HTML for "lim" label, then expression part rendered by parser, then result
            var limHtml = $"{TAG_HTML}<span style=\"font-size:1.1em\">lim</span><sub><var>{a[1]}</var>\u2192{a[2]}</sub>";
            return new SymResult(limHtml, a[0], TC(r));
        }

        private static SymResult Series(string[] a)
        {
            if (a.Length < 3) return Err("series(expr; var; n)");
            Entity e = a[0];
            var v = Var(a[1]);
            if (!int.TryParse(a[2], out var n)) n = 5;
            Entity sum = (Entity)0;
            Entity cur = e;
            long fac = 1;
            for (int k = 0; k <= n; k++)
            {
                if (k > 0) fac *= k;
                var c = cur.Substitute(v, 0).Simplify();
                sum = sum + c / (Entity)fac * MathS.Pow(v, (Entity)k);
                cur = cur.Differentiate(v);
            }
            var r = sum.Expand().Simplify();

            // Label: TAG_TAYLOR:body|n — renders as fraction-style Taylor(body) with subscript
            return new SymResult($"{TAG_TAYLOR}{a[0]}|{a[2]}", TC(r));
        }

        private static SymResult Eval(string[] a)
        {
            if (a.Length < 1) return Err("eval(expr)");
            return new SymResult(a[0], TC(((Entity)a[0]).EvalNumerical()));
        }

        private static SymResult Substitute(string[] a)
        {
            if (a.Length < 3) return Err("subs(expr; var; valor)");
            Entity e = a[0];
            var v = Var(a[1]);
            Entity val = a[2];
            var r = e.Substitute(v, val).Simplify();
            return new SymResult(a[0], TC(r));
        }

        /// <summary>
        /// Expand pdiff() inside a larger expression to ∂ notation for display.
        /// E.g., Jc(ξ; η) = [pdiff(xc(ξ; η); ξ); pdiff(yc(ξ; η); ξ)|...]
        /// Each pdiff(expr; var) becomes (∂/∂var)(expr) rendered with TAG_DERIV
        /// </summary>
        /// <summary>
        /// Expand pdiff() symbolically: replace each pdiff(expr; var) with the AngouriMath result.
        /// Returns SymResult with original expression and expanded result as two Parts.
        /// Returns null if any pdiff can't be computed symbolically.
        /// </summary>
        private static SymResult? ExpandPdiffSymbolic(string command)
        {
            // Replace each pdiff(expr; var) with the symbolic derivative
            var result = new System.Text.StringBuilder();
            int pos = 0;
            bool allResolved = true;

            while (pos < command.Length)
            {
                int idx = command.IndexOf("pdiff(", pos, StringComparison.Ordinal);
                if (idx < 0) { result.Append(command, pos, command.Length - pos); break; }
                result.Append(command, pos, idx - pos);

                int open = idx + 5;
                int depth = 1, ci = open + 1;
                while (ci < command.Length && depth > 0)
                {
                    if (command[ci] == '(') depth++;
                    else if (command[ci] == ')') depth--;
                    ci++;
                }
                var inner = command[(open + 1)..(ci - 1)];
                var parts = SplitSemicolon(inner);

                if (parts.Length >= 2)
                {
                    var expr = parts[0].Trim();
                    var variable = parts[1].Trim();

                    // Can only derive pure algebraic (no ; in expr = no user function args)
                    if (expr.IndexOf(';') >= 0) { allResolved = false; break; }

                    try
                    {
                        Entity e = expr;
                        var v = Var(variable);
                        var r = e.Differentiate(v).Simplify();
                        var rs = TC(r);
                        if (rs.Contains("Derivative", StringComparison.OrdinalIgnoreCase))
                        { allResolved = false; break; }
                        result.Append(rs);
                    }
                    catch { allResolved = false; break; }
                }
                else
                { allResolved = false; break; }

                pos = ci;
            }

            if (!allResolved) return null;

            // Return the expanded result as Calcpad expression
            // ParseInlineSym will render it with Calcpad's template
            var expanded = result.ToString();
            // Also build ∂ notation HTML for display
            var notationHtml = ExpandPdiffCalls(command);
            var matrixHtml = RenderMatrixHtml(notationHtml);
            // Return: TAG_HTML notation = Calcpad expression
            return new SymResult($"{TAG_HTML}{matrixHtml}", expanded);
        }

        private static SymResult ExpandPdiffNotation(string command)
        {
            // 1. Expand pdiff() to ∂ HTML fractions (display notation)
            var expanded = ExpandPdiffCalls(command);
            // 2. Render matrices [a; b|c; d] as HTML matrix tables
            var html = RenderMatrixHtml(expanded);

            // 3. Try to compute symbolic results for each pdiff
            var resultHtml = ComputePdiffResults(command);
            if (!string.IsNullOrEmpty(resultHtml))
                html += " = " + resultHtml;

            return new SymResult($"{TAG_HTML}{html}");
        }

        /// <summary>Compute symbolic/display results for pdiff calls in a matrix expression</summary>
        private static string? ComputePdiffResults(string command)
        {
            // Find the matrix part [...]  — either after = or standalone
            string matSource = command;
            int eqIdx = command.IndexOf('=');
            if (eqIdx >= 0)
                matSource = command[(eqIdx + 1)..].Trim();

            // Find [ that starts the matrix
            int matStart = matSource.IndexOf('[');
            if (matStart < 0) return null;

            // Find matching ]
            int depth = 0, endBrk = matStart;
            for (int i = matStart; i < matSource.Length; i++)
            {
                if (matSource[i] == '[') depth++;
                else if (matSource[i] == ']') { depth--; if (depth == 0) { endBrk = i; break; } }
            }
            var matContent = matSource[(matStart + 1)..endBrk];

            // Split into rows by |, cells by ;
            var rows = SplitByPipe(matContent);
            var resultRows = new System.Collections.Generic.List<string[]>();
            bool hasResults = false;

            foreach (var row in rows)
            {
                var cells = SplitBySemicolon(row);
                var resultCells = new string[cells.Length];
                for (int j = 0; j < cells.Length; j++)
                {
                    var cell = cells[j].Trim();
                    // Check if cell is pdiff(expr; var)
                    if (cell.StartsWith("pdiff(") && cell.EndsWith(")"))
                    {
                        var inner = cell[6..^1];
                        var parts = SplitSemicolon(inner);
                        if (parts.Length >= 2)
                        {
                            var expr = parts[0].Trim();
                            var variable = parts[1].Trim();
                            // Only try symbolic if expression has no ; (pure algebraic)
                            bool canDerive = expr.IndexOf(';') < 0;
                            if (canDerive)
                            {
                                try
                                {
                                    Entity e = expr;
                                    var v = Var(variable);
                                    var r = e.Differentiate(v).Simplify();
                                    var rs = TC(r);
                                    if (!rs.Contains("Derivative", StringComparison.OrdinalIgnoreCase))
                                    {
                                        resultCells[j] = rs;
                                        hasResults = true;
                                    }
                                }
                                catch { }
                            }
                        }
                        else
                            resultCells[j] = cell;
                    }
                    else
                    {
                        resultCells[j] = cell;
                        if (cell != "0") hasResults = true;
                    }
                }
                resultRows.Add(resultCells);
            }

            if (!hasResults) return null;

            // Build result matrix HTML
            var sb = new System.Text.StringBuilder("<span class=\"matrix\">");
            foreach (var row in resultRows)
            {
                sb.Append("<span class=\"tr\"><span class=\"td\"></span>");
                foreach (var cell in row)
                    sb.Append($"<span class=\"td\">{cell}</span>");
                sb.Append("<span class=\"td\"></span></span>");
            }
            sb.Append("</span>");
            return sb.ToString();
        }

        /// <summary>Replace pdiff(expr; var) with HTML fraction ∂/∂var · expr</summary>
        private static string ExpandPdiffCalls(string command)
        {
            var sb = new System.Text.StringBuilder();
            int pos = 0;
            while (pos < command.Length)
            {
                int idx = command.IndexOf("pdiff(", pos, StringComparison.Ordinal);
                if (idx < 0) { sb.Append(command, pos, command.Length - pos); break; }
                sb.Append(command, pos, idx - pos);

                int open = idx + 5;
                int depth = 1, ci = open + 1;
                while (ci < command.Length && depth > 0)
                {
                    if (command[ci] == '(') depth++;
                    else if (command[ci] == ')') depth--;
                    ci++;
                }
                var inner = command[(open + 1)..(ci - 1)];
                var parts = SplitSemicolon(inner);

                if (parts.Length >= 2)
                {
                    var expr = parts[0].Trim();
                    var variable = parts[1].Trim();
                    // Fraction ∂/∂var
                    sb.Append($"<span class=\"dvc\">\u2202<span class=\"dvl\"></span>\u2202<var>{variable}</var></span>");
                    // Body: function name with args — preserve ; in arguments
                    sb.Append($"\u2009{RenderExprHtml(expr)}");
                }
                else
                    sb.Append(command, idx, ci - idx);
                pos = ci;
            }
            return sb.ToString();
        }

        /// <summary>Render [a; b|c; d] as Calcpad HTML matrix</summary>
        private static string RenderMatrixHtml(string s)
        {
            // Find [...] at top level
            var sb = new System.Text.StringBuilder();
            int pos = 0;
            while (pos < s.Length)
            {
                int brk = -1;
                // Find [ that starts a matrix (not inside HTML tags)
                int tagDepth = 0;
                for (int i = pos; i < s.Length; i++)
                {
                    if (s[i] == '<') tagDepth++;
                    else if (s[i] == '>') tagDepth--;
                    else if (s[i] == '[' && tagDepth == 0) { brk = i; break; }
                }
                if (brk < 0) { sb.Append(s, pos, s.Length - pos); break; }
                sb.Append(s, pos, brk - pos);

                // Find matching ]
                int depth = 1, endBrk = brk + 1;
                int htmlTag = 0;
                while (endBrk < s.Length && depth > 0)
                {
                    if (s[endBrk] == '<') htmlTag++;
                    else if (s[endBrk] == '>') htmlTag--;
                    else if (htmlTag == 0)
                    {
                        if (s[endBrk] == '[') depth++;
                        else if (s[endBrk] == ']') depth--;
                    }
                    if (depth > 0) endBrk++;
                }

                var matContent = s[(brk + 1)..endBrk];
                // Split by | for rows (respecting HTML tags)
                var rows = SplitByPipe(matContent);

                sb.Append("<span class=\"matrix\">");
                foreach (var row in rows)
                {
                    sb.Append("<span class=\"tr\"><span class=\"td\"></span>");
                    var cols = SplitBySemicolon(row);
                    foreach (var col in cols)
                        sb.Append($"<span class=\"td\">{col.Trim()}</span>");
                    sb.Append("<span class=\"td\"></span></span>");
                }
                sb.Append("</span>");

                pos = endBrk + 1;
            }
            return sb.ToString();
        }

        /// <summary>Split string by | respecting HTML tags</summary>
        private static string[] SplitByPipe(string s)
        {
            var parts = new System.Collections.Generic.List<string>();
            int tag = 0, st = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '<') tag++;
                else if (s[i] == '>') tag--;
                else if (s[i] == '|' && tag == 0) { parts.Add(s[st..i]); st = i + 1; }
            }
            parts.Add(s[st..]);
            return parts.ToArray();
        }

        /// <summary>Split string by ; respecting HTML tags and parentheses</summary>
        private static string[] SplitBySemicolon(string s)
        {
            var parts = new System.Collections.Generic.List<string>();
            int tag = 0, paren = 0, st = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '<') tag++;
                else if (s[i] == '>') { if (tag > 0) tag--; }
                else if (tag == 0)
                {
                    if (s[i] == '(') paren++;
                    else if (s[i] == ')') paren--;
                    else if (s[i] == ';' && paren == 0) { parts.Add(s[st..i]); st = i + 1; }
                }
            }
            parts.Add(s[st..]);
            return parts.ToArray();
        }

        /// <summary>Split by semicolon respecting parens</summary>
        private static string[] SplitSemicolon(string s)
        {
            var r = new System.Collections.Generic.List<string>();
            int d = 0, st = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] is '(' or '[') d++;
                else if (s[i] is ')' or ']') d--;
                else if (s[i] == ';' && d == 0) { r.Add(s[st..i]); st = i + 1; }
            }
            r.Add(s[st..]);
            return r.ToArray();
        }

        /// <summary>Render a variable/function name as HTML var tags</summary>
        /// <summary>Render expression as HTML — wraps variable names in var tags, preserves ; and ()</summary>
        private static string RenderExprHtml(string expr)
        {
            // Parse function call: name(arg1; arg2)
            int pOpen = expr.IndexOf('(');
            if (pOpen > 0 && expr.EndsWith(")"))
            {
                var funcName = expr[..pOpen].Trim();
                var argsStr = expr[(pOpen + 1)..^1];
                var args = SplitSemicolon(argsStr);
                var htmlArgs = string.Join("; ", args.Select(a => $"<var>{a.Trim()}</var>"));
                return $"<var>{funcName}</var>({htmlArgs})";
            }
            // Simple variable or expression
            return $"<var>{expr}</var>";
        }

        private static SymResult Expression(string s)
        {
            // Expressions containing a top-level '=' are definitions / equations
            // (e.g. "N_1(x) = 1 - 3*(x/L)^2 + 2*(x/L)^3"). AngouriMath would
            // misparse these as implicit products (N_1 * x) and return a string
            // that Calcpad's math renderer can't lay out properly. Calcpad's own
            // MathParser, given the whole line with '=' in it, stuffs the RHS
            // into the subscript of the LHS. Split the two sides so the keyword
            // renderer formats each independently and joins them with " = ".
            int eq = FindTopLevelEquals(s);
            if (eq >= 0)
            {
                var lhs = s[..eq].Trim();
                var rhs = s[(eq + 1)..].Trim();
                if (lhs.Length > 0 && rhs.Length > 0)
                    return new SymResult(lhs, rhs);
                return new SymResult(s);
            }

            Entity e = s;
            var r = e.Simplify();
            var rs = TC(r);
            return rs == s ? new SymResult(s) : new SymResult(s, rs);
        }

        // Returns the index of the first top-level '=' that is not part of a
        // comparison operator (==, <=, >=, !=), or -1 if none.
        private static int FindTopLevelEquals(string s)
        {
            int d = 0;
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c is '(' or '[' or '{') d++;
                else if (c is ')' or ']' or '}') d--;
                else if (c == '=' && d == 0)
                {
                    if (i + 1 < s.Length && s[i + 1] == '=') { i++; continue; }
                    if (i > 0 && (s[i - 1] == '<' || s[i - 1] == '>' || s[i - 1] == '!' || s[i - 1] == '=')) continue;
                    return i;
                }
            }
            return -1;
        }

        // ─── Vector Calculus / FEM operators ───────────────────────

        /// <summary>
        /// Partial derivative: #sym pdiff(expr; var) or pdiff(expr; var; n)
        /// Uses ∂ instead of d
        /// </summary>
        private static SymResult PartialDiff(string[] a)
        {
            if (a.Length < 2) return Err("pdiff(expr; var) o pdiff(expr; var; n)");
            int n = a.Length >= 3 && int.TryParse(a[2], out var nn) ? nn : 1;

            string num, den;
            if (n == 1) { num = "\u2202"; den = $"\u2202{a[1]}"; }
            else { num = $"\u2202^{n}"; den = $"\u2202{a[1]}^{n}"; }

            // Try symbolic differentiation with AngouriMath
            try
            {
                Entity e = a[0];
                var v = Var(a[1]);
                Entity r = e;
                for (int i = 0; i < n; i++) r = r.Differentiate(v);
                r = r.Simplify();
                return new SymResult($"{TAG_DERIV}{num}|{den}|{a[0]}", TC(r));
            }
            catch
            {
                // Fallback: show ∂ notation only (for user-defined functions like xc(ξ;η))
                return new SymResult($"{TAG_DERIV}{num}|{den}|{a[0]}");
            }
        }

        /// <summary>
        /// Jacobian matrix: #sym jacobian(f1; f2; ...; fn; x1; x2; ...; xn)
        /// First half = functions, second half = variables
        /// Or: #sym jacobian(f1; f2; x; y) for 2D
        /// </summary>
        private static SymResult Jacobian(string[] a)
        {
            if (a.Length < 4) return Err("jacobian(f1; f2; x; y)");
            int nf = a.Length / 2;
            var funcs = a[..nf];
            var vars = a[nf..];

            // Build Jacobian matrix: J[i,j] = ∂fi/∂xj
            var rows = new List<string>();
            foreach (var f in funcs)
            {
                var cols = new List<string>();
                Entity fe = f;
                foreach (var vName in vars)
                {
                    var v = Var(vName);
                    var deriv = fe.Differentiate(v).Simplify();
                    cols.Add(TC(deriv));
                }
                rows.Add(string.Join("; ", cols));
            }
            var matStr = "[" + string.Join(" | ", rows) + "]";

            // Label: just "J"
            return new SymResult($"{TAG_HTML}<b>J</b>", matStr);
        }

        /// <summary>
        /// Gradient: #sym gradient(f; x; y; z) → ∇f = [∂f/∂x; ∂f/∂y; ∂f/∂z]
        /// </summary>
        private static SymResult Gradient(string[] a)
        {
            if (a.Length < 2) return Err("gradient(f; x; y; ...)");
            Entity fe = a[0];
            var vars = a[1..];

            var components = new List<string>();
            foreach (var vName in vars)
            {
                var v = Var(vName);
                var deriv = fe.Differentiate(v).Simplify();
                components.Add(TC(deriv));
            }
            var vecStr = "[" + string.Join("; ", components) + "]";

            // TAG_NABLA: grad|body
            return new SymResult($"{TAG_NABLA}grad|{a[0]}", vecStr);
        }

        /// <summary>
        /// Divergence: #sym divergence(F1; F2; F3; x; y; z) → ∇·F = ∂F1/∂x + ∂F2/∂y + ∂F3/∂z
        /// First half = components, second half = variables
        /// </summary>
        private static SymResult Divergence(string[] a)
        {
            if (a.Length < 4) return Err("divergence(F1; F2; x; y)");
            int nf = a.Length / 2;
            var comps = a[..nf];
            var vars = a[nf..];
            if (comps.Length != vars.Length) return Err("same number of components and variables");

            Entity sum = (Entity)0;
            for (int i = 0; i < comps.Length; i++)
            {
                Entity fi = comps[i];
                var v = Var(vars[i]);
                sum = sum + fi.Differentiate(v);
            }
            var r = sum.Simplify();

            return new SymResult($"{TAG_NABLA}div|F", TC(r));
        }

        /// <summary>
        /// Curl (3D): #sym curl(F1; F2; F3; x; y; z) → ∇×F
        /// </summary>
        private static SymResult Curl(string[] a)
        {
            if (a.Length != 6) return Err("curl(F1; F2; F3; x; y; z)");
            Entity f1 = a[0], f2 = a[1], f3 = a[2];
            var x = Var(a[3]); var y = Var(a[4]); var z = Var(a[5]);

            var c1 = (f3.Differentiate(y) - f2.Differentiate(z)).Simplify();
            var c2 = (f1.Differentiate(z) - f3.Differentiate(x)).Simplify();
            var c3 = (f2.Differentiate(x) - f1.Differentiate(y)).Simplify();

            var vecStr = $"[{TC(c1)}; {TC(c2)}; {TC(c3)}]";
            return new SymResult($"{TAG_NABLA}curl|F", vecStr);
        }

        /// <summary>
        /// Laplacian: #sym laplacian(f; x; y; z) → ∇²f = ∂²f/∂x² + ∂²f/∂y² + ∂²f/∂z²
        /// </summary>
        private static SymResult Laplacian(string[] a)
        {
            if (a.Length < 2) return Err("laplacian(f; x; y; ...)");
            Entity fe = a[0];
            var vars = a[1..];

            Entity sum = (Entity)0;
            foreach (var vName in vars)
            {
                var v = Var(vName);
                sum = sum + fe.Differentiate(v).Differentiate(v);
            }
            var r = sum.Simplify();

            return new SymResult($"{TAG_NABLA}lap|{a[0]}", TC(r));
        }

        /// <summary>
        /// Hessian matrix: #sym hessian(f; x; y; z) → H[i,j] = ∂²f/∂xi∂xj
        /// </summary>
        private static SymResult Hessian(string[] a)
        {
            if (a.Length < 2) return Err("hessian(f; x; y; ...)");
            Entity fe = a[0];
            var vars = a[1..];

            var rows = new List<string>();
            foreach (var vi in vars)
            {
                var cols = new List<string>();
                foreach (var vj in vars)
                {
                    var d = fe.Differentiate(Var(vi)).Differentiate(Var(vj)).Simplify();
                    cols.Add(TC(d));
                }
                rows.Add(string.Join("; ", cols));
            }
            var matStr = "[" + string.Join(" | ", rows) + "]";

            var label = $"{TAG_HTML}<b>H</b>{{CALCPAD:{a[0]}}}";
            return new SymResult(label, matStr);
        }

        // ─── Matrix symbolic operations ────────────────────────────

        /// <summary>Symbolic determinant of 2x2/3x3 matrix</summary>
        private static SymResult Det(string[] a)
        {
            // det([a;b | c;d]) or det(a;b;c;d) for 2x2
            if (a.Length == 4)
            {
                // 2x2: ad - bc
                Entity aa = a[0], bb = a[1], cc = a[2], dd = a[3];
                var r = (aa * dd - bb * cc).Simplify();
                var label = $"{TAG_HTML}<b>det</b>{{CALCPAD:[{a[0]}; {a[1]} | {a[2]}; {a[3]}]}}";
                return new SymResult(label, TC(r));
            }
            if (a.Length == 9)
            {
                // 3x3: Sarrus rule
                Entity a11 = a[0], a12 = a[1], a13 = a[2];
                Entity a21 = a[3], a22 = a[4], a23 = a[5];
                Entity a31 = a[6], a32 = a[7], a33 = a[8];
                var r = (a11*(a22*a33 - a23*a32) - a12*(a21*a33 - a23*a31) + a13*(a21*a32 - a22*a31)).Simplify();
                var label = $"{TAG_HTML}<b>det</b>(A)";
                return new SymResult(label, TC(r));
            }
            return Err("det(a;b;c;d) para 2x2 o det(a11;...;a33) para 3x3");
        }

        /// <summary>Symbolic inverse of 2x2 matrix</summary>
        private static SymResult Inv(string[] a)
        {
            if (a.Length != 4) return Err("inv(a;b;c;d) para matriz 2x2");
            Entity aa = a[0], bb = a[1], cc = a[2], dd = a[3];
            var det = (aa * dd - bb * cc).Simplify();
            // inv = 1/det * [d, -b; -c, a]
            var r11 = (dd / det).Simplify();
            var r12 = (-(bb) / det).Simplify();
            var r21 = (-(cc) / det).Simplify();
            var r22 = (aa / det).Simplify();
            var matStr = $"[{TC(r11)}; {TC(r12)} | {TC(r21)}; {TC(r22)}]";
            var label = $"{TAG_HTML}<b>inv</b>{{CALCPAD:[{a[0]}; {a[1]} | {a[2]}; {a[3]}]}}";
            return new SymResult(label, matStr);
        }

        /// <summary>Symbolic eigenvalues of 2x2 matrix</summary>
        private static SymResult Eigen(string[] a)
        {
            if (a.Length != 4) return Err("eigen(a;b;c;d) para matriz 2x2");
            Entity aa = a[0], bb = a[1], cc = a[2], dd = a[3];
            // eigenvalues of [a,b;c,d]: λ = (a+d)/2 ± sqrt((a-d)²/4 + bc)
            var trace = (aa + dd).Simplify();
            var det = (aa * dd - bb * cc).Simplify();
            var disc = (trace * trace - (Entity)4 * det).Simplify();
            var l1 = ((trace + MathS.Sqrt(disc)) / (Entity)2).Simplify();
            var l2 = ((trace - MathS.Sqrt(disc)) / (Entity)2).Simplify();
            var label = $"{TAG_HTML}\u03BB{{CALCPAD:[{a[0]}; {a[1]} | {a[2]}; {a[3]}]}}";
            return new SymResult(label, $"[{TC(l1)}; {TC(l2)}]");
        }

        /// <summary>Symbolic transpose — just swap rows/cols</summary>
        private static SymResult Transp(string[] a)
        {
            if (a.Length != 4) return Err("transp(a;b;c;d) para matriz 2x2");
            var matStr = $"[{a[0]}; {a[2]} | {a[1]}; {a[3]}]";
            var label = $"[{a[0]}; {a[1]} | {a[2]}; {a[3]}]^T";
            return new SymResult(label, matStr);
        }

        // ─── Laplace Transform (comprehensive table) ──────────────

        private static SymResult LaplaceTransform(string[] a)
        {
            if (a.Length < 3) return Err("laplace(f(t); t; s)");
            var t = a[1].Trim();
            var s = a[2].Trim();
            var f = a[0].Trim();

            var result = LaplaceTable(f, t, s);
            // Label: ℒ { f(t) } with curly braces and rendered expression
            var label = $"{TAG_HTML}<span style=\"font-size:120%;font-family:Georgia Pro Light,serif;color:#C080F0\">\u2112</span>" +
                $" &#123; {{CALCPAD:{f}}} &#125;";
            return new SymResult(label, result);
        }

        private static SymResult InverseLaplace(string[] a)
        {
            if (a.Length < 3) return Err("ilaplace(F(s); s; t)");
            var s = a[1].Trim();
            var t = a[2].Trim();
            var F = a[0].Trim();

            var result = ILaplaceTable(F, s, t);
            var label = $"{TAG_HTML}<span style=\"font-size:120%;font-family:Georgia Pro Light,serif;color:#C080F0\">\u2112<sup>-1</sup></span>" +
                $" &#123; {{CALCPAD:{F}}} &#125;";
            return new SymResult(label, result);
        }

        /// <summary>Laplace transform table — covers structural dynamics cases</summary>
        private static string LaplaceTable(string f, string t, string s)
        {
            // Normalize: remove spaces
            var fn = f.Replace(" ", "");

            // δ(t) → 1
            if (fn == "delta(" + t + ")") return "1";
            // 1 → 1/s
            if (fn == "1") return $"1/{s}";
            // t → 1/s²
            if (fn == t) return $"1/{s}^2";
            // t² → 2/s³
            if (fn == t + "^2") return $"2/{s}^3";
            // t³ → 6/s⁴
            if (fn == t + "^3") return $"6/{s}^4";
            // t^n → n!/s^(n+1)
            if (fn.StartsWith(t + "^"))
            {
                var nStr = fn[(t.Length + 1)..];
                if (int.TryParse(nStr, out int n))
                {
                    long fac = 1;
                    for (int i = 2; i <= n; i++) fac *= i;
                    return $"{fac}/{s}^{n + 1}";
                }
            }
            // e^(a*t) or exp(a*t) → 1/(s-a)
            if ((fn.StartsWith("e^(") || fn.StartsWith("exp(")) && !fn.Contains(")*"))
            {
                var inner = fn.StartsWith("exp(") ? fn[4..^1] : fn[3..^1];
                var coeff = ExtractCoeff(inner, t);
                if (coeff != null) return $"1/{SmS(s, coeff)}";
            }
            // sin(a*t) → a/(s²+a²)
            if (fn.StartsWith("sin(") && fn.EndsWith(")") && !fn.Contains("*sin"))
            {
                var coeff = ExtractCoeff(fn[4..^1], t);
                if (coeff != null)
                    return coeff == "1" ? $"1/({s}^2+1)" : $"{coeff}/({s}^2+{coeff}^2)";
            }
            // cos(a*t) → s/(s²+a²)
            if (fn.StartsWith("cos(") && fn.EndsWith(")") && !fn.Contains("*cos"))
            {
                var coeff = ExtractCoeff(fn[4..^1], t);
                if (coeff != null)
                    return coeff == "1" ? $"{s}/({s}^2+1)" : $"{s}/({s}^2+{coeff}^2)";
            }
            // sinh(a*t) → a/(s²-a²)
            if (fn.StartsWith("sinh(") && fn.EndsWith(")"))
            {
                var coeff = ExtractCoeff(fn[5..^1], t);
                if (coeff != null)
                    return coeff == "1" ? $"1/({s}^2-1)" : $"{coeff}/({s}^2-{coeff}^2)";
            }
            // cosh(a*t) → s/(s²-a²)
            if (fn.StartsWith("cosh(") && fn.EndsWith(")"))
            {
                var coeff = ExtractCoeff(fn[5..^1], t);
                if (coeff != null)
                    return coeff == "1" ? $"{s}/({s}^2-1)" : $"{s}/({s}^2-{coeff}^2)";
            }
            // t*sin(a*t) → 2*a*s/(s²+a²)²
            if (fn.StartsWith(t + "*sin(") && fn.EndsWith(")"))
            {
                var coeff = ExtractCoeff(fn[(t.Length + 5)..^1], t);
                if (coeff != null)
                    return coeff == "1" ? $"2*{s}/({s}^2+1)^2" : $"2*{coeff}*{s}/({s}^2+{coeff}^2)^2";
            }
            // t*cos(a*t) → (s²-a²)/(s²+a²)²
            if (fn.StartsWith(t + "*cos(") && fn.EndsWith(")"))
            {
                var coeff = ExtractCoeff(fn[(t.Length + 5)..^1], t);
                if (coeff != null)
                    return coeff == "1" ? $"({s}^2-1)/({s}^2+1)^2" : $"({s}^2-{coeff}^2)/({s}^2+{coeff}^2)^2";
            }
            // e^(a*t)*sin(w*t) or exp(-a*t)*sin(w*t) → w/((s-a)²+w²)
            if (fn.Contains(")*sin(") || fn.Contains(")*cos("))
            {
                // Split at )*sin( or )*cos(
                bool isSin = fn.Contains(")*sin(");
                var splitStr = isSin ? ")*sin(" : ")*cos(";
                var idx = fn.IndexOf(splitStr);
                if (idx > 0)
                {
                    var expPart = fn[..(idx + 1)]; // includes closing )
                    var trigArg = fn[(idx + splitStr.Length)..^1]; // inside sin/cos parens
                    // Parse exp part
                    string expInner = expPart.StartsWith("exp(") ? expPart[4..^1] :
                                      expPart.StartsWith("e^(") ? expPart[3..^1] : null;
                    if (expInner != null)
                    {
                        var a_coeff = ExtractCoeff(expInner, t);
                        var w_coeff = ExtractCoeff(trigArg, t);
                        if (a_coeff != null && w_coeff != null)
                        {
                            var sMa = SmS(s, a_coeff);
                            if (isSin)
                                return $"{w_coeff}/({sMa}^2+{w_coeff}^2)";
                            else
                                return $"{sMa}/({sMa}^2+{w_coeff}^2)";
                        }
                    }
                }
            }
            // t*e^(a*t) or t*exp(a*t) → 1/(s-a)²
            if (fn.StartsWith(t + "*e^(") || fn.StartsWith(t + "*exp("))
            {
                var expStart = fn.IndexOf("exp(") >= 0 ? fn.IndexOf("exp(") + 4 : fn.IndexOf("e^(") + 3;
                var coeff = ExtractCoeff(fn[expStart..^1], t);
                if (coeff != null) return $"1/{SmS(s, coeff)}^2";
            }

            return $"\u2112( {f} )"; // fallback
        }

        /// <summary>Inverse Laplace table</summary>
        private static string ILaplaceTable(string F, string s, string t)
        {
            var fn = F.Replace(" ", "");
            // 1/s → 1
            if (fn == $"1/{s}") return "1";
            // 1/s² → t
            if (fn == $"1/{s}^2") return t;
            // 1/(s-a) → e^(a*t)
            if (fn.StartsWith("1/(") && fn.EndsWith(")") && fn.Contains($"{s}-"))
            {
                var inner = fn[3..^1];
                var a_val = inner.Replace($"{s}-", "").Replace($"{s}+", "-");
                return $"e^({a_val}*{t})";
            }
            // 1/(s²+a²) → sin(a*t)/a
            if (fn.StartsWith($"1/({s}^2+") && fn.EndsWith(")"))
            {
                var a2 = fn[($"1/({s}^2+").Length..^1];
                // Try to find sqrt
                Entity a2e = a2;
                var a_val = MathS.Sqrt(a2e).Simplify();
                return $"sin({TC(a_val)}*{t})/{TC(a_val)}";
            }
            // s/(s²+a²) → cos(a*t)
            if (fn.StartsWith($"{s}/({s}^2+") && fn.EndsWith(")"))
            {
                var a2 = fn[($"{s}/({s}^2+").Length..^1];
                Entity a2e = a2;
                var a_val = MathS.Sqrt(a2e).Simplify();
                return $"cos({TC(a_val)}*{t})";
            }

            return $"\u2112\u207B\u00B9{{{F}}}"; // fallback
        }

        /// <summary>Format (s - coeff), handling double negatives: s-(-a) → (s+a)</summary>
        private static string SmS(string s, string coeff)
        {
            if (coeff.StartsWith("-"))
                return $"({s}+{coeff[1..]})";  // s - (-a) = s + a
            return $"({s}-{coeff})";
        }

        /// <summary>Extract coefficient: "a*t" → "a", "t" → "1", "-2*t" → "-2"</summary>
        private static string? ExtractCoeff(string expr, string tVar)
        {
            if (expr == tVar) return "1";
            if (expr == "-" + tVar) return "-1";
            if (expr.EndsWith("*" + tVar))
                return expr[..^(tVar.Length + 1)];
            return null;
        }

        // ─── ODE Solver (characteristic equation method) ──────────

        /// <summary>
        /// 1st order linear ODE: y' + a*y = 0
        /// #sym ode1(a) → y = C·e^(-a·x)
        /// #sym ode1(a; f(x)) → y = e^(-a·x)·(C + ∫f·e^(a·x)dx)
        /// </summary>
        private static SymResult ODE1(string[] a)
        {
            if (a.Length < 1) return Err("ode1(a) para y' + a·y = 0");
            Entity coeff = a[0];
            var negA = (-(coeff)).Simplify();

            if (a.Length == 1)
            {
                // Homogeneous: y' + a*y = 0 → y = C·e^(-a·x)
                var label = $"{TAG_HTML}<var>y</var>' + {{CALCPAD:{a[0]}}}·<var>y</var> = 0";
                var ns = TC(negA);
                var expStr = ns == "0" ? "C" : ns == "1" ? "C*e^x" : ns == "-1" ? "C*e^(-x)" : $"C*e^({ns}*x)";
                return new SymResult(label, expStr);
            }
            else
            {
                // Non-homogeneous: y' + a*y = f(x) → integrating factor method
                var label = $"{TAG_HTML}<var>y</var>' + {{CALCPAD:{a[0]}}}·<var>y</var> = {{CALCPAD:{a[1]}}}";
                // General solution shown symbolically
                return new SymResult(label, $"e^({TC(negA)}*x)*(C + $Integral(({a[1]})*e^({TC(coeff)}*x); x))");
            }
        }

        /// <summary>
        /// 2nd order linear ODE with constant coefficients:
        /// #sym ode2(a; b) → y'' + a·y' + b·y = 0
        /// Solves characteristic equation r² + a·r + b = 0
        /// </summary>
        private static SymResult ODE2(string[] a)
        {
            if (a.Length < 2) return Err("ode2(a; b) para y'' + a·y' + b·y = 0");
            Entity coefA = a[0];
            Entity coefB = a[1];

            var label = $"{TAG_HTML}<var>y</var>'' + {{CALCPAD:{a[0]}}}·<var>y</var>' + {{CALCPAD:{a[1]}}}·<var>y</var> = 0";

            // Discriminant: Δ = a² - 4b
            var disc = (coefA * coefA - (Entity)4 * coefB).Simplify();
            var discStr = disc.ToString();

            // α = -a/2 (real part), β = √|Δ|/2
            var alpha = (-(coefA) / (Entity)2).Simplify();
            var alphaStr = TC(alpha);

            // Helper: format e^(α·x) — omit if α=0, simplify α=1→x, α=-1→-x
            string ExpPart(string a_str)
            {
                if (a_str == "0") return "";
                if (a_str == "1") return "e^x*";
                if (a_str == "-1") return "e^(-x)*";
                return $"e^({a_str}*x)*";
            }

            // Try to evaluate discriminant numerically
            double discVal;
            try { discVal = (double)disc.EvalNumerical().RealPart; }
            catch { discVal = double.NaN; }

            if (!double.IsNaN(discVal) && Math.Abs(discVal) < 1e-10)
            {
                // Repeated root: Δ = 0 → y = (C₁ + C₂·x)·e^(α·x)
                var exp = ExpPart(alphaStr);
                if (string.IsNullOrEmpty(exp))
                    return new SymResult(label, "C_1 + C_2*x");
                return new SymResult(label, $"{exp}(C_1 + C_2*x)");
            }
            else if (!double.IsNaN(discVal) && discVal < 0)
            {
                // Complex roots: Δ < 0 → y = e^(α·x)·(C₁·cos(β·x) + C₂·sin(β·x))
                var beta = (MathS.Sqrt(-(disc)) / (Entity)2).Simplify();
                var betaStr = TC(beta);
                var exp = ExpPart(alphaStr);
                var trig = $"(C_1*cos({betaStr}*x) + C_2*sin({betaStr}*x))";
                if (string.IsNullOrEmpty(exp))
                    return new SymResult(label, trig);
                return new SymResult(label, $"{exp}{trig}");
            }
            else
            {
                // Real distinct roots: Δ > 0
                var sqrtD = MathS.Sqrt(disc).Simplify();
                var r1 = ((-(coefA) + sqrtD) / (Entity)2).Simplify();
                var r2 = ((-(coefA) - sqrtD) / (Entity)2).Simplify();
                var r1s = TC(r1);
                var r2s = TC(r2);

                string Exp1(string rs) =>
                    rs == "0" ? "C" :
                    rs == "1" ? "e^x" :
                    rs == "-1" ? "e^(-x)" :
                    $"e^({rs}*x)";

                return new SymResult(label, $"C_1*{Exp1(r1s)} + C_2*{Exp1(r2s)}");
            }
        }

        // ─── Tensor Calculus (for FEM / continuum mechanics) ──────

        /// <summary>
        /// Strain tensor from displacement field:
        /// #sym strain(u; v; x; y) → ε = [∂u/∂x, (∂u/∂y+∂v/∂x)/2; (∂u/∂y+∂v/∂x)/2, ∂v/∂y]
        /// #sym strain(u; v; w; x; y; z) → 3D strain tensor (6 components)
        /// </summary>
        private static SymResult StrainTensor(string[] a)
        {
            if (a.Length == 4)
            {
                // 2D: ε_ij = (∂ui/∂xj + ∂uj/∂xi) / 2
                Entity u = a[0], v = a[1];
                var x = Var(a[2]); var y = Var(a[3]);
                var e11 = u.Differentiate(x).Simplify();
                var e22 = v.Differentiate(y).Simplify();
                var e12 = ((u.Differentiate(y) + v.Differentiate(x)) / (Entity)2).Simplify();
                var matStr = $"[{TC(e11)}; {TC(e12)} | {TC(e12)}; {TC(e22)}]";
                var label = $"{TAG_HTML}<b>\u03B5</b>"; // ε
                return new SymResult(label, matStr);
            }
            if (a.Length == 6)
            {
                // 3D: 6 independent components
                Entity u = a[0], v = a[1], w = a[2];
                var x = Var(a[3]); var y = Var(a[4]); var z = Var(a[5]);
                var e11 = u.Differentiate(x).Simplify();
                var e22 = v.Differentiate(y).Simplify();
                var e33 = w.Differentiate(z).Simplify();
                var e12 = ((u.Differentiate(y) + v.Differentiate(x)) / (Entity)2).Simplify();
                var e13 = ((u.Differentiate(z) + w.Differentiate(x)) / (Entity)2).Simplify();
                var e23 = ((v.Differentiate(z) + w.Differentiate(y)) / (Entity)2).Simplify();
                var matStr = $"[{TC(e11)}; {TC(e12)}; {TC(e13)} | {TC(e12)}; {TC(e22)}; {TC(e23)} | {TC(e13)}; {TC(e23)}; {TC(e33)}]";
                var label = $"{TAG_HTML}<b>\u03B5</b>"; // ε
                return new SymResult(label, matStr);
            }
            return Err("strain(u; v; x; y) o strain(u; v; w; x; y; z)");
        }

        /// <summary>
        /// Stress tensor from strain (isotropic Hooke's law):
        /// #sym stress(e11; e22; e12; E; nu) → σ for plane stress
        /// </summary>
        private static SymResult StressTensor(string[] a)
        {
            if (a.Length == 5)
            {
                // Plane stress: σ = E/(1-ν²) · [1, ν, 0; ν, 1, 0; 0, 0, (1-ν)/2] · {ε11, ε22, 2·ε12}
                Entity e11 = a[0], e22 = a[1], e12 = a[2];
                Entity E = a[3], nu = a[4];
                var factor = (E / ((Entity)1 - nu * nu)).Simplify();
                var s11 = (factor * (e11 + nu * e22)).Simplify();
                var s22 = (factor * (nu * e11 + e22)).Simplify();
                var s12 = (factor * ((Entity)1 - nu) / (Entity)2 * (Entity)2 * e12).Simplify();
                var matStr = $"[{TC(s11)}; {TC(s12)} | {TC(s12)}; {TC(s22)}]";
                var label = $"{TAG_HTML}<b>\u03C3</b>"; // σ
                return new SymResult(label, matStr);
            }
            return Err("stress(ε₁₁; ε₂₂; ε₁₂; E; ν)");
        }

        /// <summary>
        /// Voigt notation: convert symmetric tensor to vector
        /// #sym voigt(a; b; c; d) → [a; d; b+c] (2D: εxx, εyy, γxy)
        /// </summary>
        private static SymResult Voigt(string[] a)
        {
            if (a.Length == 4)
            {
                // 2D: [σ11; σ22; σ12] → {σxx, σyy, τxy}
                return new SymResult($"[{a[0]}; {a[1]} | {a[2]}; {a[3]}]",
                    $"[{a[0]}; {a[3]}; {a[1]}]");
            }
            if (a.Length == 9)
            {
                // 3D: 3x3 → 6-vector {σxx, σyy, σzz, τyz, τxz, τxy}
                return new SymResult("[3x3]",
                    $"[{a[0]}; {a[4]}; {a[8]}; {a[5]}; {a[2]}; {a[1]}]");
            }
            return Err("voigt(a11; a12; a21; a22) o voigt(9 componentes 3x3)");
        }

        /// <summary>
        /// Tensor invariants: I₁ = tr(A), I₂ = (tr²(A) - tr(A²))/2, I₃ = det(A)
        /// #sym invariants(a; b; c; d) → [I₁; I₂; I₃] for 2x2
        /// </summary>
        private static SymResult TensorInvariants(string[] a)
        {
            if (a.Length == 4)
            {
                Entity aa = a[0], bb = a[1], cc = a[2], dd = a[3];
                var I1 = (aa + dd).Simplify();                        // trace
                var I2 = (aa * dd - bb * cc).Simplify();              // det (for 2x2, I2 = det)
                var label = $"{TAG_HTML}<b>I</b>{{CALCPAD:[{a[0]}; {a[1]} | {a[2]}; {a[3]}]}}";
                return new SymResult(label, $"[{TC(I1)}; {TC(I2)}]");
            }
            if (a.Length == 9)
            {
                Entity a11 = a[0], a12 = a[1], a13 = a[2];
                Entity a21 = a[3], a22 = a[4], a23 = a[5];
                Entity a31 = a[6], a32 = a[7], a33 = a[8];
                var I1 = (a11 + a22 + a33).Simplify();
                var I2 = (a11*a22 + a22*a33 + a11*a33 - a12*a21 - a23*a32 - a13*a31).Simplify();
                var I3 = (a11*(a22*a33 - a23*a32) - a12*(a21*a33 - a23*a31) + a13*(a21*a32 - a22*a31)).Simplify();
                var label = $"{TAG_HTML}<b>I</b>(A)";
                return new SymResult(label, $"[{TC(I1)}; {TC(I2)}; {TC(I3)}]");
            }
            return Err("invariants(a;b;c;d) para 2x2 o invariants(9 componentes) para 3x3");
        }

        /// <summary>
        /// Dyadic/outer product: a ⊗ b = matrix
        /// #sym dyadic(a1; a2; b1; b2) → [a1·b1, a1·b2; a2·b1, a2·b2]
        /// </summary>
        private static SymResult Dyadic(string[] a)
        {
            if (a.Length == 4)
            {
                Entity a1 = a[0], a2 = a[1], b1 = a[2], b2 = a[3];
                var m11 = (a1 * b1).Simplify();
                var m12 = (a1 * b2).Simplify();
                var m21 = (a2 * b1).Simplify();
                var m22 = (a2 * b2).Simplify();
                var label = $"[{a[0]}; {a[1]}] \u2297 [{a[2]}; {a[3]}]";
                return new SymResult(label, $"[{TC(m11)}; {TC(m12)} | {TC(m21)}; {TC(m22)}]");
            }
            return Err("dyadic(a1; a2; b1; b2)");
        }

        // ─── Helpers ───────────────────────────────────────────────

        /// <summary>To Calcpad syntax</summary>
        private static string TC(Entity e)
        {
            var s = e.ToString();
            var pi = s.IndexOf(" provided ", StringComparison.OrdinalIgnoreCase);
            if (pi > 0) s = s[..pi].Trim();
            s = s.Replace("pi", "\u03C0");
            return s;
        }

        /// <summary>
        /// In AngouriMath results, convert variable names like u1, u2 to u_1, u_2
        /// so they render as subscripts instead of superscripts.
        /// </summary>
        private static string ConvertDigitsInResult(string s)
        {
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                if (char.IsLetter(s[i]) && s[i] != '\u03C0') // skip π
                {
                    int start = i;
                    while (i < s.Length && char.IsLetter(s[i])) i++;
                    int digitStart = i;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                    // Check next char is not letter (to avoid converting "sin" etc.)
                    if (digitStart < i && digitStart > start)
                    {
                        var letters = s[start..digitStart];
                        var digits = s[digitStart..i];
                        // Don't convert math functions or single-char that could be confused
                        if (letters.Length <= 3 && !IsMathFunction(letters))
                        {
                            sb.Append(letters);
                            sb.Append('_');
                            sb.Append(digits);
                        }
                        else
                        {
                            sb.Append(s[start..i]);
                        }
                    }
                    else
                    {
                        sb.Append(s[start..i]);
                    }
                }
                else
                {
                    sb.Append(s[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static bool IsMathFunction(string name)
        {
            return name is "sin" or "cos" or "tan" or "log" or "ln" or "exp" or "abs"
                or "sqrt" or "cbrt" or "asin" or "acos" or "atan" or "sec" or "csc" or "cot"
                or "sinh" or "cosh" or "tanh" or "max" or "min"
                or "simplify" or "simp" or "expand" or "factor" or "solve" or "diff"
                or "integrate" or "integral" or "pdiff" or "partial"
                or "gradient" or "grad" or "nabla" or "divergence" or "div"
                or "curl" or "rot" or "laplacian" or "lap" or "limit" or "lim";
        }

        /// <summary>
        /// Convert variable names like u1, u2 to u_1, u_2 BEFORE sending to AngouriMath.
        /// This prevents AngouriMath from interpreting u2 as u^2.
        /// Skips math function names (sin, cos, diff, etc.) and inside function calls.
        /// </summary>
        private static string PreConvertVariableNames(string command)
        {
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < command.Length)
            {
                if (char.IsLetter(command[i]))
                {
                    int start = i;
                    while (i < command.Length && char.IsLetter(command[i])) i++;
                    int digitStart = i;
                    while (i < command.Length && char.IsDigit(command[i])) i++;

                    var letters = command[start..digitStart];
                    var digits = command[digitStart..i];

                    if (digits.Length > 0 && !IsMathFunction(letters) && letters.Length <= 4)
                    {
                        // Convert u2 → u_2, Le3 → Le_3, etc.
                        sb.Append(letters);
                        sb.Append('_');
                        sb.Append(digits);
                    }
                    else
                    {
                        sb.Append(command[start..i]);
                    }
                }
                else
                {
                    sb.Append(command[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static SymResult Err(string u) => new($"#sym: se requiere {u}", true);
    }
}
