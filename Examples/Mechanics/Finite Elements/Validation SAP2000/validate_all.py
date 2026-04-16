"""Compare ALL Calcpad CLI plate examples vs SAP2000.
Each test uses SAME mesh, SAME parameters as the Calcpad .cpd file.
"""
import sys, os
sys.path.insert(0, r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Pipy Api sap2000 hekatan\src')
import pysap2000

model = pysap2000.attach()
all_results = []


def create_slab_model(name, sap_type, a, b, t, E_mpa, nu, q, nx, ny, bc='simply_supported'):
    """Create a slab model in SAP2000 and return center deflection.

    sap_type: 1=Shell-Thin, 2=Shell-Thick, 3=Plate-Thin, 4=Plate-Thick, 5=Membrane
    bc: 'simply_supported' (w=0 edges) or 'fixed' (all DOF edges)
    """
    pysap2000.set_units('kN_m')
    model.InitializeNewModel()
    model.File.NewBlank()

    E_kn = E_mpa * 1000
    dx, dy = a/nx, b/ny

    model.PropMaterial.SetMaterial('MAT', 2)
    model.PropMaterial.SetMPIsotropic('MAT', E_kn, nu, 0)
    model.PropArea.SetShell_1('SEC', sap_type, False, 'MAT', 0, t, t)

    n1 = nx + 1
    nodes = {}
    nid = 1
    for j in range(ny+1):
        for i in range(nx+1):
            model.PointObj.AddCartesian(i*dx, j*dy, 0, '', str(nid))
            nodes[(i,j)] = str(nid)
            nid += 1

    eid = 1
    for j in range(ny):
        for i in range(nx):
            pts = [nodes[(i,j)], nodes[(i+1,j)], nodes[(i+1,j+1)], nodes[(i,j+1)]]
            model.AreaObj.AddByPoint(4, pts, f'E{eid}', 'SEC')
            eid += 1

    # Supports
    for j in range(ny+1):
        for i in range(nx+1):
            if i==0 or i==nx or j==0 or j==ny:
                n = nodes[(i,j)]
                if bc == 'simply_supported':
                    model.PointObj.SetRestraint(n, [False,False,True,False,False,False])
                else:
                    model.PointObj.SetRestraint(n, [True]*6)

    # Load
    model.LoadPatterns.Add('Q', 8, 0, True)
    for j in range(ny+1):
        for i in range(nx+1):
            tx = dx if (0<i<nx) else dx/2
            ty = dy if (0<j<ny) else dy/2
            model.PointObj.SetLoadForce(nodes[(i,j)], 'Q', [0,0,-q*tx*ty,0,0,0])

    save_path = os.path.join(r'C:\CSiAPIexample', f'{name}.sdb')
    os.makedirs(r'C:\CSiAPIexample', exist_ok=True)
    model.File.Save(save_path)
    model.Analyze.RunAnalysis()

    model.Results.Setup.DeselectAllCasesAndCombosForOutput()
    model.Results.Setup.SetCaseSelectedForOutput('Q')

    cn = nodes[(nx//2, ny//2)]
    NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]; OE=0
    [NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret] = \
        model.Results.JointDispl(cn, OE, NR, Obj, Elm, AC, ST, SN, U1, U2, U3, R1, R2, R3)
    return abs(U3[0])*1000 if NR > 0 else 0.0  # mm


def create_wall_model(name, sap_type, W, H, t, E_mpa, nu, P, nx, ny):
    """Create wall with lateral load in SAP2000. Return max horizontal disp."""
    pysap2000.set_units('kN_m')
    model.InitializeNewModel()
    model.File.NewBlank()

    E_kn = E_mpa * 1000
    dx, dy = W/nx, H/ny

    model.PropMaterial.SetMaterial('MAT', 2)
    model.PropMaterial.SetMPIsotropic('MAT', E_kn, nu, 0)
    model.PropArea.SetShell_1('SEC', sap_type, False, 'MAT', 0, t, t)

    n1 = nx + 1
    nodes = {}
    nid = 1
    for j in range(ny+1):
        for i in range(nx+1):
            model.PointObj.AddCartesian(i*dx, 0, j*dy, '', str(nid))
            nodes[(i,j)] = str(nid)
            nid += 1

    eid = 1
    for j in range(ny):
        for i in range(nx):
            pts = [nodes[(i,j)], nodes[(i+1,j)], nodes[(i+1,j+1)], nodes[(i,j+1)]]
            model.AreaObj.AddByPoint(4, pts, f'E{eid}', 'SEC')
            eid += 1

    # Base fixed
    for i in range(nx+1):
        model.PointObj.SetRestraint(nodes[(i,0)], [True]*6)

    # Lateral load at top
    model.LoadPatterns.Add('LAT', 8, 0, True)
    p_n = P / (nx+1)
    for i in range(nx+1):
        model.PointObj.SetLoadForce(nodes[(i,ny)], 'LAT', [p_n,0,0,0,0,0])

    save_path = os.path.join(r'C:\CSiAPIexample', f'{name}.sdb')
    os.makedirs(r'C:\CSiAPIexample', exist_ok=True)
    model.File.Save(save_path)
    model.Analyze.RunAnalysis()

    model.Results.Setup.DeselectAllCasesAndCombosForOutput()
    model.Results.Setup.SetCaseSelectedForOutput('LAT')

    u_max = 0.0
    for i in range(nx+1):
        n = nodes[(i,ny)]
        NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]; OE=0
        [NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret] = \
            model.Results.JointDispl(n, OE, NR, Obj, Elm, AC, ST, SN, U1, U2, U3, R1, R2, R3)
        if NR > 0 and abs(U1[0]) > abs(u_max):
            u_max = U1[0]
    return abs(u_max)*1000  # mm


# =====================================================
# TEST 1: MEMBRANE - Muro corte 5x3m, P=100kN lateral
# Calcpad: Plate Theory - Membrane Q4.cpd (8x6)
# =====================================================
print("\n" + "="*60)
print("  TEST 1: MEMBRANE (muro corte)")
print("="*60)
w_mem = create_wall_model('T1_Membrane', 5, 5, 3, 0.2, 25000, 0.2, 100, 8, 6)
print(f"  SAP2000 Membrane u_max = {w_mem:.4f} mm")
print(f"  Calcpad CLI:             = 0.0589 mm")
print(f"  Ratio: {0.0589/w_mem:.4f}")
all_results.append(('Membrane 8x6', 0.0589, w_mem))

# =====================================================
# TEST 2: PLATE-THIN (Kirchhoff) - Losa 6x4m, q=10
# Calcpad: Plate Theory - Kirchhoff Love.cpd (6x4)
# =====================================================
print("\n" + "="*60)
print("  TEST 2: PLATE-THIN (Kirchhoff, losa 6x4)")
print("="*60)
w_pt = create_slab_model('T2_PlateThin', 3, 6, 4, 0.1, 35000, 0.15, 10, 6, 4)
print(f"  SAP2000 Plate-Thin w_center = {w_pt:.4f} mm")
# Calcpad Kirchhoff result will be extracted after running
all_results.append(('Plate-Thin 6x4', None, w_pt))

# =====================================================
# TEST 3: PLATE-THICK (Mindlin) - Losa 6x4m, q=10
# Same slab as Kirchhoff for comparison
# =====================================================
print("\n" + "="*60)
print("  TEST 3: PLATE-THICK (Mindlin, losa 6x4)")
print("="*60)
w_pk = create_slab_model('T3_PlateThick', 4, 6, 4, 0.1, 35000, 0.15, 10, 6, 4)
print(f"  SAP2000 Plate-Thick w_center = {w_pk:.4f} mm")
all_results.append(('Plate-Thick 6x4', None, w_pk))

# =====================================================
# TEST 4: SHELL-THIN - Muro 5x3m, P_h=100kN + q_z=5kN/m2
# =====================================================
print("\n" + "="*60)
print("  TEST 4: SHELL-THIN (muro, carga in-plane)")
print("="*60)
w_st = create_wall_model('T4_ShellThin', 1, 5, 3, 0.2, 25000, 0.2, 100, 5, 3)
print(f"  SAP2000 Shell-Thin u_max = {w_st:.4f} mm")
all_results.append(('Shell-Thin 5x3', None, w_st))

# =====================================================
# TEST 5: SHELL-THICK - Same wall
# =====================================================
print("\n" + "="*60)
print("  TEST 5: SHELL-THICK (muro, carga in-plane)")
print("="*60)
w_sk = create_wall_model('T5_ShellThick', 2, 5, 3, 0.2, 25000, 0.2, 100, 5, 3)
print(f"  SAP2000 Shell-Thick u_max = {w_sk:.4f} mm")
all_results.append(('Shell-Thick 5x3', None, w_sk))

# =====================================================
# TEST 6: PLANE STRESS - Same wall as membrane
# Use Plane type in SAP2000
# =====================================================
print("\n" + "="*60)
print("  TEST 6: PLANE STRESS (muro corte)")
print("="*60)
# Plane stress in SAP2000 uses AreaObj with Plane section
pysap2000.set_units('kN_m')
model.InitializeNewModel()
model.File.NewBlank()
E_kn = 25000 * 1000
model.PropMaterial.SetMaterial('MAT', 2)
model.PropMaterial.SetMPIsotropic('MAT', E_kn, 0.2, 0)
# Plane section: PropArea.SetPlane('name', PlaneType, Material, MatAngle, Thickness, IncompType)
# PlaneType: 1=Plane Stress, 2=Plane Strain
model.PropArea.SetPlane('PLANE_S', 1, 'MAT', 0, 0.2, 0)

W, H, t = 5, 3, 0.2
nx, ny = 8, 6
dx, dy = W/nx, H/ny
n1 = nx+1
nodes = {}
nid = 1
for j in range(ny+1):
    for i in range(nx+1):
        model.PointObj.AddCartesian(i*dx, j*dy, 0, '', str(nid))
        nodes[(i,j)] = str(nid)
        nid += 1
eid = 1
for j in range(ny):
    for i in range(nx):
        pts = [nodes[(i,j)], nodes[(i+1,j)], nodes[(i+1,j+1)], nodes[(i,j+1)]]
        model.AreaObj.AddByPoint(4, pts, f'E{eid}', 'PLANE_S')
        eid += 1
for i in range(nx+1):
    model.PointObj.SetRestraint(nodes[(i,0)], [True,True,True,True,True,True])
model.LoadPatterns.Add('LAT', 8, 0, True)
p_n = 100.0 / (nx+1)
for i in range(nx+1):
    model.PointObj.SetLoadForce(nodes[(i,ny)], 'LAT', [p_n,0,0,0,0,0])
save_path = os.path.join(r'C:\CSiAPIexample', 'T6_PlaneStress.sdb')
model.File.Save(save_path)
model.Analyze.RunAnalysis()
model.Results.Setup.DeselectAllCasesAndCombosForOutput()
model.Results.Setup.SetCaseSelectedForOutput('LAT')
u_max_ps = 0.0
for i in range(nx+1):
    n = nodes[(i,ny)]
    NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]; OE=0
    [NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret] = \
        model.Results.JointDispl(n, OE, NR, Obj, Elm, AC, ST, SN, U1, U2, U3, R1, R2, R3)
    if NR > 0 and abs(U1[0]) > abs(u_max_ps):
        u_max_ps = U1[0]
w_ps = abs(u_max_ps)*1000
print(f"  SAP2000 Plane-Stress u_max = {w_ps:.4f} mm")
print(f"  Calcpad Membrane:          = 0.0589 mm")
print(f"  Ratio Membrane/Plane: {0.0589/w_ps:.4f}")
all_results.append(('Plane-Stress 8x6', 0.0589, w_ps))


# =====================================================
# SUMMARY
# =====================================================
print(f"\n{'='*70}")
print(f"  RESUMEN: Calcpad CLI vs SAP2000")
print(f"{'='*70}")
print(f"  {'Tipo':<25} {'Calcpad (mm)':>15} {'SAP2000 (mm)':>15} {'Ratio':>10}")
print(f"  {'-'*65}")
for name, cp, sap in all_results:
    if cp is not None:
        print(f"  {name:<25} {cp:>15.4f} {sap:>15.4f} {cp/sap:>10.4f}")
    else:
        print(f"  {name:<25} {'(pendiente)':>15} {sap:>15.4f} {'':>10}")
print(f"  {'-'*65}")
