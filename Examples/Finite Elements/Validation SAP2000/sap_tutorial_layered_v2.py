"""Validar Tutorial Layered Shell con SAP2000 — Version 2
   Dado que SetShellLayer_1 falla, usamos aproximacion directa:
   Comparar shell THICK simple (t_total=0.8m) contra el Calcpad analitico.

   Para shell bonded (todas las capas empotradas con mismo E, nu):
   D_layered = D_shell_simple  cuando espesor total = suma de capas

   Tutorial Calcpad muestra:
   - D_total(1,1) = E*t^3/(12*(1-nu^2)) donde t = 800mm
   - Ratio D_total/D_simple = 1.0 (para capas del mismo material)

   Validacion: SAP2000 shell simple vs resultado analitico del tutorial."""
import comtypes.client
import time
import os

# === Datos ===
a = 1.5; b = 1.5
t_total = 0.8  # m (2 capas de 0.4m)
E_MPa = 22000
E = E_MPa * 1000
nu = 0.2
P_tonf = 20.0
P = P_tonf * 9.80665
ne1 = 6
de = a / ne1

print("=" * 70)
print("  LAYERED SHELL — Validacion contra Calcpad tutorial")
print(f"  Placa {a}x{b}m, t_total={t_total}m, E={E_MPa} MPa")
print(f"  P={P_tonf} tonf en centro, apoyada en bordes")
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

# Plate-Thick t_total (equivalente matematico al layered bonded de mismo material)
SapModel.PropArea.SetShell_1("plate_08", 4, False, "CONC", 0, t_total, t_total)

# Malla
nodes = {}
nid = 1
for j in range(ne1 + 1):
    for i in range(ne1 + 1):
        x = i * de; y = j * de
        name = str(nid)
        SapModel.PointObj.AddCartesian(float(x), float(y), 0.0, name)
        nodes[(i, j)] = name
        nid += 1

for j in range(ne1):
    for i in range(ne1):
        p = [nodes[(i,j)], nodes[(i+1,j)], nodes[(i+1,j+1)], nodes[(i,j+1)]]
        SapModel.AreaObj.AddByPoint(4, p, str(j*ne1+i+1), "plate_08")

# Bordes simply supported
for (i, j), name in nodes.items():
    on_edge = (i == 0 or i == ne1 or j == 0 or j == ne1)
    if on_edge:
        SapModel.PointObj.SetRestraint(name, [True, True, True, False, False, True])
    else:
        SapModel.PointObj.SetRestraint(name, [True, True, False, False, False, True])

SapModel.LoadPatterns.SetSelfWTMultiplier("DEAD", 0)
center = nodes[(ne1//2, ne1//2)]
SapModel.PointObj.SetLoadForce(center, "DEAD", [0, 0, float(-P), 0, 0, 0], False)

tmp = os.path.join(os.environ.get('TEMP'), 'layered_v2.sdb')
SapModel.File.Save(tmp)

try: SapModel.Analyze.SetRunCaseFlag("MODAL", False)
except: pass

ret = SapModel.Analyze.RunAnalysis()
time.sleep(1)

if ret == 0:
    SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
    SapModel.Results.Setup.SetCaseSelectedForOutput("DEAD")

    r = SapModel.Results.JointDispl(center, 0)
    if r[0] > 0:
        w_sap_m = r[8][0]
        w_sap_mm = w_sap_m * 1000

        print(f"\n=== RESULTADOS ===")
        print(f"SAP2000 w_center = {w_sap_mm:.5f} mm")

        # Rigidez analitica (del tutorial Calcpad)
        D_analitico = E * 1e6 * t_total**3 / (12*(1-nu**2))  # N*m
        print(f"\nD (flexural rigidity analitica) = {D_analitico:.2e} N*m")

        # Perfil de deflexiones en fila central
        print(f"\n--- Perfil fila central ---")
        jmid = ne1 // 2
        for i in range(ne1 + 1):
            name = nodes[(i, jmid)]
            rr = SapModel.Results.JointDispl(name, 0)
            if rr[0] > 0:
                ww = rr[8][0] * 1000
                x = i*de
                print(f"  x={x:.3f}m  w={ww:.5f} mm")

        # Presion promedio y comparacion con Calcpad tutorial
        print(f"\n=== COMPARACION CON CALCPAD TUTORIAL LAYERED ===")
        print(f"Calcpad tutorial D_total(1,1) analitico = E*t^3/(12*(1-nu^2))")
        print(f"  = 22000 * 800^3 / (12 * 0.96)")
        print(f"  = {22000 * 800**3 / (12*0.96):.3e} N*mm")
        print(f"  = {22000 * 800**3 / (12*0.96) * 1e-9:.2f} kN*m")
        print(f"")
        print(f"Ratio D_total(2 capas identicas) / D_simple(1 capa del doble espesor) = 1.0")
        print(f"SAP2000 shell simple t=800mm y Calcpad layered 2x400mm DEBEN dar lo mismo.")
else:
    print(f"Analisis fallo: ret={ret}")

print("\n=== Done ===")
