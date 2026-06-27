# Puntos de entrada para el editor (Hekatan Sheet)

Dos backends LISTOS para que el editor WYSIWYG los consuma. **No reescribir** —
sólo invocarlos. (Construidos por el "Claude de backends"; el "Claude del editor"
sólo los enchufa.)

---

## 1) Motor SIMBÓLICO (despeje, integrales, factor, Σ/∏, fourier, …)

### Opción A — en proceso (recomendada, rápida)
Referenciar `Symbolic.Core.dll` y llamar la API pública:

```csharp
using Calcpad.Core;

bool esOp   = SymbolicApi.IsOp("solve(A = pi*r^2; r)");   // ¿es operación simbólica?
string res  = SymbolicApi.Eval("solve(A = pi*r^2; r)");   // -> "r = -sqrt(A/π); r = sqrt(A/π)"
```

`Eval` devuelve el resultado en **texto plano** (sin tags de render). `"ERR: ..."` ante error.

### Opción B — como proceso (CLI)
```
dotnet Calcpad-Symbolic/Tools/SymCli/bin/Release/net10.0/symcli.dll "solve(A = pi*r^2; r)"
```
Imprime el resultado en UTF-8, una línea.

### Operaciones disponibles (sintaxis `op(arg1; arg2; …)`, separador `;`)
- **Despeje:** `solve(A = pi*r^2; r)`  → robusto, despeja de fórmulas con parámetros
- Cálculo: `integrate(f; x)`, `integrate(f; x; a; b)`, `diff(f; x; n)`, `limit(f; x; x0)`, `series(f; x; n)`
- Álgebra: `simplify`, `expand`, `factor`, `combine(expr)`, `collect(expr; x)`
- Polinomios: `parfrac(expr; x)`, `coeffs(expr; x)`, `confrac(numero)`
- Sumas: `sum(f; k; lo; hi)`, `product(f; k; lo; hi)`  (lo/hi pueden ser `inf`)
- Transformadas: `laplace(f; t; s)`, `ilaplace(F; s; t)`, `fourier(f; t; w)`, `invfourier(F; w; t)`
- EDOs: `ode1`, `ode2`, … ; Álgebra lineal: `det`, `inv`, `eigen`, `transpose`
- Vectorial: `grad`, `div`, `curl`, `laplacian`, `hessian`, `jacobian`
- Dominio: `assume(x>0; expr)`

Motores: Maxima (preferido, rápido) → FriCAS/Axiom (el de Mathcad) → AngouriMath.

---

## 2) Export a MATHCAD (.mcdx) + abrir Mathcad

### Comando único
```
powershell -File Calcpad-Lab/Tools/CpdToMcdx/cpd_to_mathcad.ps1  C:\ruta\hoja.cpd
```
Convierte la hoja `.cpd` a `.mcdx` (sin abrir navegador) y la **abre en Mathcad Prime**
por asociación de archivo. Imprime la ruta del `.mcdx`.

### Cómo serializar la hoja del editor a `.cpd` (lo que espera el conversor)
Una línea por región, **en orden vertical**:
- **Texto / título:** línea que empieza con comilla doble →  `"Mi título`
- **Ecuación:** la expresión cruda →  `M_x = P*L/4`   (usa `^` potencia, `*` producto, `sqrt()`, `_` subíndice)

El conversor (`CpdToMcdx.dll ... --no-preview`) hace el resto (MathML + empaquetado OPC).

> Prerrequisito: compilar `CpdToMcdx` en Release una vez:
> `dotnet build Calcpad-Lab/Tools/CpdToMcdx/CpdToMcdx.csproj -c Release`

### Referencia del flujo en C#
Ya hay un ejemplo del flujo completo (serializar hoja → .cpd → conversor → abrir
Mathcad) en `HekatanMathStudio/MainWindow.xaml.cs` → método `ExportToMathcad`.
Copiar ese patrón al botón "Generar .mcdx y abrir en Mathcad" de Hekatan Sheet.

---

## División de trabajo (para no chocar)
- **Editor (Hekatan Sheet):** dueño el "Claude del editor". Wirea estos dos backends.
- **Backends (Calcpad-Symbolic, CpdToMcdx, RE de Mathcad):** dueño el "Claude de backends".
  Si el editor necesita un cambio en un backend, pedirlo — no editar esas carpetas.
