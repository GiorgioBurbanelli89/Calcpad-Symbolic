"""Replica del Tutorial de William R. Serquen
"EL SAP 2000 APLICADO AL SUELO" via SAP2000 API (Python + comtypes).

Problema (Fig. SF-A del PDF):
- Masa de suelo elastica isotropica: 20 x 20 x 10 m
- Malla: 40 x 40 x 20 = 32000 elementos C3D8 (hex unitario 0.5 m)
- Material MAT_SUELOS (arcilla rigida): E = 2000 tonf/m2, nu = 0.42
- Restricciones:
    - Base (Z=0): empotrada
    - Caras laterales (X = +-10, Y = +-10): Translation 1,2,3 fijas
- 3 patrones de carga tipo DEAD:
    - PUNTUAL: P = 100 tonf en (0, 0, 10)
    - LINEAL: q = 1 tonf/m en el perimetro cuadrado 10x10 m (via frames sobre la cara superior)
    - RECTANGULAR: w = 10 tonf/m2 en un rectangulo 5x3 m centrado (Solid Pressure Face 6)
- Combinacion COMB1 = PUNTUAL + LINEAL + RECTANGULAR

La malla original del PDF son 32000 hex8. Para pruebas rapidas se puede reducir
via el parametro GRID_DIV (default 40 = fiel al PDF, 10 = rapido).

Implementacion:
- File.NewSolidBlock crea el bloque hex8 con restraint y mesh uniforme.
- Material CONC se sustituye por MAT_SUELOS via PropMaterial.
- PropSolid "SOLID_SUELO" con MAT_SUELOS y Incompatible=True.
- Re-asignacion masiva de SOLID_SUELO a todos los solids.
- BCs laterales con SetRestraint selectivo por coordenadas.
- Carga puntual: PointObj.SetLoadForce en el nudo top-center.
- Carga lineal: se crean 4 frames perimetro + SetDistributedLoad.
- Carga rectangular: SolidObj.SetSurfacePressure en la cara superior (face 6)
  de los elementos del rectangulo central.
"""
import comtypes.client
import os
import time
import sys

# === Parametros del problema (Serquen) ===
LX = 20.0            # m (ancho en X)
LY = 20.0            # m (ancho en Y)
LZ = 10.0            # m (altura en Z)
GRID_DIV = int(os.environ.get("GRID_DIV", "20"))   # subdivisiones por lado (40 en el PDF)
NZ_DIV = GRID_DIV // 2   # el PDF usa 20 en Z (mitad de 40)

# Propiedades del suelo
E_SUELO = 2000.0     # tonf/m2 (arcilla rigida)
NU_SUELO = 0.42
ALPHA = 7.2e-6

# Cargas
P_PUNTUAL = 100.0    # tonf (hacia abajo)
Q_LINEAL = 1.0       # tonf/m (carga lineal sobre perimetro 10x10)
W_RECTANGULAR = 10.0 # tonf/m2 (presion en rectangulo 5x3)
LADO_LINEAL = 10.0   # lado del cuadrado donde se aplica la carga lineal
RECT_X = 5.0         # ancho del rectangulo (paralelo a X)
RECT_Y = 3.0         # ancho del rectangulo (paralelo a Y)

TOL = 1e-4

print("=" * 78)
print(f"  SUELO SERQUEN — Malla {GRID_DIV}x{GRID_DIV}x{NZ_DIV} hex C3D8 ({GRID_DIV*GRID_DIV*NZ_DIV} elementos)")
print(f"  Bloque {LX}x{LY}x{LZ} m — E={E_SUELO} tonf/m2, nu={NU_SUELO}")
print(f"  Cargas: P={P_PUNTUAL} tonf, q={Q_LINEAL} tonf/m, w={W_RECTANGULAR} tonf/m2")
print("=" * 78)

# === Conectar a SAP2000 ===
try:
    mySapObject = comtypes.client.GetActiveObject("CSI.SAP2000.API.SapObject")
    SapModel = mySapObject.SapModel
    _ = SapModel.GetModelIsLocked()
    print("Conectado a SAP2000 existente")
except Exception:
    print("Iniciando SAP2000...")
    helper = comtypes.client.CreateObject('SAP2000v1.Helper')
    helper = helper.QueryInterface(comtypes.gen.SAP2000v1.cHelper)
    mySapObject = helper.CreateObjectProgID("CSI.SAP2000.API.SapObject")
    mySapObject.ApplicationStart(12, True)  # 12 = Tonf, m, C
    time.sleep(8)
    SapModel = mySapObject.SapModel

