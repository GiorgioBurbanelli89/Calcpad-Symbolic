"""4 formas de modelar Zapata Lindero + Aislada con Viga de Amarre — SAP2000
   1) Frame beam (linea) + Joint Spring
   2) Frame beam (linea) + Area Spring
   3) Layered shell (viga shell encima) + Joint Spring
   4) Layered shell + Area Spring
   Patron que FUNCIONA basado en template_zapata_winkler.py"""
import comtypes.client
import time
import os
import math

# ========== PARAMETROS (iguales a Calcpad) ==========
Lz1 = 1.0;  Bz1 = 1.5    # Zapata lindero (m)
Lz2 = 1.5;  Bz2 = 1.5    # Zapata aislada (m)
Lv  = 2.0                 # Viga libre entre zapatas (m)
bv  = 0.3;  hv  = 0.4    # Seccion viga (m)
tz  = 0.40               # Espesor zapatas (m)
E_MPa = 22000
nu = 0.2
ks = 30000               # kN/m3
P_tonf = 20.0
Mx_tonfm = 0.2
My_tonfm = 0.15
de = 0.25                # Mesh size (m)

E = E_MPa * 1000         # kN/m2
P = P_tonf * 9.80665     # kN
Mx = Mx_tonfm * 9.80665  # kN*m
My = My_tonfm * 9.80665

xC1 = 0.15;  yC1 = 0.75       # Col1 (lindero): borde izquierdo
Ltot = Lz1 + Lv + Lz2
xC2 = Lz1 + Lv + Lz2/2        # Col2 (aislada): centro zapata 2
yC2 = Bz2 / 2
Bmax = max(Bz1, Bz2)
yviga = (yC1 + yC2) / 2

# --- Connect SAP2000 ---
def connect_sap():
    try:
        obj = comtypes.client.GetActiveObject("CSI.SAP2000.API.SapObject")
        m = obj.SapModel
        _ = m.GetModelIsLocked()
        print("Connected to existing SAP2000")
        return obj, m
    except:
        print("Starting SAP2000...")
        helper = comtypes.client.CreateObject('SAP2000v1.Helper')
        helper = helper.QueryInterface(comtypes.gen.SAP2000v1.cHelper)
        obj = helper.CreateObjectProgID("CSI.SAP2000.API.SapObject")
        obj.ApplicationStart(6, True)
        time.sleep(8)
        return obj, obj.SapModel

