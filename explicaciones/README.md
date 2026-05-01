# Explicaciones FEM — curso paso a paso en Calcpad-Symbolic

Esta carpeta contiene archivos `.cpd` con explicaciones detalladas de
conceptos de elementos finitos. Cada archivo se renderiza con
`Symbolic.Cli` produciendo un HTML interactivo con ecuaciones LaTeX,
gráficos y operaciones simbólicas.

## Cómo renderizar

```bash
Cli.exe nombre_del_archivo.cpd nombre_salida.html -s
```

Se abre el HTML en cualquier navegador (Edge, Chrome, Firefox).

## Archivos disponibles

### Curso completo
- **`FEM_Curso_Paso_a_Paso.cpd`** — 10 lecciones que arman intuición
  desde el resorte de 1 GDL hasta el cantilever con 2 elementos viga.
  Cada lección: pregunta → matemática → derivación simbólica → ejemplo
  numérico → gráfica.

### Funciones de forma
- **`Funciones_de_Forma_Detalladas.cpd`** — Cap 0 (intuición de
  proporciones), Cap 0.5 (coordenada natural ξ), 4 condiciones que
  cumplir, derivación lineal/cuadrática/Hermite/Q4 con `#sym diff` y
  `#sym simplify`.

### Matriz B (deformación-desplazamiento)
- **`Matriz_B_Detallada.cpd`** — qué es B físicamente, derivación 1D y
  2D CST, Jacobiano para isoparamétricos, B constante vs variable.
- **`Matrices_B_Por_Elemento_Detallado.cpd`** — un capítulo por elemento
  (barra, viga, CST, Q4, T4, H8) con dimensiones, estructura de la
  matriz y casos numéricos.
- **`Origen_y_Significado_de_B.cpd`** — historia (Cauchy → Ritz →
  Turner → Zienkiewicz), B^T·B como matriz de Gram, significado en
  contextos no-estructurales (calor, fluidos, electromagnetismo).

### Matriz de rigidez K_e = ∫B^T·D·B dV
- **`Esfuerzo_Rigidez_BtEB_Detallado.cpd`** — Hooke, matriz constitutiva
  D, energía interna, derivación de K_e con `#sym integrate`, tabla
  con dimensiones para 7 tipos de elementos.
- **`Por_Que_BtB_FEM_Mente_Geniales.cpd`** — la lógica de los
  pioneros: por qué B^T·D·B aparece **inevitablemente** cuando combinas
  energía cuadrática (U = (1/2)·ε^T·D·ε) con discretización lineal
  (ε = B·u).
- **`BtB_Algebra_Lineal_Detallado.cpd`** — B^T·B desde álgebra lineal
  pura (matriz de Gram, simétrica, PSD, conexión con SVD, mínimos
  cuadrados, proyecciones).

### Solver — almacenamiento esparso y factorización
- **`Archivos_K_Solver_SAPFIRE.cpd`** — qué contienen los archivos
  `.K_0 / .K_I / .K_J / .K_M` que ETABS/SAFE generan tras un análisis.
  Cap 0 ecuación K·u=F, Cap 1 ensamblaje de K en sistema de resortes,
  Cap 2 esparsidad (nnz vs n²), Cap 3 formato CSR (values/col_idx/row_ptr),
  Cap 4 Cholesky con derivación 3×3, Cap 5 reuso entre combinaciones,
  Cap 6 mapping completo de cada extensión, Cap 7 comparación con
  hekatan-fem (Eigen SimplicialLLT), Cap 8 reordering AMD para
  controlar fill-in.

### Conceptos físicos
- **`Energia_Cuadratica_Explicada.cpd`** — qué significa que la energía
  sea cuadrática (vs lineal del peso). Razón física: la fuerza crece
  con u, el área bajo F-u es triángulo (cuadruplica al doblar la base).
- **`Analogia_B_vs_k_Resorte.cpd`** — comparación B (FEM) vs k del
  resorte. K_e (no B) es el verdadero análogo del k. B es paso
  intermedio. Tabla de tamaños según GDL.
- **`Analisis_Dimensional_FEM.cpd`** — cada constante (B, k, D, K)
  lleva las unidades necesarias para que la ecuación balancee. B en
  1/m, k en N/m, D en N/m², K en N/m por entrada.

## Orden sugerido para aprender

1. `Energia_Cuadratica_Explicada.cpd` — intuición física
2. `Funciones_de_Forma_Detalladas.cpd` — interpolación nodal
3. `Matriz_B_Detallada.cpd` — del N a B
4. `Origen_y_Significado_de_B.cpd` — contexto histórico
5. `Esfuerzo_Rigidez_BtEB_Detallado.cpd` — la fórmula universal
6. `Por_Que_BtB_FEM_Mente_Geniales.cpd` — síntesis profunda
7. `FEM_Curso_Paso_a_Paso.cpd` — todo aplicado en 10 lecciones
8. `Matrices_B_Por_Elemento_Detallado.cpd` — referencia por elemento
9. `Analogia_B_vs_k_Resorte.cpd` — chequeo de comprensión
10. `Analisis_Dimensional_FEM.cpd` — control de unidades
11. `BtB_Algebra_Lineal_Detallado.cpd` — la matemática pura detrás
