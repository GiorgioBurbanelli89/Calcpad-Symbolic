# Calcpad-Symbolic

**Calcpad with Symbolic Math, Python, Maxima, FEM Visualization** — A fork of [CalcpadCE](https://github.com/imartincei/CalcpadCE) v7.6.2.

Calcpad-Symbolic extends CalcpadCE with three CAS engines, interactive FEM visualization, enhanced unit operators, user-defined functions, and Python/OpenSeesPy integration. All output rendered with the native CalcpadCE template.

> Gift to the CalcpadCE community. Since Ned closed the original repository, I wanted to contribute something useful.

**Author:** [Jorge Burbano](https://www.linkedin.com/in/jorge-burbano-037444113/) — Structural Engineer, Ecuador

---

## What CalcpadCE Had (v7.6.2 by Ned Ganchovski)

- Real and complex numbers, vectors, matrices
- Units of measurement (SI, Imperial, USCS)
- Operators, built-in math functions (trig, log, etc.)
- Program flow (#if, #for, #while, #repeat)
- $Plot, $Map, $Find, $Root, $Integral, $Derivative, $Sum, $Product
- HTML report generation, Word/PDF export
- WPF desktop app with syntax highlighting

## What Calcpad-Symbolic Adds (NEW)

### 1. Symbolic Math Engine — `#sym` (AngouriMath C# native)

No external process needed. Inline or block mode.

**Calculus:** diff, integrate, limit, series, pdiff (partial derivative)
**Algebra:** simplify, expand, factor, solve, eval, subs
**Vector Calculus:** gradient (nabla), divergence, curl, laplacian, jacobian, hessian
**Laplace Transform:** 14+ common pairs, inverse Laplace
**ODE Solver:** 1st and 2nd order with constant coefficients
**Tensor Calculus:** strain, stress (Hooke), voigt, invariants, dyadic product
**Matrix Symbolic:** det, inv, eigen, transpose (2x2/3x3)

### 2. Python Integration — `#python` / `#end python`

Execute Python code blocks. Output rendered with CalcpadCE template.
Works with SymPy, NumPy, SciPy, OpenSeesPy, matplotlib, and any library.
Export variables to CalcpadCE: `print(f"CALCPAD:var={value}")`

### 3. Maxima CAS — `#maxima` / `#end maxima`

Execute Maxima computer algebra system. Supports diff, integrate, solve, laplace, ode2, taylor, eigenvalues, matrices. Lines with `;` produce output, `$` are silent.

### 4. Package Manager — `#pip install`

Install Python packages directly: `#pip install numpy sympy openseespy`

### 5. Display Equations — `#deq`

Show symbolic equations without computation. Double/triple equality for reference formulas.

### 6. User-Defined Functions — `#function` / `#end function`

Multi-line functions with parameter isolation. Return scalars, vectors, or matrices.

### 7. Interactive FEM Visualization — $Fem2D, $Fem3D, $Chart, $Mesh

**$Fem2D** — 2D finite element mesh visualization (Three.js interactive)
**$Fem3D** — 3D finite element visualization with rotation/zoom
**$Chart** — Interactive charts with customizable styling (light theme)
**$Mesh** — SVG mesh with supports, loads, color-mapped results

Powered by calcpad-viz TypeScript library (Three.js v0.170.0).

### 8. Unit Operators Enhanced — `&` and `|` with Arrays

**Original CalcpadCE:** `&` and `|` only worked with scalars.

**New — Adimensionalization with arrays:**
```
u = lsolve(K; F) & [cm; cm; rad]
```
Strips ALL units from the computation (adimensionalizes to SI), then stamps each element with the specified unit.

**New — Conversion with arrays:**
```
u | [cm; cm; rad]
```
Converts each element to the specified unit (compatible units required).

**Matrix unit arrays:**
```
K & [tonf/m; tonf | tonf; tonf*m]
```

### 9. New Matrix Functions

- **lsolve**(K; F) — solve linear system
- **clsolve**(K; F) — complex linear solver
- **slsolve**(K; F), **smsolve**(K; F) — sparse solvers
- **hprod** — Hadamard product (element-wise)
- **fprod** — Frobenius product (matrix inner product)
- **kprod** — Kronecker product
- **matrix_hp**, **diagonal_hp**, **column_hp** — high-precision variants

### 10. Vector Display — Vertical Column Format

Vectors now display vertically (as columns) like matrices, matching standard math notation.

### 11. FEM Graphics Library — Include/FEM_Graphics.cpd

Macro library with predefined SVG functions for FEM diagrams:
- Joints, elements, labels, boundary conditions (pin, fixed, roller)
- Loading (distributed, point force, moment)
- Color mapping (blue to green to yellow to red gradient)

### 12. `$Table` — HTML Tables from Vectors/Matrices (NEW)

Generate formatted HTML tables directly from computed vectors and matrices.

```
$Table{v1; v2; v3 @ "Header1"; "Header2"; "Header3" & fmt=3 & row=1}
$Table{M @ "Col A"; "Col B"; "Col C" & fmt=2 & row=1 & border=1 & zebra=1}
```

**Options:** `fmt=N` (decimal places), `row=1` (show row numbers), `border=0` (hide borders), `zebra=0` (no alternating rows).

Ideal for FEM result tables: bolt reactions, nodal displacements, element forces.

### 14. `$PlotMap` — FEM Color Maps on Arbitrary Geometry (NEW)

Render color maps (contour plots) on arbitrary finite element meshes — triangles, quads, or mixed.

```
$PlotMap{xj; yj; values; ej}
```

- **xj, yj** — node coordinate vectors
- **values** — scalar field per node (displacement, stress, pressure, etc.)
- **ej** — connectivity matrix (each row = node indices of one element, 1-based)

**Features:**
- Pixel-by-pixel rasterization with inverse bilinear mapping (Newton iteration)
- Phong shadow lighting from surface gradients
- Rainbow colormap with discrete bands (same palette as $Map)
- Automatic dual-legend when two separate element groups are detected (e.g., two footings)
- Per-group min/max color scaling for full color variation in each group
- Element edge mesh overlay in semi-transparent black

Ideal for FEM results on non-rectangular geometry: trapezoidal plates, footings with tie beams, irregular meshes.

### 15. Native 3D FEM Solver — `fem_hex8` (NEW, Apr 2026)

**4000 hex8 solved in under 11 seconds** — native C# assembly + Eigen sparse
Cholesky (`HpSymmetricMatrix.ClSolve`). No Calcpad `#for` loops, no RAM
explosion. Suitable for **soil mechanics**, **concrete foundations**, and
**3D continuum problems** directly from a Calcpad document.

**Functions added to the Calcpad language:**

```calcpad
' Auto-generate regular hex8 mesh (centered at origin)
nodes = mesh_hex8_nodes([Lx; Ly; Lz; nx; ny; nz; 1])
elems = mesh_hex8_elems([nx; ny; nz])

' Auto-generate loads + BCs for a "soil box" problem
' (base + lateral faces fixed + point load at top center)
specs = mesh_soil_specs([Lx; Ly; Lz; nx; ny; nz; 1; Pz])

' Variant with RECTANGULAR distributed load (SAP2000 surface pressure)
specs = mesh_soil_specs_rect([Lx; Ly; Lz; nx; ny; nz; 1; Rx; Ry; q])

' Solve Ku = F → returns vector of 3N displacements
u = fem_hex8(nodes; elems; E; nu; specs)

' Compute nodal stress matrix [S11, S22, S33, S12, S23, S13]
stress = fem_hex8_stress(nodes; elems; E; nu; u)
s33 = col(stress; 3)  ' vertical normal stress

' Visualize with SAP2000-style color map + interactive clipping planes
$Fem3D{col(nodes;1); col(nodes;2); col(nodes;3); elems; s33}
```

**Implementation highlights** (`Symbolic.Core/Calculator/FemSolver.cs`):
- **C3D8 element** (8-node linear hex, 24 DOF, trilinear shape functions)
- **Gauss 2×2×2 integration** (8 points per element)
- **6×6 isotropic D matrix** with Lamé parameters (λ, µ)
- **Sparse global K** assembled directly in `HpSymmetricMatrix` (skyline)
- **Eigen C++ `SimplicialLDLT`** for matrices ≥ threshold (auto)
- **Penalty BCs** with coefficient 1e20
- **Nodal stress** via element-center evaluation + averaging

**Validation vs SAP2000:**

| Problem | SAP2000 | Calcpad `fem_hex8` | Diff |
|---|---|---|---|
| Cube 1×1×1 m uniaxial compression | σ = −100 kN/m² | σ = −100 kN/m² | **0.01%** |
| Soil mass 20×20×10 m (Serquén PDF Fig. SF-70) rectangular load | S33_min = −10.4 | S33_min = −9.72 | **6.6%** |

(Serquén uses 32000 hex8, we use 4000 — difference is purely mesh refinement.)

**Visualization — `$Fem3D` with interactive clipping planes (Tweakpane):**
- `renderer.localClippingEnabled = true` (Three.js)
- 6 clipping planes (X/Y/Z min/max) with correct Y↔Z swap
- **Tweakpane GUI** (same library as `awatif-v2`) with folders per axis
- **SAP2000 colormap**: 14 colors (magenta → red → yellow → green → cyan → blue)
- **ShaderMaterial with 1D texture lookup** — interpolates by VALUE, not RGB
- **White background + black wireframe** — Abaqus/SAP2000 style

**Example files:**
- `Examples/Finite Elements/test_fem_hex8.cpd` — cube validation
- `Examples/Finite Elements/test_fem_hex8_soil_fast.cpd` — 4000 hex8 soil
- `Examples/Finite Elements/test_fem_hex8_rect_bulbo.cpd` — **Fig. SF-70 replica**
- `Examples/Finite Elements/Tutorial C3D8 - Solido 3D Paso a Paso.cpd` — pedagogical C3D8
- `Examples/Finite Elements/Tutorial Suelo C3D8 - Paso a Paso.cpd` — pedagogical soil mass

**SAP2000 API reference:** full Python + comtypes patterns documented in
`../guia de api sap 2000/README.md` (portable manual with all scripts,
tutorials, source files, and step-by-step instructions for other machines).

---

### 13. FEM Examples — Base Plates, Footings, Slabs (NEW)

Complete finite element analysis examples with step-by-step symbolic formulation, color maps ($Map), result tables ($Table), and Python verification:

**Shell-Thin (DKQ — Batoz & Tahar 1982):**
- **Base Plate W-Shape:** 600x500mm, 16 anchor bolts, Pu+Mx, compression-only Winkler contact (iterative), Von Mises. Validated vs SAP2000 (ratio 1.0002).
- **Base Plate HSS Tubular:** 500x400mm, 10 bolts (auto-filtered outside tube), Pu+Mx+My as 4 independent cases. K_DKQ assembled once, copied with `add()` for each case.
- **Rectangular Slab:** Simply supported, uniform load, validated vs Navier exact solution.

**Shell-Thick (Mindlin-Reissner + MITC4 — Bathe & Dvorkin 1985):**
- **Isolated Footing:** 4x4m, 600mm thick, Winkler soil, MITC4 elements (no hourglass, no shear locking). Smooth concentric contours matching SAFE (CSI).
- **Corner/Edge/Party-wall Footings:** Eccentric column positions with tie beams (shell or frame beam).
- **Two Independent Footings:** Combined $PlotMap with dual legend, validated vs SAP2000.
- **Winkler - Joint Spring:** Lumped springs (k_nodo = ks × A_trib) at each node. Color maps of deflection and soil pressure.
- **Winkler - Area Spring:** Consistent springs (ks × N^T × N integration) over each element. Same results, enables Soil Pressure in SAP2000.
- **Trapezoidal Plate (Awatif):** Irregular geometry with general Jacobian, $PlotMap mesh mode.

**Features across all FEM examples:**
- Symbolic formulation with `#deq` and `#sym diff()` — shape functions, B-matrices, double integrals rendered as equations
- Compression-only Winkler contact (iterative convergence in 2-3 iterations)
- Anchor bolts as axial springs with automatic inside/outside filtering
- Von Mises stress maps and moment distributions in tonf*m/m
- `$Table` for bolt reactions showing compression vs tension (lift)
- SVG layout diagrams with mesh, profile footprint, and bolt positions
- Python verification scripts with jet color maps (matplotlib)
- Validated against SAP2000 via comtypes API and SAFE (CSI)

**Validation pipeline:** Theory (Batoz/Zienkiewicz/Bathe) → Calcpad → Python → SAP2000/SAFE → Hekatan Struct

---

## Installation

### Requirements
- Windows 10/11 x64
- [.NET Desktop Runtime 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)

### Optional (for extended features)
- [Python 3.x](https://www.python.org/) — for `#python` blocks and `#pip`
- [Maxima](https://maxima.sourceforge.io/) — for `#maxima` blocks
- Python packages: `pip install numpy sympy openseespy` (or use `#pip` inside Calcpad)

### Download
- **[Calcpad-Symbolic-Setup-1.0.0.exe](https://github.com/GiorgioBurbanelli89/Calcpad-Symbolic/releases/latest)** — Windows installer
- **[Calcpad-Symbolic-win-x64.zip](https://github.com/GiorgioBurbanelli89/Calcpad-Symbolic/releases/latest)** — Portable zip

### Build from Source
```
git clone https://github.com/GiorgioBurbanelli89/Calcpad-Symbolic.git
cd Calcpad-Symbolic
dotnet build Symbolic.Wpf/Symbolic.Wpf.sln
dotnet run --project Symbolic.Wpf
```

---

## Quick Reference — New Keywords

| Keyword | Mode | Description |
|---------|------|-------------|
| `#sym expr` | Inline | Symbolic math (AngouriMath) |
| `#sym`...`#end sym` | Block | Multi-line symbolic |
| `#python`...`#end python` | Block | Python code execution |
| `#maxima`...`#end maxima` | Block | Maxima CAS execution |
| `#pip install pkg` | Inline | Install Python packages |
| `#deq expr = expr` | Inline | Display-only equation |
| `#function`...`#end function` | Block | User-defined function |
| `$Fem2D{...}` | Command | Interactive 2D FEM mesh |
| `$Fem3D{...}` | Command | Interactive 3D FEM mesh |
| `$Chart{...}` | Command | Interactive chart |
| `$Mesh{...}` | Command | SVG FEM mesh |
| `$Table{v1; v2 @ "H1"; "H2" & fmt=3}` | Command | HTML table from vectors/matrices |
| `$PlotMap{xj; yj; values; ej}` | Command | FEM color map on arbitrary mesh |
| `expr & [u1; u2; u3]` | Operator | Adimensionalize + stamp units |
| `expr \| [u1; u2; u3]` | Operator | Convert units per element |
| `mesh_hex8_nodes([Lx;Ly;Lz;nx;ny;nz;c])` | Function | Regular hex8 mesh nodes (Nx3) |
| `mesh_hex8_elems([nx;ny;nz])` | Function | Regular hex8 connectivity (Mx8) |
| `mesh_soil_specs([...])` | Function | Auto-generate loads+BCs (point load) |
| `mesh_soil_specs_rect([...;Rx;Ry;q])` | Function | Auto-generate loads+BCs (rect. pressure) |
| `fem_hex8(nodes;elems;E;nu;specs)` | Function | Solve Ku=F sparse Cholesky → u |
| `fem_hex8_stress(nodes;elems;E;nu;u)` | Function | Nodal stress matrix (Nx6) |

---

## Detailed Usage

### #sym — Symbolic Math

```
"Calculus
#sym diff(x^2 + 3*x; x)
#sym integrate(sin(x); x)
#sym integrate(x^2; x; 0; 1)
#sym pdiff(x^2*y + y^3; x)
#sym limit(sin(x)/x; x; 0)
#sym series(sin(x); x; 5)

"Vector Calculus
#sym gradient(x^2 + y^2; x; y)
#sym divergence(x^2; y^2; z^2; x; y; z)
#sym curl(y*z; x*z; x*y; x; y; z)
#sym laplacian(x^2 + y^2; x; y)
#sym jacobian(x^2; y^2; x; y)
#sym hessian(x^3 + x*y^2; x; y)

"Laplace Transform
#sym laplace(sin(t); t; s)
#sym laplace(exp(-a*t)*sin(w*t); t; s)
#sym ilaplace(1/s; s; t)

"ODE Solver
#sym ode2(0; 4)
#sym ode2(2; 5)

"Tensor Calculus
#sym strain(x^2*y; x*y^2; x; y)
#sym stress(0.001; 0.002; 0.0005; 200000; 0.3)

"Block mode
#sym
diff(x^2; x)
integrate(sin(x); x)
solve(x^2 - 4; x)
#end sym
```

### #python — Python Code

```
#python
from sympy import symbols, diff, integrate, solve, sin
x = symbols('x')
print(f"diff(x^3) = {diff(x**3, x)}")
print(f"solve(x^2-4) = {solve(x**2 - 4, x)}")
#end python
```

OpenSeesPy example:
```
#python
import openseespy.opensees as ops
ops.wipe()
ops.model('basic', '-ndm', 2, '-ndf', 3)
# ... define model ...
ops.analyze(1)
uy = ops.nodeDisp(2, 2)
print(f"uy = {uy}")
print(f"CALCPAD:uy={uy}")
ops.wipe()
#end python

'Result from OpenSeesPy:
uy
```

### #maxima — Maxima CAS

```
#maxima
diff(x^2 + 3*x + 1, x);
laplace(sin(t), t, s);
ode2('diff(y,x,2) + 4*y = 0, y, x);
eigenvalues(matrix([a, b], [c, d]));
#end maxima
```

### #deq — Display Equations

```
#deq N_1 = (1 - xi)*(1 - eta)/4
#deq K = E*I/L^3*[12; 6*L; -12; 6*L | 6*L; 4*L^2; -6*L; 2*L^2 | -12; -6*L; 12; -6*L | 6*L; 2*L^2; -6*L; 4*L^2]
```

### #function — User Functions

```
#function FrameKe(E; A; L)
k = E*A/L
FrameKe = k*[1; -1 | -1; 1]
#end function

K = FrameKe(200000; 0.01; 3)
```

### Unit Arrays — & and |

```
'Adimensionalize and stamp:
u = lsolve(K; F) & [cm; cm; rad]

'Convert per element:
u | [mm; mm; rad]

'Matrix units:
K & [kN/m; kN | kN; kN*m]
```

---

## Licensing

MIT License.

Based on **CalcpadCE** by Ned Ganchovski ([proektsoft.bg](https://proektsoft.bg)).
Fork by [imartincei](https://github.com/imartincei/CalcpadCE).
Symbolic extensions by [Jorge Burbano](https://www.linkedin.com/in/jorge-burbano-037444113/).

### Dependencies
- [AngouriMath](https://github.com/asc-community/AngouriMath) 1.4.0 — Symbolic math for .NET
- [Three.js](https://threejs.org/) 0.170.0 — 3D visualization
- [Maxima](https://maxima.sourceforge.io/) — Computer algebra system
- Original CalcpadCE dependencies (Markdig, SkiaSharp, WebView2)

## Project Structure

```
Calcpad-Symbolic/
  Symbolic.Core/       Math engine, AngouriMath, SymbolicProcessor
  Symbolic.Wpf/        WPF desktop app with syntax highlighting
  Symbolic.Cli/        Command-line interface
  Symbolic.OpenXml/    Word/Excel export
  Symbolic.Tests/      Unit tests
  Symbolic.Server/     Web server (Docker)
  Symbolic.Api/        Python API bindings
  Examples/            Example files (.cpd)
  Include/             FEM_Graphics.cpd macro library
  calcpad-viz/         TypeScript visualization library
```
