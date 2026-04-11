"""Validar Tutorial C3D8 vs SAP2000 Solid element.

Usa File.NewSolidBlock() que es el template oficial de SAP2000 para
crear un bloque de sólidos hexaédricos automáticamente (como Calcpad C3D8).

Tutorial Calcpad: Cubo 1000x1000x1000 mm, E=22000 MPa, nu=0.2
Compresión uniforme 100 kN aplicada en cara superior.
Esperado: w = P*L/(A*E) = -0.004545 mm"""
import comtypes.client
import time
import os

# === Datos ===
L = 1.0            # m (cubo 1x1x1)
E_MPa = 22000
E = E_MPa * 1000   # kN/m²
nu = 0.2
P_total = 100.0    # kN (compresión)
P_per_node = P_total / 4  # 4 nudos superiores

print("=" * 70)
print("  C3D8 CUBO 1x1x1m — Validacion SAP2000 Solid Element")
print(f"  E={E_MPa} MPa, nu={nu}")
print(f"  Carga total: {P_total} kN (compresion en cara superior)")
print(f"  Template: File.NewSolidBlock (1 elemento hex)")
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

# Iniciar en kN-m
SapModel.InitializeNewModel(6)

# === Template de bloque solido ===
# NewSolidBlock con "Default" (crea automaticamente material y prop solid)
ret = SapModel.File.NewSolidBlock(
    float(L),      # XWidth
    float(L),      # YWidth
    float(L),      # Height
    False,         # Restraint
    "Default",     # Solid prop (Default usa concreto 4000psi)
    1, 1, 1,       # Divisions
)
print(f"File.NewSolidBlock: {ret}")

# === AHORA definir el material CONC con E=22000 MPa ===
# (despues de NewSolidBlock para evitar que lo resetee)
ret = SapModel.PropMaterial.SetMaterial("CONC", 2)
print(f"SetMaterial: {ret}")
ret = SapModel.PropMaterial.SetMPIsotropic("CONC", E, nu, 0.00001)
print(f"SetMPIsotropic: {ret}")

# === Definir PropSolid "SOLID_CONC" con material CONC ===
ret = SapModel.PropSolid.SetProp("SOLID_CONC", "CONC", 0.0, 0.0, 0.0, True)
print(f"PropSolid.SetProp: {ret}")

# === Reasignar la propiedad al solid ===
ret = SapModel.SolidObj.SetProperty("1", "SOLID_CONC", 0)
print(f"SolidObj.SetProperty: {ret}")

# Verificar
ret_v = SapModel.SolidObj.GetProperty("1", "")
print(f"GetProperty check: {ret_v}")
ret_m = SapModel.PropMaterial.GetMPIsotropic("CONC", 0, 0, 0)
print(f"Material check: E={ret_m[0]}, nu={ret_m[1]}, alpha={ret_m[2]}")
print(f"  Joints creados: {SapModel.PointObj.Count()}")
print(f"  Solids creados: {SapModel.SolidObj.Count()}")

# === Encontrar nudos superiores (z = L) ===
import math
top_nodes = []
bot_nodes = []
n_joints = SapModel.PointObj.Count()
names_ret = SapModel.PointObj.GetNameList()
names = names_ret[1] if len(names_ret) > 1 else []
for name in names:
    coord_ret = SapModel.PointObj.GetCoordCartesian(name)
    if len(coord_ret) >= 4:
        x, y, z = coord_ret[0], coord_ret[1], coord_ret[2]
        if abs(z - L) < 0.01:
            top_nodes.append(name)
        elif abs(z) < 0.01:
            bot_nodes.append(name)

print(f"  Nudos superiores (z={L}): {top_nodes}")
print(f"  Nudos base (z=0): {bot_nodes}")

# === Restricciones minimas (como Calcpad — sin restringir expansion lateral) ===
# Base: UZ=0 en los 4 nudos inferiores
# Nudo 1: UX=UY=UZ=0 (fija traslaciones + evita rotar)
# Nudo 2: UY=UZ=0 (evita rotacion Z)
# Nudo 4: UZ=0 (evita rotacion Y)
# Tambien RX=RY=RZ=1 en todos los nudos (solid no usa rotaciones)
bot_sorted = sorted(bot_nodes, key=lambda n: int(n))
print(f"Aplicando BCs minimas a base: {bot_sorted}")

