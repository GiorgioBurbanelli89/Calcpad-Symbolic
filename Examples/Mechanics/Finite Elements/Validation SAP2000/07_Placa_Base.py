"""Validate Placa Base FEA: Calcpad CLI vs SAP2000.

Base plate 400x450x25mm, HSS 200x250 column
E=200000 MPa, v=0.3
P=600 kN axial, M=80 kN·m moment
Winkler foundation k=50000 kN/m³
4 anchor bolts at corners
Mesh: 8x8 Plate-Thick (Mindlin)
"""
import sys, os
sys.path.insert(0, r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Pipy Api sap2000 hekatan\src')
import pysap2000
import math

# --- Parameters ---
B = 0.400       # m (400 mm)
N = 0.450       # m (450 mm)
t = 0.025       # m (25 mm)
E_mpa = 200000  # MPa
E_kn = E_mpa * 1000  # kN/m^2
nu = 0.3
P = 600.0       # kN
M = 80.0        # kN*m
k_w = 50000.0   # kN/m^3
b_col = 0.200   # m (200 mm)
h_col = 0.250   # m (250 mm)
e_p = 0.040     # m (40 mm) bolt distance from edge
d_bolt = 0.020  # m (20 mm) bolt diameter
L_emb = 0.500   # m bolt embedded length
k_bolt = E_kn * math.pi * (d_bolt/2)**2 / L_emb  # kN/m
nx, ny = 8, 8
dx, dy = B/nx, N/ny

model = pysap2000.attach()
pysap2000.set_units('kN_m')
model.InitializeNewModel()
model.File.NewBlank()

# Material
model.PropMaterial.SetMaterial('STEEL', 1)
model.PropMaterial.SetMPIsotropic('STEEL', E_kn, nu, 0)

# Plate-Thick section
model.PropArea.SetShell_1('PLATE', 4, False, 'STEEL', 0, t, t)

# Nodes
n1 = nx + 1
nodes = {}
nid = 1
for j in range(ny + 1):
    for i in range(nx + 1):
        name = str(nid)
        model.PointObj.AddCartesian(i*dx, j*dy, 0, '', name)
        nodes[(i, j)] = name
        nid += 1
print(f"Nodes: {nid - 1}")

# Elements
eid = 1
for j in range(ny):
    for i in range(nx):
        pts = [nodes[(i,j)], nodes[(i+1,j)], nodes[(i+1,j+1)], nodes[(i,j+1)]]
        model.AreaObj.AddByPoint(4, pts, f'E{eid}', 'PLATE')
        eid += 1
print(f"Elements: {eid - 1}")

# Winkler springs (all nodes)
k_node = k_w * dx * dy  # kN/m per node
for j in range(ny + 1):
    for i in range(nx + 1):
        name = nodes[(i, j)]
        # Spring in UZ direction
        model.PointObj.SetSpring(name, [0, 0, k_node, 0, 0, 0])
print(f"Winkler: k_node = {k_node:.2f} kN/m")

# Anchor bolts at 4 corners
bolt_nodes = []
for ci, cj in [(0, 0), (nx, 0), (0, ny), (nx, ny)]:
    name = nodes[(ci, cj)]
    model.PointObj.SetSpring(name, [0, 0, k_bolt, 0, 0, 0])
    bolt_nodes.append(name)
print(f"Bolts: k_bolt = {k_bolt:.2f} kN/m at nodes {bolt_nodes}")

# Load: distributed on HSS perimeter nodes
model.LoadPatterns.Add('LOAD', 8, 0, True)

cx, cy = B/2, N/2
tol = dx / 2
n_weld = 0
weld_nodes = []
for j in range(ny + 1):
    for i in range(nx + 1):
        xi, yi = i*dx, j*dy
        dx_i = abs(xi - cx)
        dy_i = abs(yi - cy)
        on_x = dx_i > b_col/2 - tol and dx_i < b_col/2 + tol and dy_i < h_col/2 + tol
        on_y = dy_i > h_col/2 - tol and dy_i < h_col/2 + tol and dx_i < b_col/2 + tol
        if on_x or on_y:
            n_weld += 1
            weld_nodes.append((i, j, yi - cy))

print(f"Weld nodes: {n_weld}")

if n_weld > 0:
    P_node = P / n_weld
    M_arm = N / 2
    for i, j, dy_off in weld_nodes:
        name = nodes[(i, j)]
        fz = -P_node + M * dy_off / (n_weld * M_arm**2 / 3)
        model.PointObj.SetLoadForce(name, 'LOAD', [0, 0, fz, 0, 0, 0])

# Save and run
save_path = os.path.join(r'C:\CSiAPIexample', 'PlacaBase_8x8.sdb')
model.File.Save(save_path)
ret = model.Analyze.RunAnalysis()
print(f"Analysis: ret={ret}")

# Results
model.Results.Setup.DeselectAllCasesAndCombosForOutput()
model.Results.Setup.SetCaseSelectedForOutput('LOAD')

# Center deflection
cn = nodes[(nx//2, ny//2)]
NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]; OE=0
[NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret] = \
    model.Results.JointDispl(cn, OE, NR, Obj, Elm, AC, ST, SN, U1, U2, U3, R1, R2, R3)
w_center = U3[0]*1000 if NR > 0 else 0

# Max deflection
w_max = 0
for j in range(ny+1):
    for i in range(nx+1):
        name = nodes[(i,j)]
        NR=0;Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[];OE=0
        [NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret] = \
            model.Results.JointDispl(name,OE,NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3)
        if NR > 0 and abs(U3[0]) > abs(w_max/1000):
            w_max = U3[0]*1000

# Bolt forces
print(f"\n{'='*60}")
print(f"  PLACA BASE - SAP2000 Results")
print(f"{'='*60}")
print(f"  w_center = {w_center:.4f} mm")
print(f"  w_max = {w_max:.4f} mm")

for bn in bolt_nodes:
    NR=0;Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[];OE=0
    [NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret] = \
        model.Results.JointDispl(bn,OE,NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3)
    if NR > 0:
        f_bolt = -k_bolt * U3[0]  # kN (positive = tension)
        print(f"  Bolt {bn}: w={U3[0]*1000:.4f} mm, F={f_bolt:.2f} kN")

# Copy sdb to validation folder
import shutil
val_dir = r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Calcpad-Symbolic\Examples\Mechanics\Finite Elements\Validation SAP2000'
shutil.copy(save_path, os.path.join(val_dir, '07_Placa_Base_8x8.sdb'))
print(f"\nSaved to validation folder")
