# MATLAB FEM Pedagogical — Traducción del código C++ getLocalStiffnessMatrix

Estos `.m` son la traducción **1:1 didáctica** del código C++ que genera la
matriz de rigidez local de cada elemento (Frame, Shell DKT/DST, Q4, Interface).

## Estructura

```
MATLAB_FEM_Pedagogical/
├── README.md                              <-- este archivo
├── main_demo.m                            <-- script demostrador (corre todo)
│
├── dispatch/
│   └── getLocalStiffnessMatrix.m          <-- dispatcher por # de nodos
│
├── frame/
│   └── getLocalStiffnessMatrixFrame.m     <-- viga Timoshenko 6 DOF/nodo
│
├── shell_dkt/
│   ├── getLocalStiffnessMatrixShell.m     <-- DKT/DST triangular 18 DOF
│   ├── getMembraneStiffnessMatrix.m       <-- ANDES 9 DOF (3 drilling)
│   ├── getBendingStrainDisplacementMatrix.m
│   ├── getShearStrainDisplacementMatrix.m
│   └── getCellSmoothingTerms.m            <-- CS (Cell Smoothing)
│
├── shell_q4/
│   └── getLocalStiffnessMatrixShellQ4.m   <-- Q4 isoparamétrico
│
├── interface_/
│   └── getLocalStiffnessMatrixInterface.m <-- Goodman 1968
│
├── materials/
│   ├── buildIsoDb.m                       <-- D bending isótropo
│   ├── buildIsoDs.m                       <-- D corte isótropo
│   ├── buildOrthotropicDb.m
│   └── buildOrthotropicDs.m
│
└── modifiers/
    ├── applyReleases.m                    <-- condensación estática DOF liberados
    └── applyPartialFixitySprings.m        <-- resortes de fijación parcial
```

## Cómo correr el demo

```matlab
>> cd MATLAB_FEM_Pedagogical
>> main_demo
```

El script `main_demo.m`:
1. Construye una viga Timoshenko 6 m, sección 0.3×0.5, hormigón.
2. Construye un Shell DKT triangular (3,4,5).
3. Construye un Q4 (0,0)-(4,0)-(4,2)-(0,2).
4. Aplica releases en el frame (rótula en el nodo 2).
5. Aplica resortes de fijación parcial en una losa.

## Pedagógicamente

Cada archivo arranca con un **bloque de comentarios** que explica:
- Qué teoría usa (Timoshenko / Kirchhoff / Mindlin / ANDES / Goodman).
- De dónde sale cada matriz (autores y referencias).
- Por qué cada paso (no solo "qué").

La idea es que un estudiante pueda **leer el código y entender el FEM**,
no solo correrlo.
