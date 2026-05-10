# Calcpad-Symbolic

**Calcpad with Symbolic Math, Python, Maxima, FEM Visualization** — A fork of [CalcpadCE](https://github.com/imartincei/CalcpadCE) v7.6.2.

Calcpad-Symbolic extends CalcpadCE with three CAS engines, interactive FEM visualization, enhanced unit operators, user-defined functions, and Python/OpenSeesPy integration. All output rendered with the native CalcpadCE template.

> Gift to the CalcpadCE community. Since Ned closed the original repository, I wanted to contribute something useful.

**Author:** [Jorge Burbano](https://www.linkedin.com/in/jorge-burbano-037444113/) — Structural Engineer, Ecuador

---

## 🚀 Quick start — mixing text, equations, symbolic and numeric

This is the **most common** thing people want to do — write a paragraph that
explains a concept and embeds equations or numerical results inline. Here are
the **five idioms** you need:

### 1. Text only — line starts with `'`

```
'Esto es texto plano. No se evalua, solo se imprime.
```

### 2. Numeric calculation — write the formula on its own line (no `'`)

```
m = 5      'kg
g = 9.81   'm/s²
P = m*g    ← Calcpad shows: P = m·g = 5·9.81 = 49.05  (substitution + result)
```

### 3. Equation WITHOUT result (display only) — `#deq`

When you want to *show* a formula but **not** compute it (variables may be
undefined), use `#deq`:

```
#deq F = m*a                    ← shows: F = m·a (no values, no =result)
#deq E = (1/2)*m*v^2 @@(EC)     ← with equation number on the right
```

### 4. Symbolic operation (derivative, integral, etc.) — `#sym`

```
#sym diff(x^3; x)               ← shows: d/dx(x³) = 3·x²
#sym integrate(sin(x); x)       ← shows: ∫sin(x)dx = -cos(x) + C
#sym diff(x^5; x; 4)            ← shows: d⁴/dx⁴(x⁵) = 120·x  (n-th derivative)
```

### 5. Mixing everything in one line (apostrophe toggles)

A line that starts with `'` is in **Text mode**. Each additional `'` flips to
**Math/Calc mode**. So the count of `'` matters:

```
'La 2da ley es '#deq F = m*a' fundamental en dinamica.
'Derivar x³ da '#sym diff(x^3; x)' segun AngouriMath.
'La energia cinetica '#deq E_c = (1/2)*m*v^2' se conserva.
'Suma de cuadrados -'S = $Sum{i^2 @ i = 1 : 10}      ← renders as Σᵢ₌₁¹⁰ i² = 385
```

Pattern: `'<text>'<directive>'<more text>` — the embedded `'#deq …'` /
`'#sym …'` is in math mode, the surrounding text is in text mode.

**Important gotcha for `$Sum`/`$Integral`/`$Product`:** they need an
**assignment** (`varname = $Cmd{...}`), and the `'` BEFORE the `=` ends text
mode. Do **not** wrap them between two `'` like `'$Sum{...}'` — that fails.

### 6. Just an expression / identity, no result wanted

If you only want to display a formula in flowing text without `#deq` (e.g. an
identity where variables aren't defined), Calcpad's *permissive inline math*
detects the pattern and routes it to display:

```
'Identidad: '(a + b)^2 = a^2 + 2*a*b + b^2' es directa.
'Leibniz: 'χ = -d^2*w/dx^2' es la curvatura.
```

The bare math between apostrophes renders as proper formula even though no
variables are defined. This works since v1.3 (Apr 2026) — see
[Pitfalls section below](#pitfalls--best-practices--deq-sym-layout-html).

### Where to learn more

- **`Examples/Finite Elements/TEST_inline.cpd`** — 12 blocks, each demonstrating
  one mixing pattern. Renders with 0 errors. Copy-paste idioms from there.
- **`Examples/Finite Elements/TEST_sintaxis.cpd`** — 24+ patterns testing what
  works and what doesn't (with labels A1..L2).
- **`Examples/Finite Elements/FEM_Curso_Paso_a_Paso.cpd`** — 10-lesson FEM
  tutorial that mixes text + equations + symbolic + numeric + charts in every
  section. Real production example of the idioms above.
- **[Mixing text + math + symbolic in the same line](#mixing-text--math--symbolic-in-the-same-line--inline-mode)**
  — full 6-rule reference with cookbook (further down in this README).

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
- **[Calcpad-Symbolic-Setup-1.8.4.exe](https://github.com/GiorgioBurbanelli89/Calcpad-Symbolic/releases/latest)** — Windows installer
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

## Pitfalls & Best Practices — `#deq`, `#sym`, layout, HTML

> Verified empirically with `Examples/Finite Elements/TEST_sintaxis.cpd` against the
> Apr-26-2026 build of `Symbolic.Cli`. Run that test file with the **current** `Cli.exe`
> from `Symbolic.Cli/bin/Release/net10.0/` to reproduce. **Do not use older binaries** —
> they don't recognize newer keywords (`#margen`, `#pgb`, `#blq`) and report
> `Invalid symbol: "#"` on line 1 of any file using them.

### Visibility modes — `#val`, `#equ`, `#noc`

| Directive | `_isVal` | Effect |
|---|---|---|
| `#val` | 1 | Show **only the numeric value** (substituted, no formula) |
| `#equ` | 0 | Default — show formula AND value (`F = m·a = 5·2 = 10`) |
| `#noc` | -1 | **No calculation** — show formula symbolically, no substitution |

`#noc` ... `#equ` blocks are useful to **type matrix equations as text** without Calcpad evaluating them:

```
#noc
[F_1|F_2] = [k_e; -k_e|-k_e; k_e]*[u_1|u_2]
#equ
```

### Layout — `#blk`, `#inl`, `#cen`, `#pgb`, `#margen`

| Directive | Mode | Effect |
|---|---|---|
| `#inl A=5 ; B=3 ; C=8` | inline | N columns in one line (separated by `;`) |
| `#blk` ... `#end blk` | block | Multi-row column layout |
| `#cen expr` | inline | Center single line |
| `#cen` ... `#end cen` | block | Center entire block |
| `#pgb` | inline | Page break (PDF/DOCX only — empty `<div class="pgb">` in HTML) |
| `#margen 20` ... `#end margen` | block | Justified text with 20mm margins both sides (default 15mm) |

### `#deq` — what works and what doesn't

| Pattern | Result | Notes |
|---|---|---|
| `#deq y = a*x + b` | ✅ math | Basic algebra |
| `#deq E = m*c^2` | ✅ superscript | `^` for single-char exponent |
| `#deq u_1 = N_1*ua` | ✅ subscript | `_1` works (numeric) |
| `#deq v_max = 5*q*L^4` | ✅ subscript | `_max` works (alphabetic, no braces) |
| `#deq σ_xx = E*ε_xx` | ✅ subscript | Greek + multi-char subscript works |
| `#deq T_{max} = K*L^2` | ✅ subscript | **`_{...}` LaTeX braces work** — extract subscript content |
| `#deq A^{2} = π*r^2` | ⚠️ partial | **`^{...}` braces fail** — `A^{2}` shows literally (asymmetric with `_{...}`) |
| `#deq F = m*a @@(2da ley)` | ✅ eqnum | **`@@(text)` produces equation number on the right** (CSS class `.eqn`) |
| `#deq v''''(x) = q(x) @@(EDP)` | ✅ eqnum | Works even with multiple primes |
| `#deq v(x) = a_1*φ_1(x) + a_2*φ_2(x)` | ⚠️ varies | Long sum with `(x)` argument may render plain — split into multiple lines |
| `#deq U = ∫f(x)dx` | ⚠️ math literal | Integral symbol renders, but content shows multiplication dots |
| `#deq U = ∫*f(x)*dx` | ❌ asterisks visible | `*` between integrand parts displays literally — use HTML for clean integrals |
| `#deq U = ∫_{0}^{L} f dx` | ❌ shows `∫0{L}` | Underscore before `{` is stripped, content not nested under integral |
| `#deq E = m*c^2 — formula` | ❌ "Invalid symbol —" | **Em dash inside `#deq` breaks the AngouriMath parser** |
| `#deq` (alone) ... `#end deq` | ✅ block | Multi-line block — use `#deq` alone on its line, content next, then `#end deq` |
| `#deq θ_x = -∂w/∂y, θ_y = ∂w/∂x` | ✅ multi | Top-level `,` splits into multiple equations on the same `#deq` line |

**Best practice for integrals:** for complex limits, use HTML — produces visually cleaner output:
```
'<p>U = (1/2) ∫₀<sup>L</sup> EI · (d²v/dx²)² dx</p>
'<p>K_e = ∫₋₁<sup>1</sup> ∫₋₁<sup>1</sup> B<sup>T</sup>·D·B·|J| dξ dη</p>
```

### `#sym` — what works and what doesn't

| Pattern | Result | Notes |
|---|---|---|
| `diff(expr; x)` | ✅ | 1st derivative |
| `diff(expr; x; n)` | ✅ | **n-th derivative** (undocumented; see `SymbolicProcessor.cs:147`). Internal loop: `for i in 0..n: r = r.Differentiate(v)` |
| `diff(diff(f; x); x)` | ❌ stack overflow | **Nested `diff` does NOT work** — use `diff(f; x; 2)` |
| `integrate(x^2; x)` | ✅ | Indefinite integral |
| `integrate(x^2; x; 0; 2)` | ✅ | Definite with **numeric** bounds |
| `integrate(x; x; 0; L)` | ⚠️ may hang | **Definite with symbolic bound** can stack-overflow AngouriMath. Stay with indefinite + describe bounds in text |
| `integrate(sin(π*x/L)^2; x)` | ⚠️ may hang | `sin²` with symbolic compound argument recurses infinitely |
| `simplify((1-ν)/(2*(1-ν^2)))` | ⚠️ may hang | **Simplify with single-char Greek (ν, ξ, η)** can stack-overflow. Workaround: rename to Roman (`nu`, `r`, `s`) |
| `simplify(N1+N2+N3+N4)` (sum of 4 products) | ⚠️ may hang | OOM on large symbolic sums |
| `expand((a+b)^3)` | ✅ | |
| `factor(x^2-4)` | ✅ | |
| `solve(x^2-5*x+6; x)` | ✅ | Polynomial solver |
| `pdiff(x^2*y; x)` | ✅ | Partial derivative |
| `gradient(x^2+y^2; x; y)` | ✅ | Vector calculus |

**Why nested `diff` fails:** `Diff()` returns a `SymResult` containing a presentation tag `TAG_DERIV|d|dx|body` (string with metadata for rendering the `d/dx` fraction), **not** an AngouriMath `Entity`. The outer `diff` cannot re-parse it. Use the 3-arg form.

**Recommendation for symbolic Greek arguments:** if `#sym simplify`/`#sym integrate` hangs, **rename single-char Greek to Roman**:
- `ν` → `nu`
- `ξ` → `r`  (or `xi`)
- `η` → `s`  (or `eta`)
- `θ` → `theta`
- `π` → leave (AngouriMath knows π is a constant)

### Escape rules for `'`-prefixed HTML lines

The leading `'` starts a comment-as-HTML line. Inside that line:

| Pattern | Result |
|---|---|
| `'<p>texto sin apostrofes</p>` | ✅ |
| `'<p>x<sub>1</sub> + y<sup>2</sup></p>` | ✅ HTML subs/sups |
| `'<p>Esto es importante — muy.</p>` | ✅ Em dash in HTML text fine |
| `'<p>los '60 fueron decisivos</p>` | ❌ Apostrophe **closes** comment context, rest parsed as math, errors |
| `'<p>derivada w' a coincidir — ...</p>` | ❌ Same — `w'` closes context, em-dash invalid in math |
| `'kPa (concreto f'c 4000 psi)` | ❌ `f'c` closes the comment in a regular comment line |

**Rule:** any **additional** `'` inside a `'`-line closes the comment context. Everything after is parsed as a Calcpad expression. Use `anios 60`, `dw/dx`, `fc`, etc.

### Quick checklist before rendering

- [ ] Use the **current** `Cli.exe` (Apr 2026 build or later) — older builds reject `#margen`, `#pgb`, `#blq`
- [ ] Avoid `^{...}` LaTeX braces in `#deq` (use `^N` for single chars; `^{N}` shows literally)
- [ ] No em dashes (`—`) inside `#deq` (use them only in `'<p>` HTML)
- [ ] No additional apostrophes inside `'`-prefixed comment lines
- [ ] **Nested `diff(diff(...))` now WORKS** since `SymbolicProcessor.cs` v1.3.3 (May 2026) — recursively resolves inner symbolic calls before applying the outer derivative
- [ ] No `simplify(...)` of expressions with single-char Greek vars (rename to Roman)
- [ ] Definite `integrate(...; var; 0; L)` with symbolic bound — risky; use indefinite + describe bounds in text
- [ ] For clean integrals with limits, use HTML `∫₀<sup>L</sup>` instead of `#deq`

---

## Mixing text + math + symbolic in the same line — Inline mode

Calcpad has rich support for mixing text and math in a single line. The rules
take some practice — see `Examples/Finite Elements/TEST_inline.cpd` for a
12-block test that exercises every pattern. Here are the working idioms:

### Rule 1 — `'` toggles between Text and Math mode

A line that starts with `'` is in **Text mode**. Each additional `'` toggles to
**Math mode** (and back). This means the count of apostrophes matters.

### Rule 2 — Inline `#deq` and `#sym` directly in text

```
'La 2da ley es '#deq F = m*a' fundamental.
'Derivada cubica '#sym diff(x^3; x; 3)' constante.
```

Pattern: `'<text>'<directive>'<more text>`. The first `'` opens text, the second
`'` switches to math (where the directive lives), the third `'` returns to text.
Renders as: *La 2da ley es F = m·a fundamental.*

### Rule 3 — Inline `$Sum`, `$Integral`, `$Product`

These need an explicit assignment: `varname = $Cmd{...}`. The pattern is
**different** from `#deq`/`#sym`:

```
'Suma -'S = $Sum{i^2 @ i = 1 : 10}
'Trabajo -'W = $Integral{k*x @ x = 0 : 1}
'Factorial -'P = $Product{i @ i = 1 : 5}
```

The `'` inside text closes the comment, then `S = $Sum{...}` is parsed as a
calculation. Result: *Suma - S = Σᵢ₌₁¹⁰ i² = 385*. Note: do **not** wrap
`$Sum{...}` between two apostrophes — that treats it as text and shows
`$Sum{...}` literally.

### Rule 4 — Permissive inline math (v1.3+)

Bare math identities work without any directive (auto-detected and routed to
display renderer):

```
'Identidad: '(a + b)^2 = a^2 + 2*a*b + b^2' clasica.
'Biharmonica: 'd^4*w/dx^4 + 2*d^4*w/dx^2*dy^2 + d^4*w/dy^4 = q/D_f' multi-termino.
```

If you reference variables that aren't yet defined, Calcpad falls back to
display mode instead of erroring. Useful for citing formulas before computing.

### Rule 5 — Numeric values mixed in text

The cleanest way to mix numeric results with text is to **define variables in
their own block lines**, then reference them in `'<p>` HTML:

```
masa = 5
g = 9.81
P = masa*g

'<p>Para masa=5kg y g=9.81 m/s² el peso es P = 49.05 N.</p>
```

If you want the *substitution chain* visible (`P = m·g = 5·9.81 = 49.05`), put
the calculation on its own line **without** `'`:

```
masa = 5
g = 9.81
P = masa*g    ← shows: P = m·g = 5·9.81 = 49.05 with substitution arrows
```

### Rule 6 — Block mode for complex multi-line content

When a line gets too cluttered or you want each item visually separated, use
block form. Every directive has a block counterpart:

| Inline | Block |
|---|---|
| `'foo '#deq F = k*x' bar` | `#deq F = k*x` (alone on a line) |
| `'foo '#sym diff(x^3; x)' bar` | `#sym` ... `diff(x^3; x)` ... `#end sym` |
| `'foo '$Sum{i @ i=1:5}'` | (always on its own line — no block form) |

### Cookbook

```
"Section title — inline mixed
'<p>The cinetic energy of a body is '#deq E_c = (1/2)*m*v^2'
'where m is mass and v is velocity. The derivative w.r.t. v
'is '#sym diff((1/2)*m*v^2; v)' which equals momentum p = m·v.</p>

"Block form of the same content
#deq E_c = (1/2)*m*v^2

#sym
diff((1/2)*m*v^2; v)
#end sym

'<p>The above shows the kinetic energy formula and its derivative
'as separate display equations.</p>

"Numeric example
m = 2     'kg
v = 10    'm/s
E_c = (1/2)*m*v^2     'Joules

'<p>Para m=2 kg y v=10 m/s la energia cinetica vale E_c = 100 J.</p>
```

### Working example in repo

`Examples/Finite Elements/TEST_inline.cpd` — 12 blocks covering every pattern.
Render with `Cli.exe TEST_inline.cpd test_inline.html -s` and inspect the HTML.

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
  explicaciones/       FEM didactic .cpd lessons (B^T·D·B, Jacobian, K_e…)
```

---

## Changelog

### v1.8.4 (May 2026) — Save round-trip protection (no .cpd corruption on close)

The WPF editor used to silently corrupt `.cpd` files when the user opened
them and clicked Save / answered Yes to "Save changes?" on close — even
when no real edit had been made. The HighLighter's re-tokenization
sometimes lost whitespace around inline `'#deqξ'` directives or dropped
the leading apostrophe of a `#blk` cell, and `GetInputText()` re-emitted
the corrupted reconstruction.

Fix:
- Snapshot the exact disk bytes (`_loadedFileText`) on every `FileOpen`.
- Track whether the user actually typed something since load
  (`_userTypedSinceLoad`) via `RichTextBox_PreviewKeyDown`.
- On save: if `_userTypedSinceLoad == false`, write the original bytes
  verbatim. Only emit `GetInputText()` when the user really edited.

Net effect: opening + closing a file (without typing) is now a true
no-op — the file on disk is byte-identical to what it was.

### v1.8.3 (May 2026) — Repackage clean (no contaminated examples)

Same source as v1.8.2 but the v1.8.2 installer accidentally included a
`.cpd` file that was overwritten by the WPF auto-save on close (lost
inline `'#deqξ'` spaces and a `#blk` cell apostrophe). The repo `.cpd`
was reverted to its clean form and the installer was recompiled.

### v1.8.2 (May 2026) — WPF highlighter polish + CLI batch mode

**WPF highlighter:**
- `#sym ... #end sym` blocks now render as passthrough (dark blue). Symbol
  names like `xi`, `eta`, `theta`, `alpha` no longer flagged as undeclared
  variables / red error background.
- Greek letters (`ξ`, `η`, etc.) and `@@(label)` after display equations no
  longer marked as errors in `#deq`. Tokens that hit `Types.Error` from
  `InitType` get demoted to `Types.Comment` (green) when the line is a
  display directive (`#deq`, `#sym`, `#inl`, `#blk`, `#cen`, `#noc`).
- `DetectBlockContextFromPrevious` recognizes `#sym` alone as block opener
  for paragraph-by-paragraph re-highlighting.

**CLI batch mode:**
- No longer crashes with `InvalidOperationException` when stdin is
  redirected (was blocking `for f in *.cpd; do cli.exe "$f" "$out" -s; done`).
- Relative paths like `Examples/x.cpd` no longer get duplicated to
  `Examples/Examples/x.cpd` after `cwd` change. Now resolves to absolute
  paths *before* `Directory.SetCurrentDirectory`.

### v1.8.0 (May 2026) — Merge with main lineage

Brought 87 commits from `origin/main` into the parser-fixes branch:

- **Excel Viewer** (Univer-based) with `#blk` Calculate() integration
- **Sandbox + Maxima fallback** — `Symbolic.Sandbox` project, AngouriMath
  fallback to Maxima for unsupported operations
- **12 explicaciones FEM** in `explicaciones/` — curso paso a paso, B^T·B
  desde álgebra lineal, funciones de forma, energía cuadrática, B vs k,
  análisis dimensional, etc.
- **Solver SAPFIRE doc** — `Archivos_K_Solver_SAPFIRE.cpd` explica
  `.K_0/.K_I/.K_J/.K_M`, ensamblaje sparse, Cholesky, AMD reordering
- **6 ejemplos Placas** — Kirchhoff, Mindlin, Q4 membrana, DKT, MITC4,
  Layered Laminate
- **Tesis Cap 15 Paz** — derivación triangular plate plane stress + axial,
  corte, flexión, torsión

### v1.7.0–v1.7.4 (May 2026) — Unit propagation + `''` semantics

- **HpMatrix.CreateFromRows** unit propagation: when first row is unitless
  but later rows have units, propagate to all rows (was assigning row units
  in column-major confusion).
- **HpVector setter** inherits Units from first non-zero unit-bearing
  assignment when initially null. Lets users build matrices with
  `K.(j;j) = K.(j;j) + k_node` without pre-declaring Unit on the template.
- **MatrixCalculator.SetUnits**: literal `1` accepted as alias for
  `clrunits` (`setunits(M; 1)` ≡ `setunits(M; clrunits(M))`).
- **`''` (double single quote)** outside a text region = "close
  expression and re-enter text mode" (`'expr''text'` ≡ `'expr' 'text'`).
  Inside a text region of same quote type = literal escaped quote.
- **WPF editor**: `''` is rendered as visible chars in the editor (the
  delimiter the user typed) instead of being silently consumed.

### v1.6.0 (May 2026) — Complete 22 web visualization directives

Phase 1 (10): `#svg`, `#plotly`, `#three`, `#mermaid`, `#canvas`, `#cyto`,
`#dot`, `#jsx`, `#map`, `#math`.
Phase 3 (10): `#mathbox`, `#d3`, `#echarts`, `#vega`, `#visnet`, `#p5`,
`#matter`, `#cannon`, `#geogebra`, `#chart`.
Phase 4 (2): `#anime`, `#manim`.

`Mathbox` enum reordered before `Math` to avoid prefix collision (`#mathbox`
matched `#math` first because `GetKeyword` iterated bucket in declaration
order).

### v1.5.0 (May 2026) — Parser/units/web-graphics groundwork

- **Unicode multiplication operator aliases** `·` (U+00B7), `×` (U+00D7)
  added to `MathParser.Input.GetCharType` and `Calculator.OperatorIndex`
  (slot 4, same as `*`).
- **Prose-text fallback** in `ExpressionParser`: lines that look like prose
  (no operators, no assignments, several spaces) are routed to text rendering
  instead of being parsed and erroring out as undeclared identifiers.
- **Inline directives** (`#deqξ`, `#sym expr`) compact form supported via
  `TryStripInlineDirective`.
- **Line-extension splice** (`_` continuation) skipped inside `#plotly`,
  web-graphics, and `#svg` blocks so JavaScript `;` doesn't trigger
  unintended line concatenation.
