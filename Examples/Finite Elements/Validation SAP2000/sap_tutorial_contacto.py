"""Validar Tutorial Contacto Placas con SAP2000
   2 placas (0.4m + 0.3m) con contacto via Link GAP.
   Placa 1 apoyada en bordes, placa 2 con carga centrada 20 tonf.
   Contacto compression-only entre todas las parejas de nudos coincidentes."""
import comtypes.client
import time
import os

# === Datos (kN, m) — iguales al tutorial Calcpad ===
L = 1.5; B = 1.5
t1 = 0.40  # placa 1 (abajo)
t2 = 0.30  # placa 2 (encima)
E_MPa = 22000
E = E_MPa * 1000
nu = 0.2
P_tonf = 20.0
P = P_tonf * 9.80665
ne1 = 4
de = L / ne1
# Para GAP en SAP: rigidez muy alta = 1e8 N/mm = 1e5 kN/m
k_contact = 1e8   # kN/m (valor muy alto)

print("=" * 70)
print("  CONTACTO ENTRE PLACAS — Validacion SAP2000 con Link GAP")
print(f"  Placa 1: {L}x{B}m, t={t1}m")
print(f"  Placa 2: {L}x{B}m, t={t2}m (encima)")
print(f"  Contacto compression-only (Link GAP)")
print(f"  P={P_tonf} tonf en centro de placa 2")
print("=" * 70)

# Conectar
try:
    mySapObject = comtypes.client.GetActiveObject("CSI.SAP2000.API.SapObject")
    SapModel = mySapObject.SapModel
    _ = SapModel.GetModelIsLocked()
    print("Conectado a SAP2000")
except:
    print("Iniciando SAP2000...")
    helper = comtypes.client.CreateObject('SAP2000v1.Helper')
    helper = helper.QueryInterface(comtypes.gen.SAP2000v1.cHelper)
    mySapObject = helper.CreateObjectProgID("CSI.SAP2000.API.SapObject")
    mySapObject.ApplicationStart(6, True)
    time.sleep(8)
    SapModel = mySapObject.SapModel

SapModel.InitializeNewModel(6)
SapModel.File.NewBlank()

# Material
SapModel.PropMaterial.SetMaterial("CONC", 2)
SapModel.PropMaterial.SetMPIsotropic("CONC", E, nu, 0.00001)

# Dos secciones shell-thick
SapModel.PropArea.SetShell_1("placa_1", 4, False, "CONC", 0, t1, t1)
SapModel.PropArea.SetShell_1("placa_2", 4, False, "CONC", 0, t2, t2)

# === PLACA 1 (Z=0) ===
# Nudos
nodes_p1 = {}
nid = 1
for j in range(ne1 + 1):
    for i in range(ne1 + 1):
        x = i * de; y = j * de; z = 0.0
        name = f"1_{nid}"
        SapModel.PointObj.AddCartesian(float(x), float(y), float(z), name)
        nodes_p1[(i, j)] = name
        nid += 1

# Shell
for j in range(ne1):
    for i in range(ne1):
        p = [nodes_p1[(i,j)], nodes_p1[(i+1,j)],
             nodes_p1[(i+1,j+1)], nodes_p1[(i,j+1)]]
        SapModel.AreaObj.AddByPoint(4, p, f"P1_{j*ne1+i+1}", "placa_1")

# Bordes apoyados
for (i, j), name in nodes_p1.items():
    on_edge = (i == 0 or i == ne1 or j == 0 or j == ne1)
    if on_edge:
        SapModel.PointObj.SetRestraint(name, [True, True, True, False, False, True])
    else:
        SapModel.PointObj.SetRestraint(name, [True, True, False, False, False, True])

# === PLACA 2 (Z = t1 + small offset para separacion visual) ===
z2 = t1 + 0.001  # pequeño offset para poder ver las dos capas
nodes_p2 = {}
nid = 1
for j in range(ne1 + 1):
    for i in range(ne1 + 1):
        x = i * de; y = j * de; z = z2
        name = f"2_{nid}"
        SapModel.PointObj.AddCartesian(float(x), float(y), float(z), name)
        nodes_p2[(i, j)] = name
        nid += 1

