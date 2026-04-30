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

**Equation numbering:** `#deq expr @@(Eq. 13.1)` — number aligned right.

**Block mode:** `#deq` ... `#end deq` — multiple numbered equations.

**Inline in text:** `'text '#deq expr' more text'`

**Inline in headings:** `"Title '#deq expr'`

### 5b. Inline Directives in Text (mixed mode)

When a line starts with `'` (text mode), each additional `'` toggles between **Text** and **Expression** mode. Multiple directives and bare math expressions can be embedded inline:

```
'Hooke '#deq F = k*x' is famous.
'Derivative '#sym diff(x^3; x)' is simple.
'Sum '$Sum{i^2 @ i=1:n}' works inline.
'Work '$Integral{k*x @ x=0:x_0}' direct.
'Factorial '$Product{i @ i=1:n}' is n!.
```

**Supported inline:** `#deq`, `#sym`, `$Sum`, `$Integral`, `$Product`.
**Block only (not inline):** `#noc/#equ`, `#hide/#show`.

**Position rule:** need at least 1 character of text before the first directive.
- ✓ `' '#deq F = k*x' at start.` (space before → works)
- ✗ `'#deq F = k*x' at start.` (no text before → fails)

#### 🆕 v1.3 — Permissive inline math

Inline math between apostrophes now accepts **display-only constructs** that previously required an explicit `#deq` or would fail evaluation. The parser auto-detects the pattern and routes to the display renderer without attempting to evaluate.

| Pattern | Example | Behaviour |
|---|---|---|
| Identity (LHS is not a variable) | `'(a+b)^2 = a^2 + 2·a·b + b^2'` | Renders as HTML math, no assignment check |
| Leibniz derivative | `'χ_1 = -d^2w/dx^2'` | Proper fraction with d² / dx² |
| Mixed partials + subscripts + Greek | `'-2·d^2φ_i/dx_j·dy_k'` | Handles Greek letters, subscripts, `^n` exponents |
| Multi-term biharmonic | `'d^4w/dx^4 + 2·d^4w/dx^2·dy^2 + d^4w/dy^4 = q/D_f'` | Each term rendered as fraction, summed |
| Integral call | `'W = integral(q·w; x; 0; L)'` | Displays as ∫ with limits |
| Matrix literal assignment | `'D = [1; ν; 0 \| ν; 1; 0 \| 0; 0; (1-ν)/2]'` | Matrix with proper brackets, RHS can reference undefined vars |
| Literal `#` / `$` reference | `'usa '#blk' para bloques, '$Plot' para graficas'` | Rendered as `<code>` |
| Last-resort fallback | `'V = (4/3)·π·R^3'` (R not yet defined) | Falls back to display mode instead of error |

Also fixed in v1.3:
- `#sym` now normalizes Unicode ops (`·`, `×`, `⋅`) → ASCII `*` before dispatching to AngouriMath.
- Depth guard (16 levels) in `TryRenderDeqSpecial` prevents stack overflow on pathological recursive inputs like `ε_ij = (∂u_i/∂x_j + ∂u_j/∂x_i)/2`.
- `#deq` Leibniz regex extended to accept `d^n` with caret, Greek letters, multi-char identifiers with subscripts (e.g. `d^2φ_i/dx_j`).
- Multi-term derivative renderer handles biharmonic `d^4w/dx^4 + 2·d^4w/dx^2dy^2 + d^4w/dy^4 = q/D` correctly.
- Inline matrix `=` is vertically centered (inline-flex + align-items:center).

#### 🆕 v1.3.1 — `#sym` output cleanup + explicit subscripts only

- **Maxima/AngouriMath artifact cleanup** in `#sym` results: `%e` → `e`,
  `log(e)` → `1` before re-parsing through Calcpad's HTML formatter.
  Without this, `#sym integrate(x^2·e^x; x)` returned the unsimplified
  Maxima string `((log(e)^2·x^2 − 2·log(e)·x + 2)·%e^(log(e)·x))/log(e)^3`
  which rendered as plain text. Now it renders as a proper formula.
- **Auto-subscript disabled**: `s1`, `s2`, `u1` no longer get silently
  rewritten to `s_1`, `s_2`, `u_1`. The user must use the explicit
  underscore notation (`s_1`) to get a subscript. `s1` stays as a
  literal identifier — both forms are valid Calcpad variables.

#### Comprehensive parser test

