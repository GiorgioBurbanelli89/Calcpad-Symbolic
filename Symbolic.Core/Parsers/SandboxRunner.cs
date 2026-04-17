using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Calcpad.Core
{
    // Runs AngouriMath operations in a child process (SymSandbox.exe) so that
    // infinite recursion / stack overflow / hangs in the symbolic engine do not
    // crash the hosting CLI or WPF. Use for operations that are known to be
    // unreliable (currently: integrate).
    internal static class SandboxRunner
    {
        // Default cap: 10 seconds per call. Polynomial integrals that resolve
        // normally finish well under 1 s; anything past 10 s is almost certainly
        // stuck in a recursive integration-by-parts loop.
        private const int DefaultTimeoutMs = 10000;

        // UTF-8 without BOM: Encoding.UTF8 emits a BOM at the start of the
        // stream which AngouriMath's parser sees as an invalid token.
        private static readonly System.Text.UTF8Encoding Utf8NoBom =
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static string _exePath;
        private static readonly object _lock = new object();

        private static string ResolveExe()
        {
            if (_exePath != null) return _exePath;
            lock (_lock)
            {
                if (_exePath != null) return _exePath;
                // Look next to the hosting assembly first, then fall back to
                // AppContext.BaseDirectory. Both CLI and WPF copy SymSandbox.exe
                // into their output directory via ProjectReference.
                var candidates = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "SymSandbox.exe"),
                    Path.Combine(Path.GetDirectoryName(typeof(SandboxRunner).Assembly.Location) ?? "", "SymSandbox.exe"),
                };
                foreach (var c in candidates)
                {
                    if (!string.IsNullOrEmpty(c) && File.Exists(c))
                    {
                        _exePath = c;
                        return _exePath;
                    }
                }
                _exePath = "";
                return _exePath;
            }
        }

        // Returns (ok, output). When ok is false, output holds a readable error
        // suitable for embedding in the rendered document.
        internal static (bool ok, string output) Run(string[] args, string expression, int timeoutMs = DefaultTimeoutMs)
        {
            var exe = ResolveExe();
            if (string.IsNullOrEmpty(exe))
                return (false, "symbolic sandbox not found (SymSandbox.exe missing)");

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Utf8NoBom,
                StandardErrorEncoding = Utf8NoBom,
                StandardInputEncoding = Utf8NoBom,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            Process p;
            try { p = Process.Start(psi); }
            catch (Exception ex) { return (false, $"could not launch sandbox: {ex.Message}"); }
            if (p == null) return (false, "could not launch sandbox");

            try
            {
                p.StandardInput.WriteLine(expression);
                p.StandardInput.Close();

                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                    return (false, $"timeout after {timeoutMs / 1000}s — expression could not be integrated symbolically");
                }

                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();

                if (p.ExitCode == 0)
                    return (true, stdout.Trim());

                // Stack overflow in .NET manifests as a negative native exit code
                // without any stderr. Normal AngouriMath exceptions set exit 1
                // and write a message to stderr.
                if (string.IsNullOrWhiteSpace(stderr))
                    return (false, "symbolic engine crashed (stack overflow — expression too recursive)");
                return (false, stderr.Trim());
            }
            finally
            {
                try { p.Dispose(); } catch { }
            }
        }
    }
}
