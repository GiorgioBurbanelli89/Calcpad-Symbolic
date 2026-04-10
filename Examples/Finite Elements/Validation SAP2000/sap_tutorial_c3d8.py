"""Validar Tutorial C3D8 (Solido 3D hexaedro) vs SAP2000 Solid element.

Tutorial Calcpad: Cubo 1000x1000x1000 mm, E=22000 MPa, nu=0.2
- Base fija (UZ=0 en 4 nudos inferiores)
- Carga compresiva P_total=100 kN distribuida en 4 nudos superiores (25 kN/nudo)
- Esperado: w = sigma*L/E = -P/(A*E)*L = -100000/(1000*1000*22000)*1000 = -0.004545 mm

Patron: template_zapata_winkler.py"""
import comtypes.client
import time
import os

# === Datos (N, mm para Calcpad, convertidos a kN, m para SAP) ===
L_mm = 1000.0
L = L_mm / 1000.0          # 1 m
E_MPa = 22000
E = E_MPa * 1000           # kN/m2
nu = 0.2
P_total = 100.0            # kN (100000 N)
P_per_node = P_total / 4   # 25 kN por nudo superior

print("=" * 70)
print("  C3D8 CUBO 1x1x1m — Validacion SAP2000 Solid Element")
print(f"  E={E_MPa} MPa, nu={nu}")
print(f"  Carga total: {P_total} kN (compresion) en 4 nudos superiores")
print("=" * 70)

# --- Conectar ---
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

# Iniciar modelo nuevo en kN-m
SapModel.InitializeNewModel(6)
SapModel.File.NewBlank()

# Material
SapModel.PropMaterial.SetMaterial("CONC", 2)  # 2 = Concrete
SapModel.PropMaterial.SetMPIsotropic("CONC", E, nu, 0.00001)

# Seccion Solid
# SetProp(name, MatProp, MatAng1, MatAng2, MatAng3, IsDrilling)
try:
    ret = SapModel.PropSolid.SetProp("SOLID_CONC", "CONC", 0, 0, 0, False)
    print(f"PropSolid.SetProp: {ret}")
except Exception as e:
    print(f"PropSolid error: {e}")

# 8 nudos del cubo (convencion Abaqus C3D8):
# 1,2,3,4 base (z=0), 5,6,7,8 top (z=L)
nodes = {
    1: (0, 0, 0),
    2: (L, 0, 0),
    3: (L, L, 0),
    4: (0, L, 0),
    5: (0, 0, L),
    6: (L, 0, L),
    7: (L, L, L),
    8: (0, L, L),
}

for nid, (x, y, z) in nodes.items():
    SapModel.PointObj.AddCartesian(float(x), float(y), float(z), str(nid))

print(f"Nudos: {len(nodes)}")

# Elemento Solid (1 hexaedro con los 8 nudos)
# AddByPoint(Point[8], Name, PropName) — orden Abaqus C3D8
solid_nodes = ['1', '2', '3', '4', '5', '6', '7', '8']
try:
    ret = SapModel.SolidObj.AddByPoint(solid_nodes, "S1", "SOLID_CONC")
    print(f"SolidObj.AddByPoint: {ret}")
except Exception as e:
    print(f"SolidObj error: {e}")

# Restricciones
# Solid element usa solo UX, UY, UZ — rotaciones RX, RY, RZ deben restringirse
# en todos los nudos para evitar singularidad.
# Todos los nudos: RX=RY=RZ=0
for nid in ['1', '2', '3', '4', '5', '6', '7', '8']:
    SapModel.PointObj.SetRestraint(nid,
        [False, False, False, True, True, True])

# Base (1,2,3,4): tambien UZ=0
for nid in ['1', '2', '3', '4']:
    SapModel.PointObj.SetRestraint(nid,
        [False, False, True, True, True, True])

# Nudo 1: UX=UY=UZ=0 (fija 3 traslaciones)
SapModel.PointObj.SetRestraint('1',
    [True, True, True, True, True, True])
# Nudo 2: UY=UZ=0 (fija rotacion del sistema en Z y X)
SapModel.PointObj.SetRestraint('2',
    [False, True, True, True, True, True])

# Cargas: 25 kN en cada nudo superior (5,6,7,8), direccion -Z
SapModel.LoadPatterns.SetSelfWTMultiplier("DEAD", 0)
for nid in ['5', '6', '7', '8']:
    SapModel.PointObj.SetLoadForce(nid, "DEAD",
        [0.0, 0.0, float(-P_per_node), 0.0, 0.0, 0.0], False)

# Guardar
temp = os.path.join(os.environ.get('TEMP'), 'c3d8_test.sdb')
SapModel.File.Save(temp)

# Correr
try:
    SapModel.Analyze.SetRunCaseFlag("MODAL", False)
except:
    pass

print("\nRunning analysis...")
ret = SapModel.Analyze.RunAnalysis()
time.sleep(2)
print(f"RunAnalysis: {ret}")

# Resultados
if ret == 0:
    SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
    SapModel.Results.Setup.SetCaseSelectedForOutput("DEAD")

    print("\n=== Desplazamientos de nudos superiores ===")
    uz_sum = 0
    count = 0
    for nid in ['5', '6', '7', '8']:
        r = SapModel.Results.JointDispl(nid, 0)
        if r[0] > 0:
            uz_mm = r[8][0] * 1000
            print(f"  Nudo {nid}: UZ = {uz_mm:.6f} mm")
            uz_sum += uz_mm
            count += 1

    if count > 0:
        uz_avg = uz_sum / count
        print(f"\nUZ promedio (nudos sup.) = {uz_avg:.6f} mm")

        # Comparacion con valores teoricos y Calcpad
        # Teorico: w = P*L/(A*E) donde P en N, L en mm, A en mm2, E en N/mm2
        # P_total = 100 kN = 100000 N
        P_N = P_total * 1000  # kN -> N
        uz_teo = -P_N * L_mm / (L_mm * L_mm * E_MPa)  # -0.004545 mm
        print(f"Teorico (P*L/(A*E)) = {uz_teo:.6f} mm")
        print(f"Calcpad (FEM C3D8)  = -0.004545 mm")

        diff_teo = abs(uz_avg - uz_teo)
        pct_teo = abs(diff_teo / uz_teo) * 100 if uz_teo != 0 else 0
        print(f"\nDiff SAP vs teorico: {diff_teo:.6f} mm ({pct_teo:.3f}%)")

        print(f"\n=== RESULTADO ===")
        if pct_teo < 5:
            print("VALIDADO: SAP2000 coincide con Calcpad y teoria")
        else:
            print("REVISAR: diferencia significativa")
else:
    print(f"Analisis fallo: ret={ret}")
    try:
        status = SapModel.Analyze.GetCaseStatus()
        print(f"CaseStatus: {status}")
    except: pass

print("\n=== Done ===")