`Examples/Tests/test_PARSER_COMPLETO.cpd` exercises 30 sections covering
every directive in both inline and block form (identities, derivatives
with Greek letters and subscripts, integrals, matrices, `#sym`, `#inl`,
`#blk`, `#for`, `#if`, `#while`, `$Plot`, `$Map`, `$Sum`, `$Product`,
`$Integral`, `$Derivative`, `$Root`, `$Find`, cell arrays, units,
conversions). Renders with **0 errors**.

#### 🆕 v1.3.2 — `$DrawStruct` con primitivas de mecánica

Alias de `$Struct` (ambos nombres funcionan) — utility de **dibujo**
estructural con primitivas pre-renderizadas estilo libro de texto.
**No calcula nada**, solo dibuja. Para análisis usar `#sym`/`#deq`/`lsolve`.

Primitivas:
- **Estructurales:** `spring` (zigzag auto), `bar` (con hatches),
  `beam`, **`damper`** (cilindro pistón, nuevo), **`mass`** (rectángulo
  amarillo con borde grueso, nuevo), **`wall`** (línea + hatches, nuevo)
- **Apoyos:** `fixed`, `pin`, `roller`
- **Cargas:** `force` (flecha SALE del nodo, no entra),
  `moment` (arco curvo)
- **Anotación:** `node`, `label`, `dim`

Ejemplo masa-resorte 1 GDL completo:

```
$DrawStruct{
  fixed,0,0
  : spring,0,0,4,0,k=1000
  : mass,4.6,0,0.7,m
  : force,5.0,0,right,F(t)
  : dim,0,-1,5,-1,L_total
  @ title=Masa-resorte 1 GDL : w=700 : h=260
}
```

Sistema masa-resorte-amortiguador (k y c en paralelo):

```
$DrawStruct{
  fixed,0,0
  : spring,0,0.5,4,0.5,k
  : damper,0,-0.5,4,-0.5,c
  : mass,4.6,0,0.7,m
  : force,5.0,0,right,F(t)
  @ title=Masa-resorte-amortiguador : w=750 : h=320
}
```

Ver `Examples/Tests/test_GRAFICAS_DRAW.cpd` para 11 ejemplos
(funciones 1D, mapas 2D, charts, masa-resorte, viga simple, pórtico).

#### 🆕 v1.3.2 — `#svg` para diagramas didácticos con flujo de control

Bloque `#svg <w> <h>` ... `#end svg` con primitivas píxel-por-píxel:

- `.rect x y w h fillColor fillOpacity strokeColor strokeWidth`
- `.line x1 y1 x2 y2 color width`
- `.circle x y r color`
- `.arrow x1 y1 x2 y2 color width`
- `.text x y label size color align [style]`
- `.arc x y r startAngle endAngle color width`

Soporta variables Calcpad y **estructuras de control** (`#for`, `#if`)
dentro del bloque. Ejemplo de zigzag con loop:

```
#svg 480 160
.rect 0 0 480 160 #f5f5f5 1 #888 1
#for k = 0 : 12
    x1 = 60 + k*20
    y1 = 80 + 20*(-1)^k
    x2 = 60 + (k+1)*20
    y2 = 80 + 20*(-1)^(k+1)
    .line x1 y1 x2 y2 #555 2.5
#loop
#end svg
```

Cuándo usar cada uno:

| Querés... | Usá |
|---|---|
| Esquema estructural ya hecho (resorte, masa, viga, apoyo, fuerza) | `$DrawStruct{...}` |
| Diagrama didáctico con control píxel-por-píxel | `#svg ... #end svg` |
| CAD interactivo con pan/zoom | `$Draw{...}` |
| Funciones / vectores / curvas | `$Plot`, `$Map`, `$Chart` |

Ejemplos relacionados — los archivos FEA refactorizados a `#svg` con `#for`/`#if`:
- `Examples/Mechanics/Finite Elements/Rectangular Slab FEA.cpd`
- `Examples/Mechanics/Finite Elements/Flat Slab FEA.cpd`
- `Examples/Mechanics/Finite Elements/Deep Beam FEA.cpd`

#### 🆕 v1.3.3 — Truncado agresivo de vectores y matrices largos

`MathSettings.MaxOutputCount` ahora tiene **default 5** (antes 20).
Vectores/matrices con más elementos se truncan automáticamente:

- Vectores **siempre horizontales** cuando son largos (antes una columna
  de 200 elementos generaba 200 líneas verticales que se salían del
  margen y saltaban de página)
