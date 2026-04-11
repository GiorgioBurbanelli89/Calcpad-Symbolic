"""Zapata Lindero Vivienda — Viga como SHELL LAYERED (zapata + viga encima)
   Malla completa con Area Springs. Datos sincronizados con Calcpad.
   Usa pysap2000 (comtypes COM)."""
import sys, os, time, math
sys.path.insert(0, r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Pipy Api sap2000 hekatan\src')
import pysap2000

# === Datos (N, mm) ===
E = 22000.0       # MPa
nu = 0.2
t_zap = 400.0     # mm (espesor zapatas)
bv = 300.0        # mm (ancho viga)
hv = 400.0        # mm (peralte viga - encima de zapata)
ks = 0.03         # N/mm^3

# Zapata lindero
Lz1 = 1000.0; Bz1 = 1500.0; Lc1 = 300.0
xC1 = 150.0; yC1 = 750.0

# Viga
Lv = 2000.0

# Zapata aislada
Lz2 = 1500.0; Bz2 = 1500.0; Lc2 = 300.0
Ltotal = Lz1 + Lv + Lz2
xC2 = Lz1 + Lv + Lz2/2; yC2 = Bz2/2
Bmax = max(Bz1, Bz2)
yviga = (yC1 + yC2) / 2

# Cargas (N, N*mm)
P1 = 20.0 * 9806.65 * 1000
P2 = 20.0 * 9806.65 * 1000
Mx1 = 0.2 * 9806.65 * 1e6
My1 = 0.15 * 9806.65 * 1e6
Mx2 = 0.2 * 9806.65 * 1e6
My2 = 0.15 * 9806.65 * 1e6

de = 250.0

print("=== Zapata Lindero — Shell Layered (zapata + viga shell) ===")

# --- SAP2000 ---
model = pysap2000.attach()
pysap2000.set_units('N_mm')
model.InitializeNewModel()
model.File.NewBlank()

# Material
model.PropMaterial.SetMaterial('CONC', 2)
model.PropMaterial.SetMPIsotropic('CONC', E, nu, 0)

# Shell sections
# zapata: thick shell, t=400mm
model.PropArea.SetShell_1('zapata', 2, False, 'CONC', 0, t_zap, t_zap)
# viga_shell: thick shell, t=400mm (la viga tiene su propio espesor)
model.PropArea.SetShell_1('viga_shell', 2, False, 'CONC', 0, hv, hv)
# zapata+viga: layered shell (zapata abajo + viga arriba)
# En SAP2000: Shell-Layered con 2 capas
# Capa 1: zapata, t=400, offset desde centro = -hv/2
# Capa 2: viga, t=400 (hv), offset desde centro = +t_zap/2
# Total thickness = t_zap + hv = 800mm
t_total = t_zap + hv
model.PropArea.SetShell_1('zapata_viga', 2, False, 'CONC', 0, t_total, t_total)

# --- Malla completa (rectangular) ---
nx = int(round(Ltotal/de)); ny = int(round(Bmax/de))

nodes = {}
nid = 1
for j in range(ny+1):
    for i in range(nx+1):
        name = str(nid)
        model.PointObj.AddCartesian(i*de, j*de, 0, '', name)
        nodes[(i,j)] = name; nid += 1

print(f"Nodes: {nid-1}")

# --- Elements ---
ne_active = 0
for j in range(ny):
    for i in range(nx):
        xce = (i+0.5)*de; yce = (j+0.5)*de
        n1 = nodes[(i,j)]; n2 = nodes[(i+1,j)]
        n3 = nodes[(i+1,j+1)]; n4 = nodes[(i,j+1)]

        in_z1 = xce < Lz1 and yce < Bz1
        in_z2 = xce > Lz1+Lv and yce < Bz2
        in_viga = Lz1 <= xce <= Lz1+Lv and abs(yce-yviga) < bv/2 + de/2

        # Zona combinada: zapata + viga (donde se superponen)
        in_z1_viga = in_z1 and abs(yce-yviga) < bv/2 + de/2
        in_z2_viga = in_z2 and abs(yce-yviga) < bv/2 + de/2

        if in_z1_viga or in_z2_viga:
            prop = 'zapata_viga'  # Layered: zapata+viga
        elif in_z1 or in_z2:
            prop = 'zapata'       # Solo zapata
        elif in_viga:
            prop = 'viga_shell'   # Solo viga (entre zapatas)
        else:
            continue

        ename = f"E{ne_active}"
        model.AreaObj.AddByPoint(4, [n1,n2,n3,n4], '', prop, ename)
        ne_active += 1

        # Winkler solo en zapatas (no en viga libre)
        if in_z1 or in_z2:
            model.AreaObj.SetSpringAssignment(ename, 1, ks, 1, '', 6, '', True)

print(f"Active elements: {ne_active}")

# --- Loads ---
def nearest(xc, yc):
    best = None; bd = 1e10
    for (i,j), name in nodes.items():
        d = math.hypot(i*de-xc, j*de-yc)
        if d < bd: bd = d; best = name
    return best

n1 = nearest(xC1, yC1); n2 = nearest(xC2, yC2)
print(f"Col1: {n1}, Col2: {n2}")

model.PointObj.SetLoadForce(n1, 'DEAD', [0, 0, -P1, Mx1, My1, 0])
model.PointObj.SetLoadForce(n2, 'DEAD', [0, 0, -P2, Mx2, My2, 0])

# --- Fix nodes outside domain ---
for j in range(ny+1):
    for i in range(nx+1):
        xk = i*de; yk = j*de
        in_any = (xk<=Lz1 and yk<=Bz1) or \
                 (xk>=Lz1+Lv and yk<=Bz2) or \
                 (Lz1<=xk<=Lz1+Lv and abs(yk-yviga)<bv/2+de/2)
        if not in_any:
            name = nodes[(i,j)]
            model.PointObj.SetRestraint(name, [True]*6)

# --- Run ---
print("Running...")
model.Analyze.SetRunCaseFlag('DEAD', True)
model.Analyze.RunAnalysis()

# --- Results ---
model.Results.Setup.DeselectAllCasesAndCombosForOutput()
model.Results.Setup.SetCaseSelectedForOutput('DEAD')

ret = model.Results.JointDispl(n1, 0)
if ret[0] == 0 and ret[1] > 0:
    print(f"\nCol1: w={ret[7][0]:.4f} mm")

ret = model.Results.JointDispl(n2, 0)
if ret[0] == 0 and ret[1] > 0:
    print(f"Col2: w={ret[7][0]:.4f} mm")

print("\n=== Compare with Frame version and Calcpad ===")
print("Frame:   w_col1 = -5.8655 mm, w_col2 = -1.8474 mm (Calcpad)")
print("Layered: values above")