# --- Create mesh (zapata 1 + zapata 2 as separate node ranges) ---
def build_model(SapModel, use_frame_beam, use_area_spring, label):
    SapModel.InitializeNewModel(6)  # kN-m
    SapModel.File.NewBlank()
    SapModel.PropMaterial.SetMaterial("CONC", 2)
    SapModel.PropMaterial.SetMPIsotropic("CONC", E, nu, 0.00001)

    # Shell sections
    SapModel.PropArea.SetShell_1("zap", 4, False, "CONC", 0, tz, tz)  # Plate-Thick
    if not use_frame_beam:
        # Layered: zapata + viga encima como shell mas grueso donde se superponen
        t_combined = tz + hv
        SapModel.PropArea.SetShell_1("zap_viga", 4, False, "CONC", 0, t_combined, t_combined)
        SapModel.PropArea.SetShell_1("viga_sh", 4, False, "CONC", 0, hv, hv)
    else:
        # Frame section for viga
        SapModel.PropFrame.SetRectangle("viga_fr", "CONC", hv, bv)

    # Nodes: Zapata 1 + Zapata 2 on separate grids
    nx1 = int(round(Lz1/de))
    nx2 = int(round(Lz2/de))
    ny  = int(round(Bmax/de))
    nj1x = nx1 + 1
    nj2x = nx2 + 1
    njy  = ny + 1

    node_z1 = {}
    node_z2 = {}
    nid = 1
    for j in range(njy):
        for i in range(nj1x):
            x = i*de; y = j*de
            name = str(nid)
            SapModel.PointObj.AddCartesian(float(x), float(y), 0.0, name)
            node_z1[(i,j)] = name
            nid += 1
    for j in range(njy):
        for i in range(nj2x):
            x = Lz1 + Lv + i*de; y = j*de
            name = str(nid)
            SapModel.PointObj.AddCartesian(float(x), float(y), 0.0, name)
            node_z2[(i,j)] = name
            nid += 1

    # Shell elements: Zapata 1
    n_el = 0
    for j in range(ny):
        for i in range(nx1):
            p = [node_z1[(i,j)], node_z1[(i+1,j)],
                 node_z1[(i+1,j+1)], node_z1[(i,j+1)]]
            aname = f"Z1_{n_el+1}"
            SapModel.AreaObj.AddByPoint(4, p, aname, "zap")
            if use_area_spring:
                SapModel.AreaObj.SetSpring(aname, 1, ks, 1, '', 6, 0, 0, True,
                                           [0.0, 0.0, 0.0], 0.0, True, '')
            n_el += 1
    # Zapata 2
    for j in range(ny):
        for i in range(nx2):
            p = [node_z2[(i,j)], node_z2[(i+1,j)],
                 node_z2[(i+1,j+1)], node_z2[(i,j+1)]]
            aname = f"Z2_{n_el+1}"
            SapModel.AreaObj.AddByPoint(4, p, aname, "zap")
            if use_area_spring:
                SapModel.AreaObj.SetSpring(aname, 1, ks, 1, '', 6, 0, 0, True,
                                           [0.0, 0.0, 0.0], 0.0, True, '')
            n_el += 1

    # Joint Springs (if not area spring)
    if not use_area_spring:
        def add_joint_springs(node_dict, nxmax):
            for (i,j), name in node_dict.items():
                A = de * de
                if (i == 0 or i == nxmax) and (j == 0 or j == ny):
                    A /= 4
                elif (i == 0 or i == nxmax) or (j == 0 or j == ny):
                    A /= 2
                kz = ks * A
                SapModel.PointObj.SetSpring(name, [0.0, 0.0, float(kz), 0.0, 0.0, 0.0])
        add_joint_springs(node_z1, nx1)
        add_joint_springs(node_z2, nx2)

    # Find col nodes
    def nearest(nodes, xc, yc, x_off=0):
        best = None; bd = 1e10
        for (i,j), name in nodes.items():
            x = i*de + x_off
            y = j*de
            d = math.hypot(x - xc, y - yc)
            if d < bd: bd = d; best = name
        return best

    n_col1 = nearest(node_z1, xC1, yC1)
    n_col2 = nearest(node_z2, xC2 - Lz1 - Lv, yC2)  # offset local in zap2 grid

    # Beam connecting columns
    if use_frame_beam:
        SapModel.FrameObj.AddByPoint(n_col1, n_col2, "VIGA", "viga_fr")
    else:
        # Layered: shell de viga entre zapatas
        nv = int(round(Lv/de))
        # Simple: nudos intermedios en el eje de la viga
        jv = int(round(yviga/de))
        jv_lo = max(0, jv - int(round(bv/2/de)))
        jv_hi = min(ny, jv + int(round(bv/2/de)))

        # Crear nodos intermedios
        viga_nodes = {}
        for j in range(jv_lo, jv_hi+1):
            # Primer nudo del segmento de viga = nudo derecho de zapata 1
            viga_nodes[(0,j)] = node_z1[(nx1, j)]
            # Ultimo nudo = nudo izquierdo de zapata 2
            viga_nodes[(nv,j)] = node_z2[(0, j)]
            # Nudos intermedios
            for i in range(1, nv):
                x = Lz1 + i*de; y = j*de
                name = str(nid)
                SapModel.PointObj.AddCartesian(float(x), float(y), 0.0, name)
                viga_nodes[(i,j)] = name
                nid += 1

        # Elementos shell de la viga (sin Winkler)
        for j in range(jv_lo, jv_hi):
            for i in range(nv):
                p = [viga_nodes[(i,j)], viga_nodes[(i+1,j)],
                     viga_nodes[(i+1,j+1)], viga_nodes[(i,j+1)]]
                aname = f"V_{n_el+1}"
                SapModel.AreaObj.AddByPoint(4, p, aname, "viga_sh")
                n_el += 1

    # Anti-rotation (minimal restraints)
    # Un nudo central en cada zapata con UX, UY restringidos
    SapModel.PointObj.SetRestraint(n_col1, [True, True, False, False, False, False])
    # Una esquina con UX para anti-rotacion
    SapModel.PointObj.SetRestraint(node_z1[(0,0)], [True, False, False, False, False, False])
    SapModel.PointObj.SetRestraint(node_z2[(0,0)], [False, True, False, False, False, False])

    # Remove self-weight
    SapModel.LoadPatterns.SetSelfWTMultiplier("DEAD", 0)

    # Loads
    SapModel.PointObj.SetLoadForce(n_col1, "DEAD",
        [0.0, 0.0, float(-P), float(Mx), float(My), 0.0], False)
    SapModel.PointObj.SetLoadForce(n_col2, "DEAD",
        [0.0, 0.0, float(-P), float(Mx), float(My), 0.0], False)

    return n_col1, n_col2