- Truncado: muestra los primeros 5 + `⋯` + el **último** elemento, con
  tooltip `"N elementos saltados (vector de M elementos)"`
- Matrices: filas con `⋮`, columnas con `⋯`, esquina con `⋱`

```
                            antes (maxCount=20)    ahora (maxCount=5)
v_5    (5 elementos)         completo               completo
v_15   (15)                  completo               5 + ⋯ + último
v_50   (50)                  20 + ⋯ + último        5 + ⋯ + último
v_200  (200)                 20 + ⋯ + último        5 + ⋯ + último
M_50×50                      20×20 + ⋮⋯              5×5 + ⋮⋯
```

Cualquier vector ≥ 6 ocupa exactamente la misma altura visual, sin
importar si tiene 6 o 6 millones de elementos. Configurable entre 5 y
100 desde Settings → MaxOutputCount si necesitás ver más detalle.

Ver `Examples/Tests/test_vectores_largos.cpd`.

#### 🆕 v1.4 — 9 directivas web sin escribir HTML

El parser ahora tiene 9 bloques de visualización web. El usuario escribe
DSL puro (JSON, JS, LaTeX, DOT) entre `#<libreria>` ... `#end <libreria>`
y el parser inyecta automáticamente el `<div>`, el script de la lib desde
CDN (una sola vez por documento) y el wrapping JS necesario.

**SIN escribir ningún `<>` HTML.**

| Directiva | Librería | CDN | Para qué |
|---|---|---|---|
| `#plotly` | Plotly.js 2.35 | cdn.plot.ly | Gráficas científicas interactivas (3D surface, scatter, contour, hover, zoom) |
| `#three` | Three.js 0.160 | unpkg | 3D real con OrbitControls (modelos estructurales rotables) |
| `#mermaid` | Mermaid 10 | jsdelivr | Diagramas (flowchart, sequence, gantt, classDiagram, gitGraph, pie) |
| `#canvas` | HTML5 nativo | — | Dibujo 2D directo con `ctx`, sin librería externa |
| `#cyto` | Cytoscape 3 | unpkg | Grafos científicos (sparsity de matrices, networks de nodos) |
| `#dot` | Graphviz (viz-js) | unpkg | Diagramas declarativos en sintaxis DOT |
| `#jsx` | JSXGraph 1.10 | jsdelivr | Geometría dinámica (puntos arrastrables, áreas reactivas) |
| `#map` | Leaflet 1.9 | unpkg | Mapas geográficos (PGA hazard, ubicación de proyectos) |
| `#math` | KaTeX 0.16 | jsdelivr | LaTeX completo con `\boxed`, `\frac`, matrices, integrales |

**Sintaxis común:**
```
#<libreria> [W] [H]
   ... contenido (DSL/JSON/JS/LaTeX según librería) ...
#end <libreria>
```

**Ejemplos rápidos:**

```
#plotly
{ data: [{x:[1,2,3], y:[4,5,6], type:'scatter'}], layout: {title:'demo'} }
#end plotly

#mermaid
flowchart TD
  D[DEAD] --> C1[1.4D]
  D --> C2[1.2D + 1.6L]
#end mermaid

#dot
digraph G { rankdir=LR; A -> B [label="x"]; B -> C; }
#end dot

#math
\frac{\partial^4 w}{\partial x^4} + 2\frac{\partial^4 w}{\partial x^2\partial y^2} + \frac{\partial^4 w}{\partial y^4} = \frac{q}{D}
#end math

#three
const cube = new THREE.Mesh(
    new THREE.BoxGeometry(2,2,2),
    new THREE.MeshStandardMaterial({color:0xffd966}));
scene.add(cube);
camera.position.set(5,5,5);
scene.add(new THREE.AmbientLight(0xffffff,1));
#end three
```

**Notas técnicas:**
- Los operadores ASCII (`<=`, `>=`, `==`, `!=`) que el lexer de Calcpad
  normalmente sustituye a Unicode (`≤`, `≥`, `≡`, `≢`) son **revertidos
  automáticamente** dentro de los bloques web — sin esto el JS quedaría roto.
- Three.js usa **importmap** inyectado para resolver el bare specifier
  `'three'` (necesario para que OrbitControls funcione).
- Para abrir un HTML generado, usar `http://` (file:// bloquea iframes
  y módulos por seguridad). Ej: `python -m http.server 8000`.

