"""Validar Tutorial MITC4 — Zapata 1.5x1.5m, 20 tonf, Joint Spring
   Usa el patron que funciona de Api sap 2000 examples/python/03_zapata_winkler_comparison.py
   Unidades: kN, m"""
import comtypes.client
import time
import os

# === Datos (kN, m) — iguales al tutorial Calcpad ===
a = 1.5        # m
b = 1.5        # m
t = 0.40       # m
E_MPa = 22000
E = E_MPa * 1000  # kN/m2
nu = 0.2
P_tonf = 20.0
P = P_tonf * 9.80665  # kN = 196.133 kN
ks = 30000  # kN/m3  (Calcpad: 0.03 N/mm3 = 30000 kN/m3)
nx = 6
ny = 6
dx = a / nx
dy = b / ny

print("=" * 70)
print("  ZAPATA 1.5x1.5m — SAP2000 Validation")
print(f"  E={E_MPa} MPa, t={t}m, ks={ks} kN/m3, P={P:.2f} kN")
print("=" * 70)

# Connect to SAP2000
try:
    mySapObject = comtypes.client.GetActiveObject("CSI.SAP2000.API.SapObject")
    SapModel = mySapObject.SapModel
    _ = SapModel.GetModelIsLocked()
    print("Connected to existing SAP2000")
except:
    print("Starting SAP2000...")
    helper = comtypes.client.CreateObject('SAP2000v1.Helper')
    helper = helper.QueryInterface(comtypes.gen.SAP2000v1.cHelper)
    mySapObject = helper.CreateObjectProgID("CSI.SAP2000.API.SapObject")
    mySapObject.ApplicationStart(6, True)
    time.sleep(8)
    SapModel = mySapObject.SapModel
    print("SAP2000 started")

# Initialize with kN-m units (unit code 6)
SapModel.InitializeNewModel(6)
SapModel.File.NewBlank()

# Material
SapModel.PropMaterial.SetMaterial("CONC", 2)
SapModel.PropMaterial.SetMPIsotropic("CONC", E, nu, 0.00001)

# Shell: Plate-Thick type = 4
SapModel.PropArea.SetShell_1("ZAP", 4, False, "CONC", 0, t, t)

# Nodes (centered at origin)
node_map = {}
for j in range(ny + 1):
    for i in range(nx + 1):
        x = -a/2 + (i / nx) * a
        y = -b/2 + (j / ny) * b
        nid = str(j * (nx + 1) + i + 1)
        SapModel.PointObj.AddCartesian(float(x), float(y), 0.0, nid)
        node_map[(i, j)] = nid

# Shell elements
for j in range(ny):
    for i in range(nx):
        p = [node_map[(i,j)], node_map[(i+1,j)],
             node_map[(i+1,j+1)], node_map[(i,j+1)]]
        aname = str(j * nx + i + 1)
        SapModel.AreaObj.AddByPoint(4, p, aname, "ZAP")

print(f"Mesh: {SapModel.PointObj.Count()} nodes, {SapModel.AreaObj.Count()} elements")

# Joint Springs at each node (Winkler)
for j in range(ny + 1):
    for i in range(nx + 1):
        A_trib = dx * dy
        on_edge_x = (i == 0 or i == nx)
        on_edge_y = (j == 0 or j == ny)
        if on_edge_x and on_edge_y:
            A_trib = dx * dy / 4
        elif on_edge_x or on_edge_y:
            A_trib = dx * dy / 2
        kz = ks * A_trib
        nid = node_map[(i, j)]
        SapModel.PointObj.SetSpring(nid, [0.0, 0.0, float(kz), 0.0, 0.0, 0.0])

# Anti-rotation restraints
nudo_centro = node_map[(nx//2, ny//2)]
SapModel.PointObj.SetRestraint(nudo_centro, [True, True, False, False, False, False])
SapModel.PointObj.SetRestraint(node_map[(0, 0)], [True, False, False, False, False, False])

# Remove self-weight
SapModel.LoadPatterns.SetSelfWTMultiplier("DEAD", 0)

# Point load at center
SapModel.PointObj.SetLoadForce(nudo_centro, "DEAD", [0.0, 0.0, float(-P), 0.0, 0.0, 0.0], False)
print(f"Load P={P:.2f} kN at center node {nudo_centro}")

# Save
temp_path = os.path.join(os.environ.get('TEMP', r'C:\Temp'), 'zapata_mitc4_test.sdb')
SapModel.File.Save(temp_path)

# Disable modal
try:
    SapModel.Analyze.SetRunCaseFlag("MODAL", False)
except:
    pass

print("Running analysis...")
ret = SapModel.Analyze.RunAnalysis()
time.sleep(2)

if ret == 0:
    SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
    SapModel.Results.Setup.SetCaseSelectedForOutput("DEAD")

    # Center displacement
    result = SapModel.Results.JointDispl(nudo_centro, 0)
    if result[0] > 0:
        w_sap_m = result[8][0]  # U3 in meters
        w_sap_mm = w_sap_m * 1000
        print(f"\n=== RESULTADOS ===")
        print(f"SAP2000  w_center = {w_sap_mm:.4f} mm")
        print(f"Calcpad  w_center = -2.95408 mm")
        diff = abs(w_sap_mm - (-2.95408))
        pct = abs(diff / 2.95408) * 100
        print(f"Diff = {diff:.4f} mm ({pct:.2f}%)")

        # Max soil pressure
        q_kN_m2 = ks * w_sap_m  # kN/m2
        q_tonf_m2 = q_kN_m2 / 9.80665
        print(f"\nSAP2000  q_center = {q_tonf_m2:.2f} tonf/m2")
        print(f"Calcpad  q_max = -9.04 tonf/m2")

        # All nodes
        print(f"\n--- Perfil de asentamientos (mm) ---")
        print(f"{'i':>3} {'j':>3} {'x':>6} {'y':>6} {'w(mm)':>10}")
        for j in range(ny+1):
            for i in range(nx+1):
                nid = node_map[(i,j)]
                r = SapModel.Results.JointDispl(nid, 0)
                if r[0] > 0:
                    w = r[8][0] * 1000
                    x = -a/2 + (i/nx)*a
                    y = -b/2 + (j/ny)*b
                    print(f"{i:>3} {j:>3} {x:>6.2f} {y:>6.2f} {w:>10.4f}")
    else:
        print(f"No results: result[0]={result[0]}")
else:
    print(f"Analysis failed: ret={ret}")

print("\n=== Done ===")