if len(bot_sorted) >= 4:
    n1, n2, n3, n4 = bot_sorted[:4]
    # Todos los nudos base: UZ=0 y rotaciones fijas
    for nid in bot_sorted:
        SapModel.PointObj.SetRestraint(nid,
            [False, False, True, True, True, True])
    # Nudo 1: UX=UY=UZ=0
    SapModel.PointObj.SetRestraint(n1,
        [True, True, True, True, True, True])
    # Nudo 2: UY=UZ=0
    SapModel.PointObj.SetRestraint(n2,
        [False, True, True, True, True, True])
    # Nudos superiores: rotaciones fijas (no afectan traslacion)
    for nid in top_nodes:
        SapModel.PointObj.SetRestraint(nid,
            [False, False, False, True, True, True])

# === Cargas: -P_per_node en cada nudo superior ===
SapModel.LoadPatterns.SetSelfWTMultiplier("DEAD", 0)
for nid in top_nodes:
    SapModel.PointObj.SetLoadForce(nid, "DEAD",
        [0.0, 0.0, float(-P_per_node), 0.0, 0.0, 0.0], False)

# === Guardar y analizar ===
temp = os.path.join(os.environ.get('TEMP'), 'c3d8_solidblock.sdb')
SapModel.File.Save(temp)
print(f"Guardado: {temp}")

try: SapModel.Analyze.SetRunCaseFlag("MODAL", False)
except: pass

# CRITICAL: activar DEAD explicitamente
try:
    ret_flag = SapModel.Analyze.SetRunCaseFlag("DEAD", True)
    print(f"SetRunCaseFlag DEAD=True: {ret_flag}")
except Exception as e:
    print(f"Error activando DEAD: {e}")

print("\nRunning analysis...")
ret = SapModel.Analyze.RunAnalysis()
time.sleep(2)
print(f"RunAnalysis: {ret}")