Ver tests:
- `Examples/Tests/test_GRAFICAS_WEB.cpd` — Plotly + Three + Mermaid + Canvas
- `Examples/Tests/test_GRAFICAS_WEB_2.cpd` — Cyto + Dot + Jsx + Map + Math

### 5c. Cell Arrays — `cells(n)` (Matlab-style)

Store multiple matrices in a single indexed container. Natively integrated with `#for` loops for FEM element assembly workflows.

```
K = cells(4)                       ← create container for 4 matrices

#for i = 1 : 4
    K.(i) = (E*A/L.(i))*[1;-1|-1;1]  ← assign matrix at slot i
#loop

K                                  ← render: [M₁ M₂ M₃ M₄] side by side
K.(2)                              ← access individual matrix (2nd slot)
K_sum = K.(1) + K.(2) + K.(3)      ← operations between cell elements
```

**Visual**: renders as `[ matrix_1  matrix_2  matrix_3 ... ]` with a single outer bracket container stretched to the full height of the matrices.

Use case: storing element stiffness matrices (`K_e`) in FEM without flattening into a giant global matrix. Direct translation of Matlab cell arrays like `K_e{i}`.

### 6. Layout Directives — `#inl`, `#blk`, `#cen`, `#margen`, `#pgb`

**Inline columns:** `#inl A = 5 ; B = 3 ; C = 8` — N columns in one line.

**Block columns:** `#blk` ... `#end blk` — multiple rows of columns.

**Center:** `#cen expr` (inline) or `#cen` ... `#end cen` (block).

**Margin:** `#margen 20` ... `#end margen` — justified text with margins.

**Page break:** `#pgb` — for PDF/Word export.

**Spacing:** three equivalent ways to force a non-breaking space:
- `~` — Calcpad shortcut (converts to `&nbsp;`)
- `&nbsp;` — HTML entity (explicit, standard)
- literal space — single space preserved, but browsers collapse multiples visually

For precise spacing/alignment use `~` or `&nbsp;`. For normal text, plain spaces are fine.

### 7. Tables — `$Table`

Generate HTML tables from vectors and matrices:
```
$Table{v1; v2 @ "Header1"; "Header2" & fmt=3 & row=1}
```
Options: `fmt=N` (decimals), `row=1` (row numbers), `border=0`, `zebra=0`.

### 8. Vector Rendering — Row and Column

Vectors with `;` render as **horizontal row**: `v = [1; 2; 3]`

`transp(v)` renders as **vertical column** with arrow.

Matrices with `|` render as vertical: `M = [1; 2|3; 4]`

### 9. User-Defined Functions — `#function` / `#end function`

Multi-line functions with parameter isolation. Return scalars, vectors, or matrices.

### 10. Interactive FEM Visualization — $Fem2D, $Fem3D, $Chart, $Draw, $Mesh

**$Fem2D** — 2D finite element mesh visualization (Three.js interactive)
**$Fem3D** — 3D finite element visualization with rotation/zoom
**$Chart** — Interactive charts with customizable styling (light theme)
**$Draw** — Vector drawings with line, arrow, circle, text, fillrect, hdim
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

### Web Graphics — 22 directivas de visualización

Bloques `#<lib> ... #end <lib>` que inyectan widgets interactivos desde CDN.

#### Phase 1 — visualización base (10)
| Directiva | Librería | Uso típico |
|---|---|---|
| `#svg` | SVG nativo | Vectores 2D declarativos (.rect, .line, .circle, .text) |
| `#plotly` | Plotly.js 2.35 | Gráficos científicos 2D/3D (scatter, surface, heatmap) |
| `#three` | Three.js 0.160 | Visualización 3D WebGL (geometría, FEM viewer, mesh) |
| `#mermaid` | Mermaid 10 | Diagramas (flowchart, sequence, gantt, classDiagram) |
| `#canvas` | HTML5 Canvas | Dibujo 2D libre (sin librería externa) |
| `#cyto` | Cytoscape 3 | Grafos / networks (sparsity de matrices, dependencias) |
| `#dot` | Graphviz (viz.js 3.7) | Grafos declarativos DOT |
| `#jsx` | JSXGraph 1.10 | Geometría dinámica interactiva |
| `#map` | Leaflet 1.9 | Mapas geográficos |
| `#math` | KaTeX 0.16 | Fórmulas LaTeX puras |

