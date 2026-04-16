"""Zapata Lindero Vivienda — Viga como FRAME + Zapatas como SHELL
   Winkler Area Spring. Datos sincronizados con Calcpad.
   Usa pysap2000 (comtypes COM)."""
import sys, os, time, math
sys.path.insert(0, r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Pipy Api sap2000 hekatan\src')
import pysap2000

# === Datos (N, mm) — iguales a Calcpad ===
E = 22000.0       # MPa = N/mm^2
nu = 0.2
t_zap = 400.0     # mm
bv = 300.0        # mm (ancho viga)
hv = 400.0        # mm (peralte viga)
ks = 0.03         # N/mm^3 (= 30000 kN/m^3)

# Zapata lindero
Lz1 = 1000.0; Bz1 = 1500.0; Lc1 = 300.0
xC1 = 150.0; yC1 = 750.0

# Viga
Lv = 2000.0

# Zapata aislada
Lz2 = 1500.0; Bz2 = 1500.0; Lc2 = 300.0
xC2 = Lz1 + Lv + Lz2/2; yC2 = Bz2/2

Bmax = max(Bz1, Bz2)

# Cargas (N, N*mm)
P1 = 20.0 * 9806.65 * 1000  # mN -> N: 20 tf = 196133 N
P2 = 20.0 * 9806.65 * 1000
Mx1 = 0.2 * 9806.65 * 1e6   # N*mm
My1 = 0.15 * 9806.65 * 1e6
Mx2 = 0.2 * 9806.65 * 1e6
My2 = 0.15 * 9806.65 * 1e6

de = 250.0  # mesh size mm

print("=== Zapata Lindero — Frame Beam + Shell ===")
print(f"P1={P1:.0f}N, P2={P2:.0f}N, de={de}mm")

# --- SAP2000 ---
model = pysap2000.attach()
pysap2000.set_units('N_mm')
model.InitializeNewModel()
model.File.NewBlank()

# Material
model.PropMaterial.SetMaterial('CONC', 2)
model.PropMaterial.SetMPIsotropic('CONC', E, nu, 0)

# Shell section (thick)
model.PropArea.SetShell_1('zapata', 2, False, 'CONC', 0, t_zap, t_zap)

# Frame section (rectangular)
model.PropFrame.SetRectangle('viga', 'CONC', hv, bv)

# --- Nodes for zapatas ---
nx1 = int(Lz1/de); ny = int(Bmax/de)
nx2 = int(Lz2/de)

nodes = {}
nid = 1

# Zapata 1
for j in range(ny+1):
    for i in range(nx1+1):
        name = str(nid)
        model.PointObj.AddCartesian(i*de, j*de, 0, '', name)
        nodes[('z1', i, j)] = name; nid += 1

# Zapata 2
for j in range(ny+1):
    for i in range(nx2+1):
        name = str(nid)
        model.PointObj.AddCartesian(Lz1+Lv+i*de, j*de, 0, '', name)
        nodes[('z2', i, j)] = name; nid += 1

print(f"Nodes: {nid-1}")

# --- Shell elements ---
ne = 0
for j in range(ny):
    for i in range(nx1):
        n1 = nodes[('z1',i,j)]; n2 = nodes[('z1',i+1,j)]
        n3 = nodes[('z1',i+1,j+1)]; n4 = nodes[('z1',i,j+1)]
        ename = f"SZ1_{ne}"
        model.AreaObj.AddByPoint(4, [n1,n2,n3,n4], '', 'zapata', ename)
        model.AreaObj.SetSpringAssignment(ename, 1, ks, 1, '', 6, '', True)
        ne += 1

for j in range(ny):
    for i in range(nx2):
        n1 = nodes[('z2',i,j)]; n2 = nodes[('z2',i+1,j)]
        n3 = nodes[('z2',i+1,j+1)]; n4 = nodes[('z2',i,j+1)]
        ename = f"SZ2_{ne}"
        model.AreaObj.AddByPoint(4, [n1,n2,n3,n4], '', 'zapata', ename)
        model.AreaObj.SetSpringAssignment(ename, 1, ks, 1, '', 6, '', True)
        ne += 1

print(f"Shell elements: {ne}")

# --- Frame beam (viga de amarre) ---
# Find nearest nodes to column centers
def find_node(zone, xc, yc):
    best = None; bd = 1e10
    prefix = 'z1' if zone == 1 else 'z2'
    for (z, i, j), name in nodes.items():
        if z != prefix: continue
        if zone == 1:
            x = i*de; y = j*de
        else:
            x = Lz1+Lv+i*de; y = j*de
        d = math.hypot(x-xc, y-yc)
        if d < bd: bd = d; best = name
    return best

n_col1 = find_node(1, xC1, yC1)
n_col2 = find_node(2, xC2, yC2)
print(f"Col1 node: {n_col1}, Col2 node: {n_col2}")

# Create frame from col1 to col2
model.FrameObj.AddByPoint(n_col1, n_col2, '', 'viga', 'VIGA')
print("Frame beam 'VIGA' created.")

# --- Loads ---
model.PointObj.SetLoadForce(n_col1, 'DEAD', [0, 0, -P1, Mx1, My1, 0])
model.PointObj.SetLoadForce(n_col2, 'DEAD', [0, 0, -P2, Mx2, My2, 0])

# --- Run ---
print("Running analysis...")
model.Analyze.SetRunCaseFlag('DEAD', True)
model.Analyze.RunAnalysis()

# --- Results ---
model.Results.Setup.DeselectAllCasesAndCombosForOutput()
model.Results.Setup.SetCaseSelectedForOutput('DEAD')

ret = model.Results.JointDispl(n_col1, 0)
if ret[0] == 0 and ret[1] > 0:
    print(f"\nCol1: w={ret[7][0]:.4f} mm, rx={ret[8][0]:.6f} rad, ry={ret[9][0]:.6f} rad")

ret = model.Results.JointDispl(n_col2, 0)
if ret[0] == 0 and ret[1] > 0:
    print(f"Col2: w={ret[7][0]:.4f} mm, rx={ret[8][0]:.6f} rad, ry={ret[9][0]:.6f} rad")

print("\n=== Compare with Calcpad ===")
print("Calcpad: w_col1 = -5.8655 mm, w_col2 = -1.8474 mm")
print("SAP2000 values above should match.")
