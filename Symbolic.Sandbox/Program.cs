using System;
using System.IO;
using AngouriMath;

namespace Calcpad.Symbolic.Sandbox
{
    // Minimal isolated runner for AngouriMath operations that can hang or stack-overflow.
    // Protocol (stdin/stdout):
    //   Args: <op> [<extra args...>]
    //   Stdin: each argument expression on its own line, in the order the op expects
    //   Stdout: result string (TC-normalized) on success
    //   Stderr + non-zero exit: error message on failure
    //
    // Supported ops:
    //   integrate <var>               stdin: expr                 → ∫ expr d(var) + C
    //   integrate_def <var> <lo> <hi> stdin: expr                 → ∫_lo^hi expr d(var)
    //   simplify                      stdin: expr                 → simplify(expr)
    //   expand                        stdin: expr                 → expand(expr)
    //   factor                        stdin: expr                 → factor(expr)
    //   diff <var> <n>                stdin: expr                 → d^n expr / d(var)^n
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                // Parent pipes UTF-8; match it here and also for any stderr output.
                Console.InputEncoding = System.Text.Encoding.UTF8;
                Console.OutputEncoding = System.Text.Encoding.UTF8;

                if (args.Length < 1)
                {
                    Console.Error.WriteLine("usage: SymSandbox <op> [args...]");
                    return 2;
                }

                var op = args[0].ToLowerInvariant();
                var exprLines = new System.Collections.Generic.List<string>();
                string line;
                while ((line = Console.In.ReadLine()) != null) exprLines.Add(line);

                string result = op switch
                {
                    "integrate" => Integrate(args, exprLines),
                    "integrate_def" => IntegrateDefinite(args, exprLines),
                    "simplify" => Simplify(exprLines),
                    "expand" => Expand(exprLines),
                    "factor" => Factor(exprLines),
                    "diff" => Diff(args, exprLines),
                    _ => throw new ArgumentException($"unknown op: {op}")
                };

                Console.Out.Write(result);
                return 0;
            }
            catch (Exception ex)
            {
                // Any exception from AngouriMath or argument parsing lands here.
                // StackOverflowException cannot be caught — that crashes the process
                // with a native exit code which the parent interprets as failure too.
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static string Integrate(string[] args, System.Collections.Generic.List<string> lines)
        {
            if (args.Length < 2) throw new ArgumentException("integrate <var>");
            if (lines.Count < 1) throw new ArgumentException("missing expression on stdin");
            Entity e = lines[0];
            var r = e.Integrate(args[1]).Simplify();
            return Normalize(r);
        }

        private static string IntegrateDefinite(string[] args, System.Collections.Generic.List<string> lines)
        {
            if (args.Length < 4) throw new ArgumentException("integrate_def <var> <lo> <hi>");
            if (lines.Count < 1) throw new ArgumentException("missing expression on stdin");
            Entity e = lines[0];
            var v = MathS.Var(args[1]);
            Entity lo = args[2], hi = args[3];
            var ad = e.Integrate(v).Simplify();
            var def = (ad.Substitute(v, hi) - ad.Substitute(v, lo)).Simplify();
            return Normalize(def);
        }

        private static string Simplify(System.Collections.Generic.List<string> lines)
        {
            if (lines.Count < 1) throw new ArgumentException("missing expression on stdin");
            Entity e = lines[0];
            return Normalize(e.Simplify());
        }

        private static string Expand(System.Collections.Generic.List<string> lines)
        {
            if (lines.Count < 1) throw new ArgumentException("missing expression on stdin");
            Entity e = lines[0];
            return Normalize(e.Expand().Simplify());
        }

        private static string Factor(System.Collections.Generic.List<string> lines)
        {
            if (lines.Count < 1) throw new ArgumentException("missing expression on stdin");
            Entity e = lines[0];
            return Normalize(e.Factorize().Simplify());
        }

        private static string Diff(string[] args, System.Collections.Generic.List<string> lines)
        {
            if (args.Length < 3) throw new ArgumentException("diff <var> <n>");
            if (lines.Count < 1) throw new ArgumentException("missing expression on stdin");
            int n = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
            Entity e = lines[0];
            var v = MathS.Var(args[1]);
            for (int i = 0; i < n; i++) e = e.Differentiate(v);
            return Normalize(e.Simplify());
        }

        // Mirrors SymbolicProcessor.TC: drop "provided ..." tail, π as unicode.
        private static string Normalize(Entity e)
        {
            var s = e.ToString();
            var pi = s.IndexOf(" provided ", StringComparison.OrdinalIgnoreCase);
            if (pi > 0) s = s[..pi].Trim();
            return s.Replace("pi", "\u03C0");
        }
    }
}