# Unidades Tonf, m, C
SapModel.InitializeNewModel(12)

# === 1. Crear bloque solido con template ===
# NewSolidBlock(XWidth, YWidth, Height, Restraint, SolidProp, NumX, NumY, NumZ)
# Restraint=True empotra la base automaticamente
print(f"\n[1] Creando bloque {LX}x{LY}x{LZ} m con File.NewSolidBlock...")
ret = SapModel.File.NewSolidBlock(
    float(LX), float(LY), float(LZ),
    True,            # Restraint base empotrada (el PDF usa esto)
    "Default",       # solid prop temporal
    GRID_DIV, GRID_DIV, NZ_DIV,
)
print(f"  NewSolidBlock ret={ret}")
n_joints = SapModel.PointObj.Count()
n_solids = SapModel.SolidObj.Count()
print(f"  Joints: {n_joints}, Solids: {n_solids}")

# === 2. Definir MAT_SUELOS (DESPUES de NewSolidBlock) ===
print(f"\n[2] Definiendo material MAT_SUELOS (E={E_SUELO}, nu={NU_SUELO})...")
SapModel.PropMaterial.SetMaterial("MAT_SUELOS", 2)  # 2 = concrete-like
SapModel.PropMaterial.SetMPIsotropic("MAT_SUELOS", E_SUELO, NU_SUELO, ALPHA)
# Peso propio = 0 (suelo ya esta en equilibrio previo)
SapModel.PropMaterial.SetWeightAndMass("MAT_SUELOS", 1, 0.0)

# PropSolid SOLID_SUELO con MAT_SUELOS y modos incompatibles
ret = SapModel.PropSolid.SetProp("SOLID_SUELO", "MAT_SUELOS", 0.0, 0.0, 0.0, True)
print(f"  PropSolid.SetProp ret={ret}")

# === 3. Reasignar SOLID_SUELO a todos los solids ===
print("\n[3] Reasignando SOLID_SUELO a todos los solids...")
names_ret = SapModel.SolidObj.GetNameList()
solid_names = names_ret[1] if len(names_ret) > 1 else []
count_ok = 0
for name in solid_names:
    r = SapModel.SolidObj.SetProperty(name, "SOLID_SUELO", 0)
    if r == 0:
        count_ok += 1
print(f"  {count_ok}/{len(solid_names)} solids con SOLID_SUELO")

# === 4. Restricciones laterales (caras X=+-10, Y=+-10) ===
print("\n[4] Aplicando restricciones laterales...")
names_ret = SapModel.PointObj.GetNameList()
joint_names = names_ret[1] if len(names_ret) > 1 else []

top_center_joint = None
top_joints_all = []
lateral_count = 0
for name in joint_names:
    coord = SapModel.PointObj.GetCoordCartesian(name)
    if len(coord) < 4:
        continue
    x, y, z = coord[0], coord[1], coord[2]
    # NewSolidBlock centra en origen (-LX/2..+LX/2)
    on_left   = abs(x - (-LX/2)) < TOL
    on_right  = abs(x - ( LX/2)) < TOL
    on_front  = abs(y - (-LY/2)) < TOL
    on_back   = abs(y - ( LY/2)) < TOL
    on_top    = abs(z - LZ) < TOL
    # Translation 1,2,3 fijas en caras laterales
    if (on_left or on_right or on_front or on_back) and z > TOL:
        SapModel.PointObj.SetRestraint(name, [True, True, True, False, False, False])
        lateral_count += 1
    # Guardar el nudo top-center para carga puntual
    if on_top and abs(x) < TOL and abs(y) < TOL:
        top_center_joint = name
    if on_top:
        top_joints_all.append((name, x, y))

print(f"  Joints laterales restringidos: {lateral_count}")
print(f"  Top-center joint: {top_center_joint}")

# === 5. Patrones de carga DEAD ===
print("\n[5] Creando patrones de carga...")
SapModel.LoadPatterns.SetSelfWTMultiplier("DEAD", 0)
for pat in ("PUNTUAL", "LINEAL", "RECTANGULAR"):
    SapModel.LoadPatterns.Add(pat, 8, 0, True)  # 8 = DEAD type

