"""Validate Calcpad CLI membrane wall vs SAP2000.

Wall: 5m x 3m x 0.2m, E=25000 MPa, v=0.2
Load: 100 kN lateral at top (distributed to top nodes)
Base: fixed (u=v=0)
Mesh: 8x6 Q4 membrane elements (plane stress)
"""
import sys
sys.path.insert(0, r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Pipy Api sap2000 hekatan\src')
import pysap2000

# --- Parameters (same as Calcpad CLI) ---
W = 5.0      # m
H = 3.0      # m
t = 0.2      # m
E = 25000.0  # MPa
nu = 0.2
P = 100.0    # kN
nx = 8
ny = 6
dx = W / nx
dy = H / ny

# --- Connect ---
model = pysap2000.attach()
pysap2000.set_units('kN_m')

# --- New blank model ---
model.InitializeNewModel()
model.File.NewBlank()

# --- Material ---
MATERIAL_CONCRETE = 2
model.PropMaterial.SetMaterial('CONC', MATERIAL_CONCRETE)
model.PropMaterial.SetMPIsotropic('CONC', E * 1000, nu, 0)  # E in kN/m^2

# --- Shell section (membrane only) ---
# ShellType: 1=Shell-Thin, 2=Shell-Thick, 3=Plate-Thin, 4=Plate-Thick, 5=Membrane
model.PropArea.SetShell_1('MEMBRANE', 5, False, 'CONC', 0, t, t)

# --- Generate mesh nodes ---
n1 = nx + 1  # nodes per row
nodes = {}
node_id = 1
for j in range(ny + 1):
    for i in range(nx + 1):
        x = i * dx
        y = 0.0
        z = j * dy
        name = str(node_id)
        model.PointObj.AddCartesian(x, y, z, '', name)
        nodes[(i, j)] = name
        node_id += 1

print(f"Nodes: {node_id - 1}")

# --- Generate Q4 elements ---
elem_id = 1
for j in range(ny):
    for i in range(nx):
        n1_name = nodes[(i, j)]
        n2_name = nodes[(i+1, j)]
        n3_name = nodes[(i+1, j+1)]
        n4_name = nodes[(i, j+1)]
        ename = f'E{elem_id}'
        model.AreaObj.AddByPoint(4, [n1_name, n2_name, n3_name, n4_name], ename, 'MEMBRANE')
        elem_id += 1

print(f"Elements: {elem_id - 1}")

# --- Restraints (base: j=0, all i) ---
for i in range(nx + 1):
    name = nodes[(i, 0)]
    model.PointObj.SetRestraint(name, [True, True, True, True, True, True])

print(f"Base restrained: {nx + 1} nodes")

# --- Load pattern ---
model.LoadPatterns.Add('LATERAL', 8, 0, True)

# --- Apply lateral load at top nodes ---
n_top = nx + 1
p_per_node = P / n_top
for i in range(nx + 1):
    name = nodes[(i, ny)]
    # Force in X direction (lateral)
    model.PointObj.SetLoadForce(name, 'LATERAL', [p_per_node, 0, 0, 0, 0, 0])

print(f"Load: {P} kN distributed to {n_top} top nodes ({p_per_node:.4f} kN each)")

# --- Save and run ---
import os
save_dir = r'C:\CSiAPIexample'
os.makedirs(save_dir, exist_ok=True)
save_path = os.path.join(save_dir, 'membrane_wall_validation.sdb')
model.File.Save(save_path)
print(f"Saved: {save_path}")

ret = model.Analyze.RunAnalysis()
print(f"Analysis run: ret={ret}")

# --- Get results: max horizontal displacement at top ---
model.Results.Setup.DeselectAllCasesAndCombosForOutput()
model.Results.Setup.SetCaseSelectedForOutput('LATERAL')

u_max = 0.0
u_node = ''
for i in range(nx + 1):
    name = nodes[(i, ny)]
    NumberResults = 0
    Obj = Elm = ACase = StepType = StepNum = []
    U1 = U2 = U3 = R1 = R2 = R3 = []
    ObjectElm = 0

    [NumberResults, Obj, Elm, ACase, StepType, StepNum,
     U1, U2, U3, R1, R2, R3, ret] = model.Results.JointDispl(
        name, ObjectElm, NumberResults, Obj, Elm, ACase,
        StepType, StepNum, U1, U2, U3, R1, R2, R3)

    if NumberResults > 0 and abs(U1[0]) > abs(u_max):
        u_max = U1[0]
        u_node = name

print(f"\n{'='*50}")
print(f"SAP2000 Results:")
print(f"  Max horizontal displacement: {u_max:.6f} m = {u_max*1000:.4f} mm")
print(f"  At node: {u_node}")
print(f"{'='*50}")

# --- Calcpad CLI result (from previous run) ---
# TODO: read from Calcpad CLI output
print(f"\nCompare with Calcpad CLI result manually.")
print(f"Run: Cli.exe 'Plate Theory - Membrane Q4.cpd'")