#### Phase 3 — visualización avanzada (10)
| Directiva | Librería | Uso típico |
|---|---|---|
| `#mathbox` | MathBox 2.3.1 | Math viz 3D, isosurfaces, integrales triples ⭐⭐⭐ |
| `#d3` | D3.js v7.8.5 | Custom plots data-driven (axes log-log, paramétricos) |
| `#echarts` | Apache ECharts 5.4.3 | Sankey, parallel coords, heatmap, treemap |
| `#vega` | Vega-Lite 5.21 | Charts declarativos JSON |
| `#visnet` | vis-network 9.1.9 | Networks dinámicos |
| `#p5` | p5.js 1.10 | Creative coding |
| `#matter` | Matter.js 0.20 | Física 2D rígidos |
| `#cannon` | Cannon-es 0.20 + Three.js | Física 3D rígidos |
| `#geogebra` | GeoGebra | Math interactivo educativo |
| `#chart` | Chart.js 4.4 | Gráficos simples (line, bar, doughnut) |

#### Phase 4 — animaciones (2)
| Directiva | Librería | Uso típico |
|---|---|---|
| `#anime` | anime.js 3.2 | Animaciones generales (DOM, SVG) |
| `#manim` | MathBox + tema oscuro | Animaciones matemáticas estilo 3blue1brown |

**Total: 22 directivas web operativas.** Cada bloque carga su CDN una sola vez por documento. Ejemplo:

```calcpad
#three 600 400
const cube = new THREE.Mesh(
    new THREE.BoxGeometry(2,2,2),
    new THREE.MeshStandardMaterial({color:0x4a90e2}));
scene.add(cube);
scene.add(new THREE.AxesHelper(3));
camera.position.set(4,4,4);
#end three
```

### Operadores simbólicos en matrices/vectores

`pdiff()`, `diff()`, `integrate()` ahora se traducen a los solvers nativos `$slope{...}` / `$area{...}` y se pueden usar dentro de literales de matriz, incluyendo multi-row e integrales múltiples (doble, triple):

```calcpad
g(x; y; z) = x^2 + y^2 + z^2
'Triple integral en cubo unitario:'integrate(integrate(integrate(g(x;y;z); z; 0; 1); y; 0; 1); x; 0; 1)
'(esperado: 1)
'Matriz Jacobiana de gradiente:
J(ξ; η) = [pdiff(N_1(ξ;η); ξ); pdiff(N_2(ξ;η); ξ) | pdiff(N_1(ξ;η); η); pdiff(N_2(ξ;η); η)]
J(0; 0)  ' evalúa numéricamente con central FD
```

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

## Pitfalls & Best Practices — `#deq`, `#sym`, HTML

> Verified empirically with `Examples/Finite Elements/TEST_sintaxis.cpd`. Run that file
> with `Cli.exe` to reproduce. The behaviors below are the ground truth in this
> version of Calcpad-Symbolic; some of them are not in the AngouriMath docs.

### `#deq` — what works and what doesn't

| Pattern | Result | Notes |
|---|---|---|
| `#deq y = a*x + b` | ✅ math | Basic algebra always renders |
| `#deq E = m*c^2` | ✅ superscript | `^` for single-char exponent |
| `#deq u_1 = N_1*ua` | ✅ subscript | `_1` works (numeric) |
| `#deq v_max = 5*q*L^4` | ✅ subscript | `_max` works (alphabetic, no braces) |
| `#deq σ_xx = E*ε_xx` | ✅ subscript | Greek + multi-char `_xx` works |
| `#deq T_{max} = K*L^2` | ❌ shows `T{max}` | **LaTeX braces `_{...}` NOT supported** |
| `#deq A^{2} = π*r^2` | ❌ shows `A^{2}` | **LaTeX braces `^{...}` NOT supported** |
| `#deq F = m*a @@(label)` | ❌ shows `@@(label)` | **`@@(...)` annotation does NOT render** |
| `#deq U = ∫f(x)dx` | ⚠️ shows `∫f(x)dx` | Integral symbol OK, but no fraction/limits |
| `#deq U = ∫*f(x)*dx` | ❌ shows asterisks | `*` shows literally — for clean integrals use HTML |
| `#deq U = ∫_{0}^{L} f dx` | ❌ shows `∫{0}^{L}` | Underscore stripped before braces |
| `#deq E = m*c^2 — formula` | ❌ "Invalid symbol —" | **Em dash inside `#deq` breaks parser** |
| `#deq v''''(x) = q(x)` | ✅ math | Multiple primes (apostrophes inside `()`) work |

