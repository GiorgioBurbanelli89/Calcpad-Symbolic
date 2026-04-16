"""Crear modelo SAP2000 solo placa base 2D para comparar con DKQ.
   Misma malla 9x9, Winkler compresion-only via area springs,
   pernos como point springs, cargas en nodos del perfil.
   Leer resultados: deflexion, momentos, Von Mises."""
import comtypes.client
import numpy as np

# === Datos ===
E = 200000.0  # MPa
nu = 0.3
t = 50.0  # mm
Pu = 148.0 * 9806.65  # N
Mu = 50.0 * 9806650.0  # N*mm
kb = E * np.pi * (25.4/2)**2 / 500.0  # N/mm
kw = 50000 / 1e6  # N/mm^3

# Malla (SAP coords: X=500, Y=600)
sap_x = [0, 50, 100, 175, 250, 325, 400, 450, 500]
sap_y = [0, 50, 112, 174, 300, 426, 488, 550, 600]

# Connect to SAP
try:
    sap = comtypes.client.GetActiveObject("CSI.SAP2000.API.SapObject")
    print("Conectado a SAP2000")
except:
    print("Abriendo SAP2000...")
    helper = comtypes.client.CreateObject("SAP2000v1.Helper")
    helper = helper.QueryInterface(comtypes.gen.SAP2000v1.cHelper)
    sap = helper.CreateObjectProgID("CSI.SAP2000.API.SapObject")
    sap.ApplicationStart()

sm = sap.SapModel

# New model, N-mm units
sm.InitializeNewModel(6)  # 6 = N-mm-C
sm.File.NewBlank()

# === Create joints ===
joint_map = {}  # (i,j) -> joint_name
jcount = 0
for j, sy in enumerate(sap_y):
    for i, sx in enumerate(sap_x):
        jcount += 1
        jname = str(jcount)
        sm.PointObj.AddCartesian(float(sx), float(sy), 0.0, '', jname)
        joint_map[(i,j)] = jname

print(f"Creados {jcount} joints")

# === Define shell section e50 ===
sm.PropArea.SetShell_1('e50', 1, False, 'A36', 0, t, t)

# Define A36 material
sm.MatProp.SetMaterial('A36', 1)  # 1 = steel
sm.MatProp.SetMPIsotropic('A36', E, nu, 1.17e-5)

# === Create area elements ===
ecount = 0
for j in range(8):
    for i in range(8):
        j1 = joint_map[(i, j)]
        j2 = joint_map[(i+1, j)]
        j3 = joint_map[(i+1, j+1)]
        j4 = joint_map[(i, j+1)]
        ecount += 1
        ename = str(ecount)
        sm.AreaObj.AddByPoint(4, [j1, j2, j3, j4], '', 'e50', ename)

print(f"Creados {ecount} areas")

# === Winkler springs (area springs on each element) ===
# Using AreaObj.SetSpringAssignment for compression-only
# Type 1 = simple spring, face 6 = bottom face
for j in range(8):
    for i in range(8):
        ename = str(j*8 + i + 1)
        # SetSpring: name, springType, stiffness, springSimpleType, csys, direction
        # direction 1=local1, face=6 bottom
        ret = sm.AreaObj.SetSpring(ename, 1, kw*1e6, 0, '', 0, 6, 0, False)

print(f"Winkler springs asignados (kw={kw*1e6} N/m^3)")

# === Point springs for bolts ===
bolt_sap = [(50,50),(175,50),(325,50),(450,50),
            (50,174),(175,174),(325,174),(450,174),
            (50,426),(175,426),(325,426),(450,426),
            (50,550),(175,550),(325,550),(450,550)]

# Find nearest joint for each bolt
for bsx, bsy in bolt_sap:
    best_name = None
    best_d2 = 1e18
    for (i,j), jname in joint_map.items():
        d2 = (sap_x[i]-bsx)**2 + (sap_y[j]-bsy)**2
        if d2 < best_d2:
            best_d2 = d2
            best_name = jname
    # Point spring in Z direction (local 3)
    # SetSpring: name, stiffness[6], itemType, isCouple, replace
    kvals = [0.0]*6
    kvals[2] = kb  # UZ spring
    sm.PointObj.SetSpring(best_name, kvals, 0, False, True)

print(f"Pernos: {len(bolt_sap)} point springs (kb={kb:.0f} N/mm)")

# === Load pattern ===
sm.LoadPatterns.Add('CARGA', 1, 0, True)

# === Apply loads at profile nodes ===
# Profile footprint (SAP coords):
# Alas: SAP Y=112 and Y=488, SAP X=100..400
# Alma: SAP X=250, SAP Y=112..488

# Ala inf (SAP Y=112, j=2): i=2,3,4,5,6
ala_inf_joints = [joint_map[(i,2)] for i in range(2,7)]
# Ala sup (SAP Y=488, j=6): i=2,3,4,5,6
ala_sup_joints = [joint_map[(i,6)] for i in range(2,7)]
# Alma (SAP X=250, i=4): j=2,3,4,5,6
alma_joints = [joint_map[(4,j)] for j in range(2,7)]

all_prof = sorted(set(ala_inf_joints + ala_sup_joints + alma_joints))
np_nodes = len(all_prof)
fp = -Pu / np_nodes  # downward (negative Z)

print(f"\nPerfil: {np_nodes} nodos")
print(f"fp = {fp:.0f} N = {fp/9806.65:.2f} tonf por nodo")

brazo = 488 - 112  # 376mm
Fm = Mu / brazo
fm = Fm / 5

print(f"brazo = {brazo} mm, fm = {fm:.0f} N = {fm/9806.65:.2f} tonf")

# Apply axial load to all profile nodes
for jname in all_prof:
    sm.PointObj.SetLoadForce(jname, 'CARGA', [0,0,fp,0,0,0], True)

# Apply moment as couple on flanges
for jname in ala_inf_joints:
    sm.PointObj.SetLoadForce(jname, 'CARGA', [0,0,-fm,0,0,0], True)  # extra down
for jname in ala_sup_joints:
    sm.PointObj.SetLoadForce(jname, 'CARGA', [0,0,fm,0,0,0], True)   # up (less down)

print("Cargas aplicadas")

# === Analysis settings ===
sm.Analyze.SetRunCaseFlag('DEAD', False)
sm.Analyze.SetRunCaseFlag('CARGA', True)

# Save
save_path = r"C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Calcpad-Symbolic\Examples\Mechanics\Finite Elements\Validation SAP2000\07_Placa_Base_2D_compare.sdb"
sm.File.Save(save_path)
print(f"\nModelo guardado: {save_path}")
print("\n*** Ejecuta el analisis manualmente en SAP2000 ***")
print("*** Luego corre sap_read_results_2D.py para leer resultados ***")