# === Resultados ===
if ret == 0:
    status = SapModel.Analyze.GetCaseStatus()
    print(f"CaseStatus: {status}")

    SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
    SapModel.Results.Setup.SetCaseSelectedForOutput("DEAD")

    print("\n=== Desplazamientos UZ nudos superiores ===")
    uz_vals = []
    for nid in top_nodes:
        r = SapModel.Results.JointDispl(nid, 0)
        if r[0] > 0:
            uz_mm = r[8][0] * 1000  # m → mm
            uz_vals.append(uz_mm)
            print(f"  Nudo {nid}: UZ = {uz_mm:.6f} mm")

    if uz_vals:
        uz_avg = sum(uz_vals) / len(uz_vals)

        # Teorico
        P_N = P_total * 1000   # kN -> N
        L_mm = L * 1000        # m -> mm
        uz_teo = -P_N * L_mm / (L_mm * L_mm * E_MPa)

        print(f"\n=== COMPARACION ===")
        print(f"SAP2000 (Solid hex)   w = {uz_avg:.6f} mm")
        print(f"Teorico (P*L/(A*E))   w = {uz_teo:.6f} mm")
        print(f"Calcpad FEM C3D8      w = -0.004545 mm")
        print(f"NumPy C3D8 independ.  w = -0.004545 mm")

        diff_teo = abs(uz_avg - uz_teo)
        pct_teo = abs(diff_teo / uz_teo) * 100 if uz_teo != 0 else 0
        print(f"\nDiff SAP vs Teorico: {diff_teo:.7f} mm ({pct_teo:.3f}%)")

        diff_calc = abs(uz_avg - (-0.004545))
        pct_calc = abs(diff_calc / 0.004545) * 100
        print(f"Diff SAP vs Calcpad: {diff_calc:.7f} mm ({pct_calc:.3f}%)")

        print(f"\n=== RESULTADO FINAL ===")
        if pct_teo < 5 and pct_calc < 5:
            print(f"VALIDADO: SAP2000 = Calcpad = NumPy = Teoria")
            print(f"El elemento C3D8 de Calcpad es EQUIVALENTE al Solid de SAP2000")
        else:
            print(f"Diferencia significativa — revisar")

    # === ESFUERZOS: SolidStress ===
    print("\n" + "=" * 70)
    print("  ESFUERZOS EN EL SOLIDO (SAP2000 SolidStress)")
    print("=" * 70)

    try:
        # SolidStress(Name, ItemTypeElm=0, NumberResults, Obj, Elm, PointElm,
        #   LoadCase, StepType, StepNum, S11, S22, S33, S12, S13, S23,
        #   SMax, SMid, SMin, SVM, DirCos...)
        stress = SapModel.Results.SolidStress("1", 0)
        print(f"SolidStress ret[0] = {stress[0]}")

        if stress[0] > 0:
            n = stress[0]
            print(f"Numero de resultados: {n}")
            # Los arrays de stresses estan en indices 9..18 (aprox)
            # ret[0]=NumResults, ret[1..8]=metadata, ret[9]=S11, ret[10]=S22, etc.
            print(f"\n{'Node':>5} {'S11 (kN/m2)':>14} {'S22':>14} {'S33':>14} {'SVM':>14}")
            print("-" * 65)
            for i in range(min(n, 8)):
                pt = stress[3][i]  # PointElm
                s11 = stress[9][i]
                s22 = stress[10][i]
                s33 = stress[11][i]
                svm = stress[18][i]
                print(f"{pt:>5} {s11:>14.4f} {s22:>14.4f} {s33:>14.4f} {svm:>14.4f}")

            # NOTA: SAP2000 reporta en sistema LOCAL del solid.
            # Para NewSolidBlock, el eje local 1 coincide con Z global (vertical)
            # → S11_local = σzz_global
            # S11 = -100 kN/m² corresponde a la compresion vertical esperada.

            s11_avg = sum(stress[9][i] for i in range(n)) / n  # local 1 = vertical
            s22_avg = sum(stress[10][i] for i in range(n)) / n
            s33_avg = sum(stress[11][i] for i in range(n)) / n
            svm_avg = sum(stress[18][i] for i in range(n)) / n

            sigma_zz_teo = -P_total / (L * L)  # -100 kN/m²
            print(f"\nPromedio esfuerzos SAP (sistema local):")
            print(f"  S11 (local 1 = vertical) = {s11_avg:.4f} kN/m2")
            print(f"  S22 (local 2)            = {s22_avg:.6f} kN/m2")
            print(f"  S33 (local 3)            = {s33_avg:.6f} kN/m2")
            print(f"  Von Mises                = {svm_avg:.4f} kN/m2")

            print(f"\nTeorico (P/A):")
            print(f"  sigma_zz = {sigma_zz_teo:.4f} kN/m2")
            print(f"  Von Mises uniaxial = |sigma_zz| = {abs(sigma_zz_teo):.4f} kN/m2")

            # Comparar S11 con teorico
            diff = abs(s11_avg - sigma_zz_teo)
            pct = abs(diff / sigma_zz_teo) * 100 if sigma_zz_teo != 0 else 0
            print(f"\nDiff S11_SAP vs sigma_teo: {diff:.4f} kN/m2 ({pct:.3f}%)")

            # Calcpad stress calculado de strain
            # eps_xx = eps_yy = nu*4.545e-6 (Poisson)
            # eps_zz = -4.545e-6
            # sigma_zz = lambda*(eps_xx+eps_yy+eps_zz) + 2*mu*eps_zz
            lam_kN = E * nu / ((1 + nu) * (1 - 2*nu))
            mu_kN = E / (2 * (1 + nu))
            eps_xx = 4.545e-6 * 0.2
            eps_yy = 4.545e-6 * 0.2
            eps_zz = -4.545e-6
            sigma_zz_calc = lam_kN * (eps_xx + eps_yy + eps_zz) + 2 * mu_kN * eps_zz
            print(f"Calcpad sigma_zz (de strain) = {sigma_zz_calc:.4f} kN/m2")
    except Exception as e:
        print(f"Error leyendo stress: {e}")
        import traceback
        traceback.print_exc()
else:
    print(f"Analisis fallo: ret={ret}")

print("\n=== Done ===")
