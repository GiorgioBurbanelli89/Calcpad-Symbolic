# Calcpad-Symbolic

**Calcpad with Symbolic Math** — A fork of [CalcpadCE](https://github.com/imartincei/CalcpadCE) v7.6.2 with symbolic computation, Python integration, and Maxima CAS.

> Gift to the CalcpadCE community. Since Ned closed the original repository, I wanted to contribute something back.

**Author:** [Jorge Burbano](https://www.linkedin.com/in/jorge-burbano-037444113/) — Structural Engineer, Ecuador

---

## Fields of Application

- Structural engineering calculations with symbolic math
- FEM shape function derivation and verification
- Symbolic differentiation, integration, equation solving
- Laplace transforms and ODE solving for dynamics
- Tensor calculus for continuum mechanics
- Python/SymPy/OpenSeesPy integration for advanced analysis
- Maxima CAS for computer algebra

---

## What's New (vs CalcpadCE)

### `#sym` — Symbolic Math Engine (AngouriMath, native C#)

| Operation | Syntax | Example |
|-----------|--------|---------|
| Derivative | `#sym diff(expr; var)` | `#sym diff(x^2+3*x; x)` |
| Integral | `#sym integrate(expr; var)` | `#sym integrate(sin(x); x)` |
| Partial | `#sym pdiff(expr; var)` | `#sym pdiff(x^2*y; x)` |
| Simplify | `#sym simplify(expr)` | `#sym simplify((x^2-1)/(x-1))` |
| Expand | `#sym expand(expr)` | `#sym expand((a+b)^3)` |
| Factor | `#sym factor(expr)` | `#sym factor(x^2-5*x+6)` |
| Solve | `#sym solve(expr; var)` | `#sym solve(x^2-4; x)` |
| Limit | `#sym limit(expr; var; val)` | `#sym limit(sin(x)/x; x; 0)` |
| Taylor | `#sym series(expr; var; n)` | `#sym series(sin(x); x; 5)` |
| Evaluate | `#sym eval(expr)` | `#sym eval(sqrt(2))` |
| Substitute | `#sym subs(expr; var; val)` | `#sym subs(x^2; x; 3)` |

### Vector Calculus and FEM

| Operation | Syntax |
|-----------|--------|
| Gradient | `#sym gradient(f; x; y; z)` |
| Divergence | `#sym divergence(F1; F2; F3; x; y; z)` |
| Curl | `#sym curl(F1; F2; F3; x; y; z)` |
| Laplacian | `#sym laplacian(f; x; y; z)` |
| Jacobian | `#sym jacobian(f1; f2; x; y)` |
| Hessian | `#sym hessian(f; x; y)` |
| Strain tensor | `#sym strain(u; v; x; y)` |
| Stress tensor | `#sym stress(e11; e22; e12; E; nu)` |
| Voigt notation | `#sym voigt(a; b; c; d)` |
| Invariants | `#sym invariants(a; b; c; d)` |
| Dyadic product | `#sym dyadic(a1; a2; b1; b2)` |

### Laplace Transform and ODE

| Operation | Syntax |
|-----------|--------|
| Laplace | `#sym laplace(sin(t); t; s)` |
| Inverse Laplace | `#sym ilaplace(1/s; s; t)` |
| ODE 1st order | `#sym ode1(a)` |
| ODE 2nd order | `#sym ode2(a; b)` |
| Determinant | `#sym det(a; b; c; d)` |
| Inverse | `#sym inv(a; b; c; d)` |
| Eigenvalues | `#sym eigen(a; b; c; d)` |

### `#python` — Python Code Blocks

Execute Python code with output rendered using the CalcpadCE template. Works with SymPy, NumPy, OpenSeesPy, and any Python library.

### `#maxima` — Maxima CAS Blocks

Execute Maxima computer algebra system blocks. Supports diff, integrate, solve, laplace, ode2, taylor, eigenvalues, and all Maxima functions.

### `#pip` — Install Python Packages

Install Python packages directly from CalcpadCE.

### `#deq` — Symbolic Equations

Display-only double equality equations for reference formulas.

### `#function` — User-Defined Functions

Multi-line functions that return scalars, vectors, or matrices.

### Block Mode

All three CAS engines support multi-line blocks:
- `#sym` ... `#end sym`
- `#python` ... `#end python`
- `#maxima` ... `#end maxima`

### Template Rendering

All output uses the CalcpadCE native template: fractions with division lines, superscripts, matrices with brackets, symbols for integrals, partial derivatives, nabla, and Laplace transforms. No plain text output.

---

## Installation

### Requirements
- Windows 10/11 x64
- .NET Desktop Runtime 10.0
- Python 3.x (optional, for #python blocks)
- Maxima (optional, for #maxima blocks)

### Download
Download the latest release from the [Releases page](https://github.com/GiorgioBurbanelli89/Calcpad-Symbolic/releases/latest).

### Build from Source
```
git clone https://github.com/GiorgioBurbanelli89/Calcpad-Symbolic.git
cd Calcpad-Symbolic
dotnet build Symbolic.Wpf/Symbolic.Wpf.sln
dotnet run --project Symbolic.Wpf
```

---

## How it Works

1. Write expressions and symbolic operations in the left panel
2. Press F5 or click Run to calculate
3. Results appear in the right panel with formatted math

---

## Licensing and Terms of Use

MIT License — same as CalcpadCE.

Based on CalcpadCE by Ned Ganchovski (proektsoft.bg).
Fork maintained by imartincei (https://github.com/imartincei/CalcpadCE).
Symbolic extensions by Jorge Burbano (https://www.linkedin.com/in/jorge-burbano-037444113/).

---

## The Language

All original CalcpadCE features are preserved. See the CalcpadCE documentation for the base language reference.

### New Keywords Summary

| Keyword | Mode | Description |
|---------|------|-------------|
| `#sym expr` | Inline | Symbolic math (AngouriMath) |
| `#sym` ... `#end sym` | Block | Multi-line symbolic |
| `#python` ... `#end python` | Block | Python code execution |
| `#maxima` ... `#end maxima` | Block | Maxima CAS execution |
| `#pip install pkg` | Inline | Install Python packages |
| `#deq expr = expr` | Inline | Display-only equation |
| `#function` ... `#end function` | Block | User-defined function |