# Combinacion COMB1 = PUNTUAL + LINEAL + RECTANGULAR
SapModel.RespCombo.Add("COMB1", 0)  # 0 = Linear Add
for pat in ("PUNTUAL", "LINEAL", "RECTANGULAR"):
    SapModel.RespCombo.SetCaseList("COMB1", 0, pat, 1.0)
print("  Patrones: PUNTUAL, LINEAL, RECTANGULAR; Combo: COMB1")

# === 6. Carga PUNTUAL: -100 tonf en (0, 0, 10) ===
print("\n[6] Aplicando carga PUNTUAL...")
if top_center_joint is None:
    print("  ERROR: No se encontro nudo top-center (0,0,10). Usando nudo mas cercano.")
    min_d = 1e9
    for name, x, y in top_joints_all:
        d = x*x + y*y
        if d < min_d:
            min_d = d
            top_center_joint = name
SapModel.PointObj.SetLoadForce(top_center_joint, "PUNTUAL",
    [0.0, 0.0, -float(P_PUNTUAL), 0.0, 0.0, 0.0], False)
print(f"  -{P_PUNTUAL} tonf en joint {top_center_joint}")

# === 7. Carga LINEAL: q=1 tonf/m en perimetro cuadrado 10x10 ===
# Como no tenemos frames directos en un modelo solido, aplicamos la carga como
# fuerza puntual distribuida en los joints del perimetro del cuadrado 10x10 de
# la cara superior. Cada joint recibe Fz = -q * Ltrib.
print("\n[7] Aplicando carga LINEAL (como puntuales sobre el perimetro del cuadrado)...")
half_side = LADO_LINEAL / 2
perim_joints = []
for name, x, y in top_joints_all:
    on_left   = abs(x - (-half_side)) < TOL
    on_right  = abs(x - ( half_side)) < TOL
    on_front  = abs(y - (-half_side)) < TOL
    on_back   = abs(y - ( half_side)) < TOL
    in_x_range = -half_side - TOL <= x <= half_side + TOL
    in_y_range = -half_side - TOL <= y <= half_side + TOL
    if ((on_left or on_right) and in_y_range) or ((on_front or on_back) and in_x_range):
        perim_joints.append((name, x, y))

# Longitud tributaria de cada nudo (perimetro = 4 * 10 = 40 m)
dx = LX / GRID_DIV
# Eliminar duplicados (esquinas aparecen 2 veces)
unique_perim = list({jn[0]: jn for jn in perim_joints}.values())
n_perim = len(unique_perim)
# Suma de todas las cargas debe dar = q * perimetro = 1 * 40 = 40 tonf
# Cada nudo recibe aprox dx * q (sin contar esquinas dobles)
F_per_joint = Q_LINEAL * dx  # tonf
# Para esquinas, damos Ltrib/2 ... pero es aproximado, usamos el mismo valor
for name, x, y in unique_perim:
    SapModel.PointObj.SetLoadForce(name, "LINEAL",
        [0.0, 0.0, -float(F_per_joint), 0.0, 0.0, 0.0], False)
print(f"  {n_perim} joints con Fz=-{F_per_joint:.4f} tonf (q={Q_LINEAL} tonf/m, dx={dx} m)")
print(f"  Suma aplicada ~ {n_perim * F_per_joint:.2f} tonf (esperado {Q_LINEAL * 4 * LADO_LINEAL:.2f})")

# === 8. Carga RECTANGULAR: w=-10 tonf/m2 en rectangulo 5x3 m ===
# Se aplica como surface pressure en la face 6 (top) de los solids cuyos centros
# estan dentro del rectangulo.
print(f"\n[8] Aplicando carga RECTANGULAR (w={W_RECTANGULAR} tonf/m2)...")
half_rx = RECT_X / 2
half_ry = RECT_Y / 2
# Los solids en la fila superior tienen sus centros en z = LZ - dz/2
dz = LZ / NZ_DIV
z_top_center = LZ - dz / 2
count_rect = 0
# Iteramos sobre solids y chequeamos si el centro esta en la fila superior
# y dentro del rectangulo
for sname in solid_names:
    # Obtener nudos del solid para calcular su centro
    pts_ret = SapModel.SolidObj.GetPoints(sname)
    if len(pts_ret) < 2:
        continue
    pt_names = pts_ret[0]
    # Centro = promedio de coords
    cx = cy = cz = 0.0
    n = 0
    for pn in pt_names:
        c = SapModel.PointObj.GetCoordCartesian(pn)
        if len(c) >= 4:
            cx += c[0]; cy += c[1]; cz += c[2]
            n += 1
    if n == 0:
        continue
    cx /= n; cy /= n; cz /= n
    if abs(cz - z_top_center) < dz / 4:  # es fila superior
        if -half_rx - TOL <= cx <= half_rx + TOL and -half_ry - TOL <= cy <= half_ry + TOL:
            # Face 6 en SAP2000 solids = cara superior (z=+)
            # Firma v24+: SetLoadSurfacePressure(Name, LoadPat, Face, Value, JointPatternName, Replace, ItemType)
            SapModel.SolidObj.SetLoadSurfacePressure(sname, "RECTANGULAR", 6, -float(W_RECTANGULAR), "", False, 0)
            count_rect += 1