def run_and_get(SapModel, n1, n2, label):
    # Save
    temp = os.path.join(os.environ.get('TEMP', r'C:\Temp'),
                        f'zapata_lindero_{label}.sdb')
    SapModel.File.Save(temp)

    try:
        SapModel.Analyze.SetRunCaseFlag("MODAL", False)
    except:
        pass

    ret = SapModel.Analyze.RunAnalysis()
    time.sleep(2)
    if ret != 0:
        return 0, 0

    SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
    SapModel.Results.Setup.SetCaseSelectedForOutput("DEAD")

    w1 = w2 = 0
    r = SapModel.Results.JointDispl(n1, 0)
    if r[0] > 0:
        w1 = r[8][0] * 1000  # m -> mm
    r = SapModel.Results.JointDispl(n2, 0)
    if r[0] > 0:
        w2 = r[8][0] * 1000

    return w1, w2

# ========== MAIN ==========
print("=" * 70)
print("  ZAPATA LINDERO + AISLADA — 4 FORMAS")
print(f"  Lz1={Lz1}m Bz1={Bz1}m, Lz2={Lz2}m Bz2={Bz2}m, Lv={Lv}m")
print(f"  P1=P2={P_tonf}tonf, Mx={Mx_tonfm}, My={My_tonfm} tonf*m")
print(f"  ks={ks} kN/m3, de={de}m")
print("=" * 70)

obj, SapModel = connect_sap()

configs = [
    ("1_frame_joint",   True,  False, "1) Frame + Joint Spring"),
    ("2_frame_area",    True,  True,  "2) Frame + Area Spring"),
    ("3_layered_joint", False, False, "3) Layered + Joint Spring"),
    ("4_layered_area",  False, True,  "4) Layered + Area Spring"),
]

results = {}
for label, use_frame, use_area, name in configs:
    print(f"\n--- {name} ---")
    try:
        n1, n2 = build_model(SapModel, use_frame, use_area, label)
        w1, w2 = run_and_get(SapModel, n1, n2, label)
        results[name] = (w1, w2)
        print(f"  w_col1 = {w1:.4f} mm")
        print(f"  w_col2 = {w2:.4f} mm")
    except Exception as ex:
        print(f"  ERROR: {ex}")
        import traceback
        traceback.print_exc()
        results[name] = (0, 0)

# === Summary ===
print("\n" + "=" * 70)
print("RESUMEN:")
print("-" * 70)
print(f"{'Modelo':<35} {'w_col1 (mm)':>12} {'w_col2 (mm)':>12}")
print("-" * 70)
for name, (w1, w2) in results.items():
    print(f"{name:<35} {w1:>12.4f} {w2:>12.4f}")
print("-" * 70)
print(f"{'Calcpad (Frame+Area)':<35} {'~-5.87':>12} {'~-1.85':>12}")
print("=" * 70)
