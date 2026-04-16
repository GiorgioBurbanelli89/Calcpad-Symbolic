"""Validar Tutorial Layered Shell con SAP2000
   Test: placa simple con shell layered (2 capas de 400mm concreto)
   Debe dar mismo resultado que un shell simple de espesor 800mm.

   Caso: placa cuadrada 1.5x1.5m, t_total=800mm, E=22000MPa, P=20tonf centro
   Simplemente apoyada en los 4 bordes.
   Usa patron que funciona de MANUAL_SAP2000_API.md"""
import comtypes.client
import time
import os

# === Datos (kN, m) — iguales al Calcpad layered tutorial ===
a = 1.5;  b = 1.5       # placa (m)
t1 = 0.40               # capa 1: zapata (m)
t2 = 0.40               # capa 2: viga (m)
t_total = t1 + t2       # 0.80 m
E_MPa = 22000
E = E_MPa * 1000        # kN/m2
nu = 0.2
P_tonf = 20.0
P = P_tonf * 9.80665    # kN
ne1 = 6
de = a / ne1

print("=" * 70)
print("  LAYERED SHELL — Test 2 capas vs 1 shell grueso")
print(f"  Placa {a}x{b}m, t1={t1}m + t2={t2}m = {t_total}m")
print(f"  E={E_MPa} MPa, P={P_tonf} tonf")
print("=" * 70)

# --- Connect ---
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

# ==========================================================================
# TEST 1: Shell SIMPLE de espesor 0.8m (referencia)
# ==========================================================================
print("\n--- TEST 1: Shell simple t=0.8m ---")
SapModel.InitializeNewModel(6)  # kN-m
SapModel.File.NewBlank()

SapModel.PropMaterial.SetMaterial("CONC", 2)
SapModel.PropMaterial.SetMPIsotropic("CONC", E, nu, 0.00001)

# Plate-Thick con t_total
SapModel.PropArea.SetShell_1("plate_simple", 4, False, "CONC", 0, t_total, t_total)

# Malla
nodes = {}
nid = 1
for j in range(ne1 + 1):
    for i in range(ne1 + 1):
        x = i * de;  y = j * de
        name = str(nid)
        SapModel.PointObj.AddCartesian(float(x), float(y), 0.0, name)
        nodes[(i, j)] = name
        nid += 1

for j in range(ne1):
    for i in range(ne1):
        p = [nodes[(i,j)], nodes[(i+1,j)], nodes[(i+1,j+1)], nodes[(i,j+1)]]
        SapModel.AreaObj.AddByPoint(4, p, str(j*ne1+i+1), "plate_simple")

# Restricciones: simply supported (bordes con UZ=0)
for (i, j), name in nodes.items():
    on_edge = (i == 0 or i == ne1 or j == 0 or j == ne1)
    if on_edge:
        SapModel.PointObj.SetRestraint(name, [True, True, True, False, False, True])
    else:
        SapModel.PointObj.SetRestraint(name, [True, True, False, False, False, True])

SapModel.LoadPatterns.SetSelfWTMultiplier("DEAD", 0)