**Workaround for integrals:** use HTML in `'<p>...</p>` instead of `#deq`:
```
'<p>U = (1/2) ∫₀<sup>L</sup> EI · (d²v/dx²)² dx</p>
'<p>K_e = ∫₋₁<sup>1</sup> ∫₋₁<sup>1</sup> B<sup>T</sup>·D·B·|J| dξ dη</p>
```

**Workaround for annotations:** put the label on a separate centered line:
```
#deq F = m*a
'<p style="text-align:center"><i>(2nd Newton's law)</i></p>
```

### `#sym` — what works and what doesn't

| Pattern | Result | Notes |
|---|---|---|
| `diff(expr; x)` | ✅ | 1st derivative |
| `diff(expr; x; n)` | ✅ | **n-th derivative — undocumented but works** (see `SymbolicProcessor.cs:147`) |
| `diff(diff(f; x); x)` | ❌ stack overflow | **Nested `diff` does NOT work** — use `diff(f; x; 2)` instead |
| `integrate(x^2; x)` | ✅ | Indefinite integral |
| `integrate(x^2; x; 0; 2)` | ✅ | Definite with numeric bounds |
| `integrate(x; x; 0; L)` | ⚠️ may hang | **Definite with symbolic bounds may stack overflow AngouriMath** |
| `integrate(sin(π*x/L)^2; x)` | ⚠️ may hang | sin² with symbolic argument can recurse infinitely |
| `simplify((1-ν)/(2*(1-ν^2)))` | ⚠️ may hang | **Simplification with single-char Greek can stack overflow** — workaround: replace `ν` with `nu` (Roman name) |
| `simplify(N1+N2+N3+N4)` (4 products) | ⚠️ may hang | Sum of 4+ products with multiple symbolic variables triggers OOM |
| `expand((a+b)^3)` | ✅ | |
| `factor(x^2-4)` | ✅ | |
| `solve(x^2-5*x+6; x)` | ✅ | Polynomial solver |

**Why nested `diff` fails:** `Diff()` returns a `SymResult` containing a presentation tag (`TAG_DERIV|d|dx|body`), not an `Entity` parseable by AngouriMath. Use the 3rd-arg form `diff(expr; x; n)` for higher derivatives.

**Recommendation for symbolic Greek arguments:** if a `#sym simplify`/`#sym integrate` block hangs, **replace single-char Greek symbols with their Roman names** (`ν` → `nu`, `ξ` → `r`, `η` → `s`, `θ` → `theta`) — AngouriMath simplification works fine with the renamed variables.

### HTML inside `'<p>...</p>` lines

| Pattern | Result | Notes |
|---|---|---|
| `'<p>texto</p>` | ✅ | Standard HTML paragraph |
| `'<p>x<sub>1</sub> + y<sup>2</sup></p>` | ✅ | Subs/sups work |
| `'<p>Esto es importante — muy.</p>` | ✅ | **Em dash works in HTML text** |
| `'<p>los '60 fueron decisivos</p>` | ❌ "Invalid symbol —" | **Apostrophe inside the line CLOSES the comment context** |
| `'<p>derivada w' a coincidir — ...</p>` | ❌ parser error | Same — apostrophes break the `'` line, then em dash fails |

**Rule of `'` lines:** the leading `'` starts an HTML‑comment line where the content is treated as raw text/HTML. Any **additional** apostrophe inside the line closes the context, and everything after is parsed as a Calcpad expression — special chars (`—`, `<`, `</`, `?`) then trigger errors.

**Workaround:** write `los anios 60` instead of `los '60`, `dw/dx` instead of `w'`, etc.

### `f'c` (concrete strength) inside expressions

```
E_c = 25000000   'kPa (concreto fc=4000 psi)        ✅ OK
E_c = 25000000   'kPa (concreto f'c 4000 psi)       ❌ FAILS — apostrophe breaks comment
```

### Quick checklist before rendering

- [ ] No `_{...}` or `^{...}` LaTeX braces in `#deq`
- [ ] No `@@(...)` annotations (use centered italics on next line instead)
- [ ] No em dashes (`—`) inside `#deq`
- [ ] No apostrophes (`'`, `f'c`, `'60`, `w'`) inside `'<p>...</p>` lines
- [ ] No nested `diff(diff(...))` — use `diff(expr; var; n)`
- [ ] No `simplify(...)` of expressions with single-char Greek vars (rename to Roman)
- [ ] Definite `integrate(...; var; 0; L)` with symbolic bound — risky; use indefinite + explain bounds in text

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
