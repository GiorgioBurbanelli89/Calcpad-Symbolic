"""Validar zapata lindero + viga + zapata aislada — Vivienda
   Winkler AREA SPRING en SAP2000. Datos sincronizados con Calcpad.
   Usa pysap2000 wrapper (comtypes COM)."""
import sys, os, time
sys.path.insert(0, r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Pipy Api sap2000 hekatan\src')
import pysap2000

# === Datos IGUALES al Calcpad (kN, m) ===
E = 22000000.0   # kN/m^2 (22000 MPa, fc=210)
nu = 0.2
t_zap = 0.4      # m (espesor zapatas)
t_viga = 0.4     # m (espesor viga)
ks = 30000.0     # kN/m^3 (subgrade modulus)

# Zapata lindero (izquierda)
Lz1 = 1.0; Bz1 = 1.5; Lc1 = 0.3
xC1 = 0.15; yC1 = 0.75  # columna en borde

# Viga de amarre
Lv = 2.0; Bv = 0.3

# Zapata aislada (derecha)
Lz2 = 1.5; Bz2 = 1.5; Lc2 = 0.3
xC2 = Lz1 + Lv + Lz2/2; yC2 = Bz2/2

Ltotal = Lz1 + Lv + Lz2
Bmax = max(Bz1, Bz2)
yviga = (yC1 + yC2) / 2

P1 = 20.0 * 9.80665   # kN (20 tonf)
P2 = 20.0 * 9.80665   # kN (20 tonf)
M1 = 0.2 * 9.80665    # kN*m (0.2 tonf*m)
M2 = 0.2 * 9.80665    # kN*m
de = 0.15              # mesh size (m)

print(f"=== Zapata Lindero Vivienda — Area Spring ===")
print(f"Ltotal={Ltotal}m, Bmax={Bmax}m, de={de}m")
print(f"Col1:({xC1},{yC1}) P1={P1:.1f}kN")
print(f"Col2:({xC2},{yC2}) P2={P2:.1f}kN")

# --- Connect to SAP2000 ---
model = pysap2000.attach()
pysap2000.set_units('kN_m')
model.InitializeNewModel()
model.File.NewBlank()

# --- Material ---
model.PropMaterial.SetMaterial('CONC', 2)  # 2 = Concrete
model.PropMaterial.SetMPIsotropic('CONC', E, nu, 0)

# --- Shell sections: Thick (Mindlin) ---
# SetShell_1(name, ShellType, IncludeDrillingDOF, MatProp, MatAngle, Thickness, BendingThk)
# ShellType: 1=ShellThin, 2=ShellThick, 3=Membrane
model.PropArea.SetShell_1('zapata', 2, False, 'CONC', 0, t_zap, t_zap)
model.PropArea.SetShell_1('viga', 2, False, 'CONC', 0, t_viga, t_viga)

# --- Create mesh ---
nx = int(round(Ltotal / de)); ny = int(round(Bmax / de))
nj1x = nx + 1; nj1y = ny + 1
nodes = {}
nid = 1
for j in range(nj1y):
    for i in range(nj1x):
        name = str(nid)
        model.PointObj.AddCartesian(i * de, j * de, 0, '', name)
        nodes[(i, j)] = name
        nid += 1

print(f"Nodes: {nid-1} ({nj1x}x{nj1y})")

# --- Create shell elements and classify ---
ne_active = 0
for j in range(ny):
    for i in range(nx):
        xce = (i + 0.5) * de
        yce = (j + 0.5) * de
        n1 = nodes[(i, j)]
        n2 = nodes[(i+1, j)]
        n3 = nodes[(i+1, j+1)]
        n4 = nodes[(i, j+1)]

        # Classify element
        in_z1 = xce < Lz1 and yce < Bz1
        in_viga = Lz1 <= xce <= Lz1 + Lv and abs(yce - yviga) < Bv/2 + de/2
        in_z2 = xce > Lz1 + Lv and yce < Bz2

        if in_z1 or in_z2:
            prop = 'zapata'
        elif in_viga:
            prop = 'viga'
        else:
            continue

        ename = f"E{j*nx+i+1}"
        model.AreaObj.AddByPoint(4, [n1, n2, n3, n4], '', prop, ename)
        ne_active += 1

        # Area Spring (Winkler) for zapata elements only (not viga)
        if in_z1 or in_z2:
            # SetSpringAssignment: AreaName, MyType, s, SimpleSpringType, LinkProp, Face, DirCoordSys, OutwardNorm
            # MyType=1 = Simple, s = spring value per unit area
            # SimpleSpringType: 1=compression only (soil)
            model.AreaObj.SetSpringAssignment(ename, 1, ks, 1, '', 6, '', True)

print(f"Elements: {ne_active} active")

# --- Apply loads ---
# Find nearest node to column position
def nearest_node(xc, yc):
    best = None; best_d = 1e10
    for (i, j), name in nodes.items():
        d = ((i*de - xc)**2 + (j*de - yc)**2)**0.5
        if d < best_d:
            best_d = d; best = name
    return best

n_col1 = nearest_node(xC1, yC1)
n_col2 = nearest_node(xC2, yC2)
print(f"Col1 node: {n_col1}, Col2 node: {n_col2}")

# Point load: Fz (downward = negative Z)
model.PointObj.SetLoadForce(n_col1, 'DEAD', [0, 0, -P1, M1, 0, 0])
model.PointObj.SetLoadForce(n_col2, 'DEAD', [0, 0, -P2, M2, 0, 0])

# --- Run analysis ---
print("Running analysis...")
model.Analyze.SetRunCaseFlag('DEAD', True)
model.Analyze.RunAnalysis()
print("Done.")

# --- Read results ---
model.Results.Setup.DeselectAllCasesAndCombosForOutput()
model.Results.Setup.SetCaseSelectedForOutput('DEAD')

# Joint displacements
ret = model.Results.JointDispl(n_col1, 0)
if ret[0] == 0:
    nres = ret[1]
    print(f"\nCol1 displacement: w={ret[7][0]*1000:.4f} mm, rot_x={ret[8][0]*1000:.4f} mrad")

ret = model.Results.JointDispl(n_col2, 0)
if ret[0] == 0:
    print(f"Col2 displacement: w={ret[7][0]*1000:.4f} mm, rot_x={ret[8][0]*1000:.4f} mrad")

# Joint reactions (sum)
ret = model.Results.JointReact('', 2)  # GroupElm=2 means all
if ret[0] == 0:
    nres = ret[1]
    total_Rz = sum(ret[8])
    print(f"\nTotal Rz = {total_Rz:.2f} kN (applied: {P1+P2:.2f} kN)")

print("\n=== SAP2000 validation complete ===")
print("Compare with Calcpad HTML output.")