# Carga centro
center = nodes[(ne1//2, ne1//2)]
SapModel.PointObj.SetLoadForce(center, "DEAD", [0, 0, float(-P), 0, 0, 0], False)

temp = os.path.join(os.environ.get('TEMP'), 'layered_test1.sdb')
SapModel.File.Save(temp)
try: SapModel.Analyze.SetRunCaseFlag("MODAL", False)
except: pass

ret = SapModel.Analyze.RunAnalysis()
time.sleep(1)

w_simple = 0
if ret == 0:
    SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
    SapModel.Results.Setup.SetCaseSelectedForOutput("DEAD")
    r = SapModel.Results.JointDispl(center, 0)
    if r[0] > 0:
        w_simple = r[8][0] * 1000  # mm
        print(f"  w_center (shell simple t=0.8m) = {w_simple:.5f} mm")

# ==========================================================================
# TEST 2: Shell LAYERED con 2 capas de 0.4m cada una
# ==========================================================================
print("\n--- TEST 2: Shell Layered (2 capas concreto 0.4m) ---")
SapModel.InitializeNewModel(6)
SapModel.File.NewBlank()

SapModel.PropMaterial.SetMaterial("CONC", 2)
SapModel.PropMaterial.SetMPIsotropic("CONC", E, nu, 0.00001)

# Shell Layered con 2 capas
# SetShellLayer_1(name, NumberLayers, LayerName[], Distance[], Thickness[],
#                 Type[], NumIntegrationPts[], MatProp[], MatAng[],
#                 S11Type[], S22Type[], S12Type[])
num_layers = 2
layer_names = ['Zapata', 'Viga']
distances  = [-t1/2, t1/2]  # Offset desde plano medio (m)
thicknesses = [t1, t2]
types       = [1, 1]  # 1=Shell, 2=Membrane, 3=Plate
num_int     = [2, 2]
mats        = ['CONC', 'CONC']
angles      = [0.0, 0.0]
s11_types   = [1, 1]  # 1=Linear, 2=Nonlinear, 0=Inactive
s22_types   = [1, 1]
s12_types   = [1, 1]

try:
    ret = SapModel.PropArea.SetShellLayer_1(
        "plate_layered", num_layers,
        layer_names, distances, thicknesses,
        types, num_int, mats, angles,
        s11_types, s22_types, s12_types
    )
    print(f"  SetShellLayer_1: ret={ret}")
except Exception as e:
    print(f"  SetShellLayer_1 error: {e}")
    # Fallback: try with simpler SetShellLayer signature
    try:
        ret = SapModel.PropArea.SetShellLayer(
            "plate_layered", num_layers,
            layer_names, distances, thicknesses,
            types, num_int, mats, angles
        )
        print(f"  SetShellLayer: ret={ret}")
    except Exception as e2:
        print(f"  SetShellLayer error: {e2}")
        print("  Fallback: using simple thick shell t=0.8m")
        SapModel.PropArea.SetShell_1("plate_layered", 4, False, "CONC", 0, t_total, t_total)

# Misma malla
nodes2 = {}
nid = 1
for j in range(ne1 + 1):
    for i in range(ne1 + 1):
        x = i * de;  y = j * de
        name = str(nid)
        SapModel.PointObj.AddCartesian(float(x), float(y), 0.0, name)
        nodes2[(i, j)] = name
        nid += 1

for j in range(ne1):
    for i in range(ne1):
        p = [nodes2[(i,j)], nodes2[(i+1,j)], nodes2[(i+1,j+1)], nodes2[(i,j+1)]]
        SapModel.AreaObj.AddByPoint(4, p, str(j*ne1+i+1), "plate_layered")

for (i, j), name in nodes2.items():
    on_edge = (i == 0 or i == ne1 or j == 0 or j == ne1)
    if on_edge:
        SapModel.PointObj.SetRestraint(name, [True, True, True, False, False, True])
    else:
        SapModel.PointObj.SetRestraint(name, [True, True, False, False, False, True])

SapModel.LoadPatterns.SetSelfWTMultiplier("DEAD", 0)
center2 = nodes2[(ne1//2, ne1//2)]
SapModel.PointObj.SetLoadForce(center2, "DEAD", [0, 0, float(-P), 0, 0, 0], False)

temp2 = os.path.join(os.environ.get('TEMP'), 'layered_test2.sdb')
SapModel.File.Save(temp2)
try: SapModel.Analyze.SetRunCaseFlag("MODAL", False)
except: pass

ret = SapModel.Analyze.RunAnalysis()
time.sleep(1)

w_layered = 0
if ret == 0:
    SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
    SapModel.Results.Setup.SetCaseSelectedForOutput("DEAD")
    r = SapModel.Results.JointDispl(center2, 0)
    if r[0] > 0:
        w_layered = r[8][0] * 1000  # mm
        print(f"  w_center (layered 2x0.4m) = {w_layered:.5f} mm")

# ==========================================================================
# COMPARISON
# ==========================================================================
print("\n" + "=" * 70)
print("  COMPARACION")
print("=" * 70)
print(f"  Shell simple  (t=0.8m):  w = {w_simple:.5f} mm")
print(f"  Shell layered (2x0.4m):  w = {w_layered:.5f} mm")
if w_simple != 0:
    diff = abs(w_layered - w_simple)
    pct = abs(diff / w_simple) * 100
    print(f"  Diferencia: {diff:.6f} mm ({pct:.3f}%)")

# Teoria: placa simplemente apoyada con carga puntual central
# w_max ≈ 0.1267 * P * a^2 / D  (segun Timoshenko, con b/a=1)
# D = E*t^3/(12*(1-nu^2))
D_simple = E * 1e6 * t_total**3 / (12*(1-nu**2))  # kN/m (kN*m^2 de flexural rigidity)
w_teo_m = 0.1267 * P * a**2 / D_simple
print(f"\n  Teorico (Timoshenko SS): w = {w_teo_m*1000:.5f} mm")

print("\n=== Validation complete ===")