print(f"  {count_rect} solids con surface pressure en face 6")
print(f"  Area cargada ~ {count_rect * dx * dx:.2f} m2 (esperado {RECT_X * RECT_Y:.2f})")

# === 9. Guardar y analizar ===
print("\n[9] Guardando y analizando...")
temp_path = os.path.join(os.environ.get("TEMP", "C:/Temp"), "suelo_serquen.sdb")
SapModel.File.Save(temp_path)
print(f"  Guardado en: {temp_path}")

try: SapModel.Analyze.SetRunCaseFlag("MODAL", False)
except: pass
for pat in ("PUNTUAL", "LINEAL", "RECTANGULAR"):
    try: SapModel.Analyze.SetRunCaseFlag(pat, True)
    except: pass

t0 = time.time()
print(f"  Ejecutando analisis...")
ret = SapModel.Analyze.RunAnalysis()
time.sleep(2)
dt = time.time() - t0
print(f"  RunAnalysis ret={ret} (dt={dt:.1f}s)")

# === 10. Resultados ===
if ret == 0:
    SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
    for pat in ("PUNTUAL", "LINEAL", "RECTANGULAR"):
        SapModel.Results.Setup.SetCaseSelectedForOutput(pat)
    SapModel.Results.Setup.SetComboSelectedForOutput("COMB1")

    print("\n=== DESPLAZAMIENTOS UZ EN NUDO TOP-CENTER ===")
    for pat in ("PUNTUAL", "LINEAL", "RECTANGULAR", "COMB1"):
        SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
        if pat == "COMB1":
            SapModel.Results.Setup.SetComboSelectedForOutput("COMB1")
        else:
            SapModel.Results.Setup.SetCaseSelectedForOutput(pat)
        r = SapModel.Results.JointDispl(top_center_joint, 0)
        if r[0] > 0:
            uz_mm = r[8][0] * 1000
            print(f"  {pat:15s}: UZ = {uz_mm:>10.4f} mm")

    # Stress en algun solid de la fila superior en el centro
    # Buscar el solid central superior
    center_solid = None
    for sname in solid_names:
        pts_ret = SapModel.SolidObj.GetPoints(sname)
        if len(pts_ret) < 2:
            continue
        pt_names = pts_ret[0]
        cx = cy = cz = 0.0
        n = 0
        for pn in pt_names:
            c = SapModel.PointObj.GetCoordCartesian(pn)
            if len(c) >= 4:
                cx += c[0]; cy += c[1]; cz += c[2]
                n += 1
        cx /= n; cy /= n; cz /= n
        if abs(cz - z_top_center) < dz / 4 and abs(cx) < dx and abs(cy) < dx:
            center_solid = sname
            break

    if center_solid:
        print(f"\n=== ESFUERZOS S33 EN SOLID CENTRAL SUPERIOR ({center_solid}) ===")
        for pat in ("PUNTUAL", "LINEAL", "RECTANGULAR", "COMB1"):
            SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
            if pat == "COMB1":
                SapModel.Results.Setup.SetComboSelectedForOutput("COMB1")
            else:
                SapModel.Results.Setup.SetCaseSelectedForOutput(pat)
            try:
                stress = SapModel.Results.SolidStress(center_solid, 0)
                if stress[0] > 0:
                    s33_vals = stress[11]
                    s33_avg = sum(s33_vals) / len(s33_vals)
                    print(f"  {pat:15s}: S33_avg = {s33_avg:>10.4f} tonf/m2")
            except Exception as e:
                print(f"  {pat:15s}: Error {e}")
else:
    print(f"  ERROR en analisis: ret={ret}")

print("\n=== Tutorial suelo (Serquen) completo ===")
