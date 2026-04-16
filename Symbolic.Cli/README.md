# Calcpad Symbolic CLI

Motor de calculo simbolico y numerico para ingenieria estructural.
Genera reportes HTML, DOCX y PDF desde archivos `.cpd`.

## Uso rapido

```bash
# Generar HTML desde un archivo .cpd
Cli.exe mi_calculo.cpd

# Generar PDF
Cli.exe mi_calculo.cpd pdf

# Generar DOCX
Cli.exe mi_calculo.cpd docx

# Modo silencioso (no abre el archivo)
Cli.exe mi_calculo.cpd -s
```

## Sintaxis del archivo .cpd

### Titulos y texto
```
"Titulo principal (h1)
"Subtitulo (h2)
"Sub-subtitulo (h3)
'Esto es texto (comentario que se muestra)
'Se puede usar <b>negrita</b> y <i>cursiva</i> HTML
```

### Variables y calculos
```
E = 210000'MPa          ← variable con unidad en comentario
ν = 0.3
G = E/(2*(1 + ν))       ← se calcula automaticamente
'G ='G'MPa              ← muestra: G = 80769.23 MPa
```

### Funciones personalizadas
```
f(x) = x^2 + 3*x        ← define funcion
y = f(5)                 ← usa funcion: y = 40
```
Separar argumentos con `;`:
```
N_1(ξ; η) = (1 - ξ)*(1 - η)/4
```

### Ecuaciones decorativas (#noc ... #equ)
Para ecuaciones de referencia que NO se calculan:
```
#noc
K_e = B^T*D*B*det(J)
M = D*S*φ
#equ
```

### Ecuaciones decorativas con numero (#deq ... @@)
Para ecuaciones de referencia con numeracion al estilo libro:
```
#deq u_x = z*φ_x @@(13.1)
#deq ε_x = du/dx + z*dφ_x/dx @@(13.2)
#deq M_x = D*dφ_x/dx
```
Resultado:
```
u_x = z·φ_x                                              (13.1)
ε_x = du/dx + z·dφ_x/dx                                  (13.2)
M_x = D·dφ_x/dx
```
Sin `@@` no muestra numero. Soporta derivadas de Leibniz, primas, etc.

### Columnas inline (#inl)
Para mostrar expresiones en columnas lado a lado (una sola linea):
```
#inl D ; α
#inl E ; ν ; G
#inl F = 100 ; 'Fuerza axial en kN
```
Separa por `;` — tantas columnas como quieras (2, 3, 4, N).
Cada parte puede ser un calculo o texto (con `'`).

### Columnas en bloque (#blk ... #end blk)
Para multiples filas de columnas con borde:
```
#blk
E = 210000 ; 'Modulo de elasticidad (MPa)
ν = 0.3 ; 'Coeficiente de Poisson
G = E/(2*(1 + ν)) ; 'Modulo de corte (MPa)
h = 0.2 ; 'Espesor de placa (m)
#end blk
```
Cada linea dentro del bloque es una fila de columnas.

### Condicionales (#if ... #else ... #end if)
```
L_t = a/h
#if L_t > 20
'→ Placa delgada
#else
'→ Placa gruesa
#end if
```

### Graficas ($Plot)
```
f(x) = sin(x)
g(x) = cos(x)
$Plot{f(x) | g(x) @ x = 0 : 6.28}
```

### Mapas de contorno ($Map)
```
f(x; y) = sin(x)*cos(y)
$Map{f(x; y) @ x = -3 : 3 & y = -3 : 3}
```

### Matrices y vectores
```
v = [1|2|3]              ← vector columna
M = [1; 2; 3|4; 5; 6]   ← matriz 2x3 (| separa filas, ; columnas)
D = E*t^3/(12*(1 - ν^2))*[1; ν; 0|ν; 1; 0|0; 0; (1 - ν)/2]
```

### Integracion numerica ($Area, $Integral)
```
$Area{f(x) @ x = 0 : 1}
$Integral{f(x) @ x = 0 : 1}
```

### Derivada ($Derivative, $Slope)
```
$Slope{f(x) @ x = 2}
$Derivative{f(x) @ x = 2}
```

### Sumatorias y productos
```
$Sum{k^2 @ k = 1 : 10}
$Product{k @ k = 1 : 5}
```

### Resolver ecuaciones ($Root, $Find)
```
$Root{f(x) = 0 @ x = -1 : 1}
$Find{f(x) @ x = 0 : 10}
```

### Simbolico (#sym ... #end sym)
Para calculo simbolico (derivadas, simplificacion):
```
#sym
diff(x^3 + 2*x; x)
#end sym
```

### Espaciado y formato
```
'~ = espacio pequeno
'~~ = espacio mediano
'~~~~~~~~~~~~ = espacio grande
```

### Letras griegas
Usar ` (acento grave) antes de la letra latina:
```
`a = α    `b = β    `g = γ    `d = δ    `e = ε
`f = φ    `l = λ    `m = μ    `n = ν    `p = π
`r = ρ    `s = σ    `t = τ    `w = ω    `q = θ
```
O escribir directamente los caracteres Unicode: α, β, γ, δ, ε, φ, etc.

## Ejemplo completo

```
"Placa Simplemente Apoyada — Mindlin-Reissner
'Referencia: Zienkiewicz & Taylor, Cap.13

"1. Datos
E = 10920'kN/m²
ν = 0.3
h = 0.1'm
κ = 5/6

"2. Rigideces
D = E*h^3/(12*(1 - ν^2))
G = E/(2*(1 + ν))
α = κ*G*h
'D ='D'kN·m
'α ='α'kN/m

"3. Curvaturas (referencia)
#noc
κ_x(x; y) = dφ_x/dx
κ_y(x; y) = dφ_y/dy
κ_xy(x; y) = dφ_x/dy + dφ_y/dx
#equ

"4. Benchmark SSSS
a = 1.0'm
q = -1.0'kN/m²
w_bar = 0.004270
w_centro = w_bar*q*a^4/D
'w_centro ='w_centro'm
```

## Estructura de archivos

```
Symbolic.Cli/
├── Cli.exe              ← ejecutable
├── Calcpad.Core.dll     ← motor de calculo
├── doc/
│   ├── HELP.TXT         ← referencia completa
│   ├── template.html    ← plantilla del reporte
│   └── help.html        ← ayuda interactiva
├── Examples/
│   └── Python/          ← integracion con Python
├── Fonts/               ← fuentes para PDF
└── Settings.xml         ← configuracion
```

## Integracion con Python

Ver `Examples/Python/Readme.md` para usar Calcpad desde Python
via la libreria PyCalcpad.dll y Python.NET.