for j in range(ne1):
    for i in range(ne1):
        p = [nodes_p2[(i,j)], nodes_p2[(i+1,j)],
             nodes_p2[(i+1,j+1)], nodes_p2[(i,j+1)]]
        SapModel.AreaObj.AddByPoint(4, p, f"P2_{j*ne1+i+1}", "placa_2")

# Placa 2: sin apoyos directos (solo restringir UX, UY, RZ para estabilidad)
for (i, j), name in nodes_p2.items():
    SapModel.PointObj.SetRestraint(name, [True, True, False, False, False, True])

# === Link GAP (Compression-Only) ===
# SetGap(name, DOF[6], Fixed[6], Ke[6], Ce[6], Open[6], KeValues[6], DampValues[6], OpenValues[6], Dir, nonlinear, stiffness)
# Usamos Link con U1 (vertical) gap
try:
    # Gap: nonlinear link con "Compression Only" en U3
    ret = SapModel.PropLink.SetGap(
        "GAP_VERT",
        [False, False, True, False, False, False],   # DOF activos: solo U3
        [False, False, False, False, False, False],  # No fixed
        [0, 0, k_contact, 0, 0, 0],                   # Linear stiffness
        [0]*6,                                        # No damping
        [0, 0, 0, 0, 0, 0],                           # Initial gap = 0
        [0, 0, k_contact, 0, 0, 0],                   # Nonlinear stiffness
        [0]*6,
        [0, 0, 0, 0, 0, 0],                           # Nonlinear gap
        0, True, 0
    )
    print(f"SetGap: {ret}")
except Exception as e:
    print(f"SetGap signature failed: {e}")
    # Try simpler version
    try:
        ret = SapModel.PropLink.SetLinear(
            "GAP_VERT",
            [False, False, True, False, False, False],
            [False]*6,
            [0, 0, k_contact, 0, 0, 0],
            [0]*6,
            0, 0
        )
        print(f"SetLinear fallback: {ret}")
    except Exception as e2:
        print(f"SetLinear error: {e2}")

# Crear links entre cada par de nudos coincidentes
for (i, j) in nodes_p1:
    n1 = nodes_p1[(i, j)]
    n2 = nodes_p2[(i, j)]
    link_name = f"L_{i}_{j}"
    try:
        SapModel.LinkObj.AddByPoint(n1, n2, link_name, False, "GAP_VERT")
    except Exception as e:
        pass

# Carga
SapModel.LoadPatterns.SetSelfWTMultiplier("DEAD", 0)
center_p2 = nodes_p2[(ne1//2, ne1//2)]
SapModel.PointObj.SetLoadForce(center_p2, "DEAD", [0, 0, float(-P), 0, 0, 0], False)
print(f"Load on node {center_p2}")

# Guardar y analizar
temp = os.path.join(os.environ.get('TEMP'), 'contacto_test.sdb')
SapModel.File.Save(temp)

try: SapModel.Analyze.SetRunCaseFlag("MODAL", False)
except: pass

print("Running analysis...")
ret = SapModel.Analyze.RunAnalysis()
time.sleep(2)

# Resultados
if ret == 0:
    SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
    SapModel.Results.Setup.SetCaseSelectedForOutput("DEAD")

    # Deflexion en centro de placa 2
    r = SapModel.Results.JointDispl(center_p2, 0)
    if r[0] > 0:
        w_p2 = r[8][0] * 1000
        print(f"\nw placa 2 (centro) = {w_p2:.5f} mm")

    # Deflexion en centro de placa 1
    center_p1 = nodes_p1[(ne1//2, ne1//2)]
    r = SapModel.Results.JointDispl(center_p1, 0)
    if r[0] > 0:
        w_p1 = r[8][0] * 1000
        print(f"w placa 1 (centro) = {w_p1:.5f} mm")
else:
    print(f"Analysis failed: ret={ret}")

print("\n=== Done ===")
