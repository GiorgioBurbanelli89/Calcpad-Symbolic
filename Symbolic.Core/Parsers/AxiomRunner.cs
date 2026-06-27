using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Calcpad.Core
{
    // Motor simbólico vía Axiom/FriCAS (AXIOMSys.exe).
    //
    // FriCAS 1.3.2 (fork open-source de Axiom, licencia BSD) viene embebido en la
    // instalación de PTC Mathcad Prime — y es el MISMO motor simbólico que usa
    // Mathcad (mctranslator.dll → symeval_axiom). Es especialmente fuerte en
    // INTEGRACIÓN simbólica (algoritmo de Risch, funciones especiales tipo erf).
    //
    // Se maneja headless por stdin/stdout. Para obtener salida lineal parseable
    // se usa `unparse(<expr>::InputForm)`, que devuelve un String en sintaxis de
    // entrada (p.ej. "(1/3)*x^3"). Para `solve`, que devuelve List(Equation) y no
    // convierte directo a InputForm, se mapea el lado derecho de cada ecuación.
    internal static class AxiomRunner
    {
        private const int DefaultTimeoutMs = 30000;   // FriCAS arranca lento (carga imagen Lisp)

        private static string _exe;
        private static string _axiomDir;
        private static bool _searched;
        private static readonly object _lock = new object();

        internal static bool IsAvailable()
        {
            Resolve();
            return !string.IsNullOrEmpty(_exe);
        }

        private static void Resolve()
        {
            if (_searched) return;
            lock (_lock)
            {
                if (_searched) return;
                _searched = true;
                var candidates = new System.Collections.Generic.List<string>();
                foreach (var pf in new[] { @"C:\Program Files\PTC", @"C:\Program Files (x86)\PTC" })
                {
                    if (!Directory.Exists(pf)) continue;
                    try
                    {
                        foreach (var prime in Directory.EnumerateDirectories(pf, "Mathcad Prime*"))
                            candidates.Add(Path.Combine(prime, "axiom", "win_nt", "x86e_win64", "AXIOMSys.exe"));
                    }
                    catch { }
                }
                // FriCAS instalado aparte (si algún día se desacopla de Mathcad)
                candidates.Add(@"C:\Program Files\FriCAS\AXIOMSys.exe");
                foreach (var c in candidates)
                    if (File.Exists(c)) { _exe = c; _axiomDir = Path.GetDirectoryName(c); return; }
            }
        }

        // ---- operaciones (FriCAS es especialmente bueno en integración) ----

        internal static (bool ok, string output) Integrate(string expr, string var, int t = DefaultTimeoutMs)
            => Scalar($"integrate({expr},{var})", t);

        internal static (bool ok, string output) IntegrateDefinite(string expr, string var, string lo, string hi, int t = DefaultTimeoutMs)
            => Scalar($"integrate({expr},{var}={lo}..{hi})", t);

        internal static (bool ok, string output) Diff(string expr, string var, int order = 1, int t = DefaultTimeoutMs)
            => Scalar($"differentiate({expr},{var},{order})", t);

        internal static (bool ok, string output) Limit(string expr, string var, string point, int t = DefaultTimeoutMs)
            => Scalar($"limit({expr},{var}={point})", t);

        internal static (bool ok, string output) Factor(string expr, int t = DefaultTimeoutMs)
            => Scalar($"factor({expr})", t);

        internal static (bool ok, string output) Simplify(string expr, int t = DefaultTimeoutMs)
            => Scalar($"simplify({expr})", t);

        // solve → lista de valores del lado derecho de cada solución, p.ej. ["3","2"]
        internal static (bool ok, string output) Solve(string equation, string var, int t = DefaultTimeoutMs)
            => Run($"[unparse((rhs e)::InputForm) for e in solve({equation},{var})]", t, isList: true);

        // Escotilla: ejecuta unparse((<call>)::InputForm)
        internal static (bool ok, string output) Eval(string call, int t = DefaultTimeoutMs) => Scalar(call, t);

        private static (bool ok, string output) Scalar(string call, int t)
            => Run($"unparse(({call})::InputForm)", t, isList: false);

        // Envía UN comando a FriCAS y devuelve el string lineal resultante.
        private static (bool ok, string output) Run(string command, int timeoutMs, bool isList)
        {
            Resolve();
            if (string.IsNullOrEmpty(_exe)) return (false, "axiom/fricas not found");
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _exe,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };
                psi.EnvironmentVariables["AXIOM"] = _axiomDir;
                using var p = Process.Start(psi);
                if (p == null) return (false, "could not launch fricas");

                p.StandardInput.WriteLine(command);
                p.StandardInput.WriteLine(")quit");
                p.StandardInput.Flush();
                p.StandardInput.Close();

                var stdout = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                    return (false, $"fricas timeout after {timeoutMs / 1000}s");
                }
                var res = Extract(stdout, isList);
                return string.IsNullOrEmpty(res) ? (false, "fricas: no usable output") : (true, res);
            }
            catch (Exception ex) { return (false, $"fricas: {ex.Message}"); }
        }

        // La salida de FriCAS imprime el resultado como `(N)  "texto"` (envuelto en
        // varias líneas si es largo) seguido de una línea `Type: ...`. Reconstruimos
        // el bloque del resultado, lo colapsamos y extraemos la(s) cadena(s).
        private static string Extract(string stdout, bool isList)
        {
            if (string.IsNullOrWhiteSpace(stdout)) return null;
            if (stdout.Contains("Error", StringComparison.Ordinal) &&
                stdout.Contains("is not valid", StringComparison.OrdinalIgnoreCase)) return null;

            var lines = stdout.Replace("\r", "").Split('\n');
            var buf = new StringBuilder();
            bool capturing = false;
            foreach (var line in lines)
            {
                // Una línea de RESULTADO es "   (N)  ..." (indentada, sin "-->").
                // Las de PROMPT "(N) %-->" van al margen y SÍ contienen "-->": se ignoran.
                var m = Regex.Match(line, @"^\s*\((\d+)\)\s");
                if (m.Success && !line.Contains("-->"))
                {
                    capturing = true;
                    buf.Clear();
                    buf.Append(line.Substring(m.Index + m.Length).Trim());
                    continue;
                }
                if (capturing)
                {
                    var tl = line.TrimStart();
                    if (tl.StartsWith("Type:")) { capturing = false; continue; }
                    buf.Append(tl);
                }
            }
            var flat = buf.ToString();
            if (flat.Length == 0) return null;

            if (isList)
            {
                // ["3", "2"]  →  3; 2   (separador ';' para Calcpad)
                var items = Regex.Matches(flat, "\"([^\"]*)\"");
                if (items.Count == 0) return null;
                var vals = new System.Collections.Generic.List<string>();
                foreach (Match m in items) vals.Add(m.Groups[1].Value);
                return string.Join("; ", vals);
            }
            // "texto"  →  texto
            var one = Regex.Match(flat, "\"(.*)\"");
            return one.Success ? one.Groups[1].Value : null;
        }
    }
}
