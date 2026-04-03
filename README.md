# Calcpad-Symbolic

**Calcpad with Symbolic Math** — A fork of [CalcpadCE](https://github.com/imartincei/CalcpadCE) v7.6.2 with symbolic computation, Python integration, and Maxima CAS.

Calcpad-Symbolic extends CalcpadCE with three Computer Algebra System (CAS) engines, adding symbolic differentiation, integration, equation solving, Laplace transforms, ODE solving, tensor calculus, and FEM operators — all rendered with the native CalcpadCE template (fractions, superscripts, matrices, mathematical symbols).

> Gift to the CalcpadCE community. Since Ned closed the original repository, I wanted to contribute something useful.

**Author:** [Jorge Burbano](https://www.linkedin.com/in/jorge-burbano-037444113/) — Structural Engineer, Ecuador

---

## Fields of Application

- **Structural engineering** — symbolic derivation of stiffness matrices, shape functions, FEM formulations
- **Dynamics** — Laplace transforms, ODE solving, modal analysis
- **Continuum mechanics** — strain/stress tensors, Voigt notation, invariants, Jacobians
- **Mathematics** — derivatives, integrals, series, limits, equation solving
- **Education** — step-by-step symbolic derivations with formatted output
- **Verification** — Python/OpenSeesPy integration for FEM validation

---

## Installation

Requires a 64-bit computer with Windows 10/11 and [Microsoft .NET Desktop Runtime 10.0](https://dotnet.microsoft.com/download/dotnet/10.0).

Optional:
- [Python 3.x](https://www.python.org/) — for `#python` blocks
- [Maxima](https://maxima.sourceforge.io/) — for `#maxima` blocks

### Download

Download the latest portable release (no installer needed):

**[Calcpad-Symbolic-win-x64.zip](https://github.com/GiorgioBurbanelli89/Calcpad-Symbolic/releases/latest)**

### Build from Source

```
git clone https://github.com/GiorgioBurbanelli89/Calcpad-Symbolic.git
cd Calcpad-Symbolic
dotnet build Symbolic.Wpf/Symbolic.Wpf.sln
dotnet run --project Symbolic.Wpf
```

---

## Licensing and Terms of Use

Copyright (c) 2025 Ned Ganchovski (original CalcpadCE), Jorge Burbano (symbolic extensions)

MIT License. See LICENSE file for details.

Based on **CalcpadCE** by Ned Ganchovski ([proektsoft.bg](https://proektsoft.bg)).
Fork maintained by [imartincei](https://github.com/imartincei/CalcpadCE).
Symbolic extensions by [Jorge Burbano](https://www.linkedin.com/in/jorge-burbano-037444113/).

### Acknowledgments

- [AngouriMath](https://github.com/asc-community/AngouriMath) — symbolic math engine for .NET
- [Maxima](https://maxima.sourceforge.io/) — computer algebra system
- Original CalcpadCE acknowledgments apply (icons8, wkhtmltopdf, font families)

---

## How it Works

1. Write expressions, symbolic operations, and code blocks in the **left panel**
2. Press **F5** or click **Run** to calculate
3. Results appear in the **right panel** with formatted math (fractions, superscripts, matrices, symbols)

---

## New Features (Symbolic Extensions)

All original CalcpadCE features are preserved. The following keywords are new:

### `#sym` — Symbolic Math (AngouriMath)

Native C# symbolic computation using AngouriMath. No external process needed.

**Inline mode** — one expression per line:
```
#sym diff(x^2 + 3*x; x)
```

**Block mode** — multiple expressions:
```
#sym
diff(x^2 + 3*x; x)
integrate(sin(x); x)
solve(x^2 - 4; x)
#end sym
```

#### Calculus

**diff**(expr; var) — derivative of expr with respect to var:
```
#sym diff(x^3 + 2*x; x)              → 3x² + 2
#sym diff(sin(x)*cos(x); x)          → cos(2x)
#sym diff(x^3 - 3*x^2 + 2*x; x; 2)  → 6x - 6  (second derivative)
```

**integrate**(expr; var) — indefinite integral:
```
#sym integrate(x^2; x)               → x³/3 + C
#sym integrate(sin(x); x)            → -cos(x) + C
```

**integrate**(expr; var; a; b) — definite integral:
```
#sym integrate(x^2; x; 0; 1)         → 1/3
#sym integrate(sin(x); x; 0; pi)     → 2
```

**limit**(expr; var; value) — limit:
```
#sym limit(sin(x)/x; x; 0)           → 1
```

**series**(expr; var; n) — Taylor series around 0 with n terms:
```
#sym series(sin(x); x; 5)            → x - x³/6 + x⁵/120
#sym series(e^x; x; 4)               → 1 + x + x²/2 + x³/6 + x⁴/24
```

#### Algebra

**simplify**(expr) — simplify expression:
```
#sym simplify((x^2 - 1)/(x - 1))    → x + 1
```

**expand**(expr) — expand expression:
```
#sym expand((a + b)^3)                → a³ + 3a²b + 3ab² + b³
```

**factor**(expr) — factorize:
```
#sym factor(x^2 - 5*x + 6)           → (x-2)(x-3)
```

**solve**(expr; var) — solve equation = 0:
```
#sym solve(x^2 - 4; x)               → {2, -2}
#sym solve(x^2 + 2*x - 3; x)         → {-3, 1}
```

**eval**(expr) — evaluate to numeric:
```
#sym eval(sqrt(2))                    → 1.4142135...
```

**subs**(expr; var; value) — substitute:
```
#sym subs(x^2 + 2*x + 1; x; 3)      → 16
```

#### Partial Derivatives

**pdiff**(expr; var) — partial derivative (uses symbol):
```
#sym pdiff(x^2*y + y^3; x)           → 2xy
#sym pdiff(x^2*y + y^3; y)           → x² + 3y²
#sym pdiff(x^2*y^2 + sin(x*y); x; 2) → (2 - sin(xy))y²
```

#### Vector Calculus

**gradient**(f; x; y; z) — gradient vector:
```
#sym gradient(x^2 + y^2 + x*y; x; y)  → [2x+y; x+2y]
```

**divergence**(F1; F2; F3; x; y; z) — divergence (first half = components, second half = variables):
```
#sym divergence(x^2; y^2; z^2; x; y; z)  → 2(x+y+z)
```

**curl**(F1; F2; F3; x; y; z) — curl (3D only):
```
#sym curl(y*z; x*z; x*y; x; y; z)    → [0; 0; 0]
```

**laplacian**(f; x; y; z) — Laplacian:
```
#sym laplacian(x^2 + y^2 + z^2; x; y; z)  → 6
```

#### FEM Operators

**jacobian**(f1; f2; x; y) — Jacobian matrix (first half = functions, second half = variables):
```
#sym jacobian(x^2; y^2; x; y)        → [2x, 0; 0, 2y]
```

**hessian**(f; x; y) — Hessian matrix of second derivatives:
```
#sym hessian(x^3 + x*y^2; x; y)      → [6x, 2y; 2y, 2x]
```

**strain**(u; v; x; y) — strain tensor from displacement field (2D):
```
#sym strain(x^2*y; x*y^2; x; y)      → [2xy, (x²+y²)/2; (x²+y²)/2, 2xy]
```

**strain**(u; v; w; x; y; z) — strain tensor 3D (6 components)

**stress**(e11; e22; e12; E; nu) — stress tensor via isotropic Hooke's law (plane stress):
```
#sym stress(0.001; 0.002; 0.0005; 200000; 0.3)
```

**voigt**(a; b; c; d) — convert symmetric 2x2 tensor to Voigt vector notation

**invariants**(a; b; c; d) — tensor invariants I1, I2 for 2x2; or I1, I2, I3 for 3x3 (9 args)

**dyadic**(a1; a2; b1; b2) — outer product:
```
#sym dyadic(1; 2; 3; 4)              → [3, 4; 6, 8]
```

#### Matrix Operations

**det**(a; b; c; d) — symbolic determinant 2x2:
```
#sym det(a; b; c; d)                  → ad - bc
```

**det**(a11; ...; a33) — symbolic determinant 3x3 (9 args)

**inv**(a; b; c; d) — symbolic inverse 2x2:
```
#sym inv(a; b; c; d)                  → [d/(ad-bc), -b/(ad-bc); -c/(ad-bc), a/(ad-bc)]
```

**eigen**(a; b; c; d) — symbolic eigenvalues 2x2:
```
#sym eigen(4; -2; -2; 4)             → [6; 2]
```

**transp**(a; b; c; d) — transpose 2x2

#### Laplace Transform

**laplace**(f; t; s) — Laplace transform (14+ common pairs):
```
#sym laplace(1; t; s)                 → 1/s
#sym laplace(t; t; s)                 → 1/s²
#sym laplace(t^2; t; s)              → 2/s³
#sym laplace(sin(t); t; s)           → 1/(s²+1)
#sym laplace(cos(w*t); t; s)         → s/(s²+w²)
#sym laplace(exp(-a*t); t; s)        → 1/(s+a)
#sym laplace(sinh(t); t; s)          → 1/(s²-1)
#sym laplace(cosh(t); t; s)          → s/(s²-1)
#sym laplace(exp(-a*t)*sin(w*t); t; s) → w/((s+a)²+w²)
#sym laplace(exp(-a*t)*cos(w*t); t; s) → (s+a)/((s+a)²+w²)
#sym laplace(t*sin(w*t); t; s)       → 2ws/(s²+w²)²
#sym laplace(t*exp(-a*t); t; s)      → 1/(s+a)²
```

**ilaplace**(F; s; t) — inverse Laplace (basic pairs):
```
#sym ilaplace(1/s; s; t)             → 1
#sym ilaplace(1/s^2; s; t)           → t
```

#### ODE Solver

**ode1**(a) — first order: y' + ay = 0:
```
#sym ode1(2)                          → C*e^(-2x)
#sym ode1(-1)                         → C*e^(x)
```

**ode2**(a; b) — second order: y'' + ay' + by = 0:
```
#sym ode2(5; 6)                       → C1*e^(-2x) + C2*e^(-3x)  (real distinct)
#sym ode2(4; 4)                       → e^(-2x)*(C1 + C2*x)       (repeated)
#sym ode2(0; 4)                       → C1*cos(2x) + C2*sin(2x)   (pure oscillation)
#sym ode2(2; 5)                       → e^(-x)*(C1*cos(2x) + C2*sin(2x))  (underdamped)
```

---

### `#deq` — Symbolic Display Equations

Display-only equations with double/triple equality. No computation performed — purely for showing reference formulas.

```
#deq f(x) = x^2 + 3*x + 1
#deq K = E*I/L^3*[12; 6*L; -12; 6*L | 6*L; 4*L^2; -6*L; 2*L^2 | -12; -6*L; 12; -6*L | 6*L; 2*L^2; -6*L; 4*L^2]
#deq N_1 = (1 - ξ)*(1 - η)/4
#deq ε = du/dx
```

---

### `#function` / `#end function` — User-Defined Functions

Multi-line functions with variable isolation. Parameters separated by semicolons.

```
#function FrameKe(E; A; L)
k = E*A/L
FrameKe = k*[1; -1 | -1; 1]
#end function

K = FrameKe(200000; 0.01; 3)
```

- The last line with `FunctionName = expr` defines the return value
- Variables inside the function are isolated (don't leak outside)
- Functions can return scalars, vectors, or matrices

---

### `#python` / `#end python` — Python Code Blocks

Execute Python code with output rendered using the CalcpadCE template.

```
#python
from sympy import symbols, diff, integrate, solve, sin
x = symbols('x')
print(f"diff(x^3) = {diff(x**3, x)}")
print(f"integrate(sin(x)) = {integrate(sin(x), x)}")
print(f"solve(x^2-4) = {solve(x**2 - 4, x)}")
#end python
```

Output rendering:
- `diff(...)` on the left side renders as d/dx fraction
- `integrate(...)` renders as integral symbol
- `solve(...)` renders as equation = 0
- `**` converts to superscripts, `*` to multiplication dot
- `[[a,b],[c,d]]` converts to CalcpadCE matrix notation
- Scientific notation `1.23e+04` converts to decimal

#### Variable Export

Export values from Python to CalcpadCE variables using `CALCPAD:` prefix:

```
#python
import numpy as np
K = np.array([[4, -2], [-2, 4]])
det_K = np.linalg.det(K)
print(f"CALCPAD:det_K={det_K}")
#end python

'The determinant from Python is:
det_K
```

#### OpenSeesPy Example

```
#python
import openseespy.opensees as ops
ops.wipe()
ops.model('basic', '-ndm', 2, '-ndf', 3)
ops.node(1, 0.0, 0.0)
ops.node(2, 5.0, 0.0)
ops.fix(1, 1, 1, 1)
ops.element('elasticBeamColumn', 1, 1, 2, 0.01, 200000.0, 8.333e-6, 1)
ops.timeSeries('Linear', 1)
ops.pattern('Plain', 1, 1)
ops.load(2, 0.0, -10.0, 0.0)
ops.system('BandSPD')
ops.numberer('RCM')
ops.constraints('Plain')
ops.integrator('LoadControl', 1.0)
ops.algorithm('Linear')
ops.analysis('Static')
ops.analyze(1)
uy = ops.nodeDisp(2, 2)
print(f"uy = {uy:.6e}")
print(f"CALCPAD:uy={uy}")
ops.wipe()
#end python
```

---

### `#maxima` / `#end maxima` — Maxima CAS Blocks

Execute Maxima computer algebra system code. Requires [Maxima](https://maxima.sourceforge.io/) installed.

```
#maxima
diff(x^2 + 3*x + 1, x);
integrate(sin(x), x);
solve(x^2 - 4, x);
laplace(sin(t), t, s);
ode2('diff(y,x,2) + 4*y = 0, y, x);
taylor(sin(x), x, 0, 7);
eigenvalues(matrix([a, b], [c, d]));
#end maxima
```

Lines ending with `;` produce output. Lines ending with `$` are silent.

---

### `#pip` — Install Python Packages

Install Python packages directly from CalcpadCE:

```
#pip install numpy sympy openseespy matplotlib
```

Only shows output for new installations (skips "already satisfied" messages).

---

## Syntax Highlighting and Autocomplete

All new keywords are registered in:
- **WPF Syntax Highlighter** — keywords colored in magenta
- **WPF Autocomplete** — suggestions appear when typing `#sym`, `#python`, etc.
- **Sublime Text** — completions file updated
- **Notepad++** — auto-complete XML updated

---

## Project Structure

```
Calcpad-Symbolic/
  Symbolic.Core/       Math engine + AngouriMath + SymbolicProcessor
  Symbolic.Wpf/        WPF desktop application
  Symbolic.Cli/        Command-line interface
  Symbolic.OpenXml/    Word/Excel export
  Symbolic.Tests/      Unit tests
  Symbolic.Server/     Web server
  Symbolic.Api/        Python API bindings
  Examples/            Example files
```

Key files:
- `Symbolic.Core/Parsers/SymbolicProcessor.cs` — all #sym operations
- `Symbolic.Core/Parsers/ExpressionParser/ExpressionParser.Keywords.cs` — keyword handlers (#sym, #python, #maxima, #pip, #deq, #function)
- `Symbolic.Core/Calcpad.Core.csproj` — AngouriMath NuGet reference
