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
