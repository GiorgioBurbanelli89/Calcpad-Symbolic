using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Calcpad.Core
{
    // Fallback symbolic engine using Maxima CAS. Invoked when AngouriMath
    // (via SandboxRunner) cannot resolve an operation — typically symbolic
    // integration of polynomials that involve a free parameter (e.g. L in
    // Hermite shape functions). Maxima handles these trivially; AngouriMath
    // gets stuck in infinite integration-by-parts.
    internal static class MaximaRunner
    {
        private const int DefaultTimeoutMs = 15000;

        private static string _exe;
        private static bool _searched;
        private static readonly object _lock = new object();

        internal static bool IsAvailable()
        {
            ResolveExe();
            return !string.IsNullOrEmpty(_exe);
        }

        private static void ResolveExe()
        {
            if (_searched) return;
            lock (_lock)
            {
                if (_searched) return;
                _searched = true;
                // Hard-coded path used by the existing #maxima integration — keep
                // it as the first hit, then scan C:\maxima-<version>\bin\maxima.bat
                // so we don't break whenever Maxima is updated.
                var candidates = new System.Collections.Generic.List<string>
                {
                    "C:/maxima-5.48.1/bin/maxima.bat",
                };
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories("C:/", "maxima-*"))
                    {
                        var bat = Path.Combine(dir, "bin", "maxima.bat");
                        if (File.Exists(bat)) candidates.Add(bat);
                    }
                }
                catch { /* ignore — not critical */ }
                foreach (var c in candidates)
                {
                    if (File.Exists(c)) { _exe = c; return; }
                }
                // Last resort: assume it's on PATH.
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "maxima",
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    using var p = Process.Start(psi);
                    if (p != null && p.WaitForExit(3000) && p.ExitCode == 0)
                        _exe = "maxima";
                }
                catch { /* not on PATH either */ }
            }
        }

        // Indefinite integral: ∫ expr d(var).
        internal static (bool ok, string output) Integrate(string expression, string variable, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"integrate({expression},{variable})", timeoutMs);

        // Definite integral: ∫_lo^hi expr d(var).
        internal static (bool ok, string output) IntegrateDefinite(string expression, string variable, string lo, string hi, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"integrate({expression},{variable},{lo},{hi})", timeoutMs);

        // ---- operaciones simbólicas ampliadas (usan el Maxima ya instalado) ----

        // Derivada n-ésima.
        internal static (bool ok, string output) Diff(string expression, string variable, int order = 1, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"diff({expression},{variable},{order})", timeoutMs);

        // Resolver ecuación(es). `expr` puede ser "lhs=rhs" o una expresión (=0).
        // Devuelve la lista de soluciones de Maxima, p.ej. [x=2,x=3].
        internal static (bool ok, string output) Solve(string expression, string variable, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"solve([{expression}],[{variable}])", timeoutMs);

        // Factorizar.
        internal static (bool ok, string output) Factor(string expression, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"factor({expression})", timeoutMs);

        // Expandir.
        internal static (bool ok, string output) Expand(string expression, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"expand({expression})", timeoutMs);

        // Simplificación pesada: racional + radicales + trig.
        internal static (bool ok, string output) Simplify(string expression, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"fullratsimp(trigsimp(radcan({expression})))", timeoutMs);

        // Límite. `point` puede ser un valor, "inf", "minf"; `dir` opcional "plus"/"minus".
        internal static (bool ok, string output) Limit(string expression, string variable, string point, string dir = null, int timeoutMs = DefaultTimeoutMs)
            => RunScript(string.IsNullOrEmpty(dir)
                ? $"limit({expression},{variable},{point})"
                : $"limit({expression},{variable},{point},{dir})", timeoutMs);

        // Sumatoria en forma cerrada: Σ_{var=lo}^{hi} expr.
        internal static (bool ok, string output) Sum(string expression, string variable, string lo, string hi, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"ev(sum({expression},{variable},{lo},{hi}),simpsum,nouns)", timeoutMs);

        // Productoria en forma cerrada.
        internal static (bool ok, string output) Product(string expression, string variable, string lo, string hi, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"ev(product({expression},{variable},{lo},{hi}),simpproduct,nouns)", timeoutMs);

        // EDO general por ode2 (1er/2º orden). `eqn` en notación Maxima con 'diff,
        // p.ej. "'diff(y,x,2)+a*'diff(y,x)+b*y=f(x)". dep=y, indep=x.
        internal static (bool ok, string output) Ode2(string eqn, string dep, string indep, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"ode2({eqn},{dep},{indep})", timeoutMs);

        // EDO con condiciones iniciales por desolve (transformada de Laplace).
        // eqns y funcs en notación Maxima, p.ej. desolve('diff(y(x),x)=y(x), y(x)).
        internal static (bool ok, string output) Desolve(string eqns, string funcs, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"desolve([{eqns}],[{funcs}])", timeoutMs);

        // Transformada de Laplace y su inversa.
        internal static (bool ok, string output) Laplace(string expression, string t, string s, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"laplace({expression},{t},{s})", timeoutMs);
        internal static (bool ok, string output) InverseLaplace(string expression, string s, string t, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"ilt({expression},{s},{t})", timeoutMs);

        // Fracciones parciales (Mathcad: parfrac).
        internal static (bool ok, string output) Partfrac(string expression, string variable, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"partfrac({expression},{variable})", timeoutMs);

        // Coeficientes del polinomio en `variable`, de grado 0 al máximo → [c0,c1,...].
        internal static (bool ok, string output) Coeffs(string expression, string variable, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"block([p:expand({expression})],makelist(coeff(p,{variable},i),i,0,hipow(p,{variable})))", timeoutMs);

        // Recolectar términos respecto de `variable` (Mathcad: collect).
        internal static (bool ok, string output) Collect(string expression, string variable, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"collectterms(expand({expression}),{variable})", timeoutMs);

        // Combinar términos sobre denominador común (Mathcad: combine).
        internal static (bool ok, string output) Combine(string expression, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"combine({expression})", timeoutMs);

        // Fracción continua de un número (Mathcad: confrac) → [a0,a1,a2,...].
        internal static (bool ok, string output) ContinuedFraction(string expression, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"cf({expression})", timeoutMs);

        // Transformada de Fourier continua: f(t) → F(w) = ∫ f·e^{-i w t} dt.
        internal static (bool ok, string output) Fourier(string expression, string t, string w, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"assume({w}>0)$integrate(({expression})*%e^(-%i*{w}*{t}),{t},minf,inf)", timeoutMs);

        // Transformada inversa de Fourier: F(w) → f(t) = (1/2π) ∫ F·e^{i w t} dw.
        internal static (bool ok, string output) InvFourier(string expression, string w, string t, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"assume({t}>0)$(1/(2*%pi))*integrate(({expression})*%e^(%i*{w}*{t}),{w},minf,inf)", timeoutMs);

        // Despejar/resolver con assumption opcional (para evitar preguntas interactivas).
        internal static (bool ok, string output) SolveAssume(string equation, string variable, string assumption, int timeoutMs = DefaultTimeoutMs)
            => RunScript($"assume({assumption})$solve([{equation}],[{variable}])", timeoutMs);

        // Escotilla general: ejecuta cualquier llamada Maxima (sin el ';' final).
        internal static (bool ok, string output) Eval(string maximaCall, int timeoutMs = DefaultTimeoutMs)
            => RunScript(maximaCall, timeoutMs);

        private static (bool ok, string output) RunScript(string maximaCall, int timeoutMs)
        {
            ResolveExe();
            if (string.IsNullOrEmpty(_exe))
                return (false, "maxima not found");

            var tempFile = Path.Combine(Path.GetTempPath(),
                "calcpad_maxima_" + Guid.NewGuid().ToString("N")[..8] + ".mac");
            try
            {
                var script = "display2d:false$\n" + maximaCall + ";\n";
                File.WriteAllText(tempFile, script, new System.Text.UTF8Encoding(false));

                // Maxima treats backslashes as escape characters inside the
                // batch path, so C:\Users\... gets mangled to C:Users...
                // Forward slashes always work on Windows and Linux alike.
                var batchPath = tempFile.Replace('\\', '/');
                var psi = new ProcessStartInfo
                {
                    FileName = _exe,
                    Arguments = $"--very-quiet --batch \"{batchPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                };
                using var p = Process.Start(psi);
                if (p == null) return (false, "could not launch maxima");
                var stdout = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                    return (false, $"maxima timeout after {timeoutMs / 1000}s");
                }
                var result = ExtractResult(stdout, maximaCall);
                if (string.IsNullOrEmpty(result))
                    return (false, "maxima returned no usable output");
                return (true, result);
            }
            catch (Exception ex)
            {
                return (false, $"maxima: {ex.Message}");
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        // Maxima's --very-quiet --batch output echoes the script lines, the
        // display2d statement, and the expression we sent before printing the
        // result. We drop all the echo noise and return the last meaningful
        // non-empty line.
        private static string ExtractResult(string stdout, string echoedCall)
        {
            if (string.IsNullOrWhiteSpace(stdout)) return null;
            var lines = stdout.Split('\n');
            string lastGood = null;
            foreach (var raw in lines)
            {
                var t = raw.TrimEnd('\r').Trim();
                if (t.Length == 0) continue;
                if (t.StartsWith("batch(")) continue;
                if (t.StartsWith("read and")) continue;
                if (t.StartsWith("display2d")) continue;
                if (t.StartsWith("\"") && t.EndsWith("\"")) continue;   // file path echo
                if (t.StartsWith("Shell cwd was reset")) continue;
                if (t.Equals(echoedCall, StringComparison.Ordinal)) continue;
                // Error lines start with "incorrect syntax" / "Maxima encountered"
                if (t.StartsWith("incorrect syntax", StringComparison.OrdinalIgnoreCase)) return null;
                if (t.StartsWith("Maxima encountered", StringComparison.OrdinalIgnoreCase)) return null;
                lastGood = t;
            }
            return lastGood;
        }

        // Maxima returns results in standard infix but with `^` and `*`; the
        // host already handles those. AngouriMath-style `+ C` (integration
        // constant) is not added by Maxima, so callers append it themselves.
    }
}
