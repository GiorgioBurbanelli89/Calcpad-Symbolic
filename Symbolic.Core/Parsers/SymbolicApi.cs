using System.Linq;
using System.Text.RegularExpressions;

namespace Calcpad.Core
{
    // API pública y mínima del motor simbólico, para que OTRAS apps (p.ej. el editor
    // Hekatan Sheet) lo consuman sin el bundle HTML de 5 MB ni los tags de render.
    //
    // Uso:
    //   Calcpad.Core.SymbolicApi.IsOp("solve(...)")  -> ¿es operación simbólica?
    //   Calcpad.Core.SymbolicApi.Eval("solve(A = pi*r^2; r)")  -> "r = -√(A/π); r = √(A/π)"
    //
    // Operaciones: solve(despeje), integrate, diff, limit, series, simplify, expand,
    // factor, sum, product, parfrac, coeffs, collect, fourier, invfourier, combine,
    // confrac, assume, laplace/ilaplace, det/inv/eigen, grad/div/curl/laplacian, ode...
    public static class SymbolicApi
    {
        // ¿La cadena es una operación simbólica reconocida?
        public static bool IsOp(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;
            int p = command.IndexOf('(');
            var op = (p < 0 ? command : command[..p]).Trim();
            return SymbolicProcessor.IsSymbolicOp(op);
        }

        // Evalúa una operación simbólica y devuelve el RESULTADO en texto plano.
        // Devuelve "ERR: ..." ante error.
        public static string Eval(string command)
        {
            var r = SymbolicProcessor.Process(command);
            if (r.Error != null) return "ERR: " + r.Error;
            if (r.Parts == null || r.Parts.Length == 0) return "";

            var last = r.Parts[^1] ?? "";

            // solve → "var = v1; var = v2"   (tag interno \x01SOLVE:var|v1|v2)
            const string SOLVE = "\x01SOLVE:";
            if (last.StartsWith(SOLVE))
            {
                var seg = last[SOLVE.Length..].Split('|');
                if (seg.Length >= 2)
                    return string.Join("; ", seg.Skip(1).Select(v => $"{seg[0]} = {v}"));
            }

            // quitar cualquier otro tag de render (\x01NARY:, \x01HTML:, etc.)
            return Regex.Replace(last, "\x01[A-Z_]+:", "").Trim();
        }
    }
}
