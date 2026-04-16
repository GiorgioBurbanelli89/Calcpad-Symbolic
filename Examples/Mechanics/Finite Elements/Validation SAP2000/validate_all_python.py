"""validate_all_python.py — FEA validation: Python numpy vs SAP2000 vs Calcpad.

Runs 4 benchmark cases using the element implementations in:
  python_dkq.py      — DKQ Shell-Thin (Discrete Kirchhoff Quadrilateral)
  python_membrane.py — Membrane Q4 (plane stress)
  python_mindlin.py  — Mindlin-Reissner plate

Reference (hardcoded, no SAP2000 connection needed):
  SAP2000 values obtained from previously run SAP2000 analyses.
  Calcpad values from Calcpad-Symbolic .cpd files.

Units throughout: N, mm  (consistent system)
"""
import sys, os
import numpy as np

# ---------------------------------------------------------------------------
# Add current directory to path so we can import the element modules
# ---------------------------------------------------------------------------
_dir = os.path.dirname(os.path.abspath(__file__))
if _dir not in sys.path:
    sys.path.insert(0, _dir)

from python_dkq      import dkq_stiffness, dkq_load_uniform, dkq_bending_matrix
from python_membrane import membrane_stiffness, membrane_plane_stress
from python_mindlin  import mindlin_stiffness, mindlin_load_uniform, \
                             mindlin_bending_matrix, mindlin_shear_matrix


# ===========================================================================
# Helper: assemble global stiffness from element matrices
# ===========================================================================

def assemble_global(n_nodes, n_dof_node, elements, Ke_func, **kwargs):
    """Generic assembler.

    elements : list of (i,j,k,l) global node indices (0-based)
    Ke_func  : callable returning local stiffness (n_dof_elem × n_dof_elem)
    kwargs   : passed to Ke_func

    Returns K (n_total × n_total)
    """
    n_total = n_nodes * n_dof_node
    n_dof_elem = 4 * n_dof_node
    K = np.zeros((n_total, n_total))

    Ke = Ke_func(**kwargs)

    for nodes in elements:
        # DOF indices for this element
        dofs = []
        for n in nodes:
            for d in range(n_dof_node):
                dofs.append(n * n_dof_node + d)
        for a, ga in enumerate(dofs):
            for b, gb in enumerate(dofs):
                K[ga, gb] += Ke[a, b]
    return K


def assemble_global_variable_Ke(n_nodes, n_dof_node, elements, Ke_list):
    """Assemble from pre-computed element matrices (possibly different per element)."""
    n_total = n_nodes * n_dof_node
    K = np.zeros((n_total, n_total))
    for elem_nodes, Ke in zip(elements, Ke_list):
        dofs = []
        for n in elem_nodes:
            for d in range(n_dof_node):
                dofs.append(n * n_dof_node + d)
        for a, ga in enumerate(dofs):
            for b, gb in enumerate(dofs):
                K[ga, gb] += Ke[a, b]
    return K


def build_mesh(nx, ny):
    """Build rectangular mesh. Returns node_coords, elements.

    Nodes numbered row by row: node(i,j) = j*(nx+1) + i
    Elements: (node1, node2, node3, node4) = (bl, br, tr, tl)
    """
    n_nodes = (nx+1) * (ny+1)
    node_id = lambda i, j: j*(nx+1) + i

    elements = []
    for j in range(ny):
        for i in range(nx):
            bl = node_id(i,   j)
            br = node_id(i+1, j)
            tr = node_id(i+1, j+1)
            tl = node_id(i,   j+1)
            elements.append((bl, br, tr, tl))
    return n_nodes, elements


def apply_penalty(K, F, dofs, k_penalty=1e20):
    """Apply penalty constraints: K[d,d] += k_penalty, leave F unchanged."""
    for d in dofs:
        K[d, d] += k_penalty
    return K


# ===========================================================================
# CASE 1 — Placa Base e50 DKQ (Shell-Thin)
# ===========================================================================
def case1_placa_base_dkq():
    """Placa base 500×600mm, t=50mm, A36.

    Mesh: 11×13 nodes (10×12 elements), dx=dy=50mm
    Springs: k_winkler=120 N/mm on all 143 nodes
             k_corner=202810 N/mm on 4 corner nodes
    Load: P=-1480000 N at center node (index: jc)

    SAP2000:  w_center = -9.4336 mm
    Calcpad:  w_center = -9.4546 mm
    """
    # ---- Parameters ----
    E     = 200000.0   # MPa = N/mm^2
    nu    = 0.3
    t     = 50.0       # mm
    B_a   = 500.0      # mm  (width  in x)
    N_a   = 600.0      # mm  (height in y)
    nx    = 10
    ny    = 12
    dx    = B_a / nx   # 50 mm
    dy    = N_a / ny   # 50 mm
    P     = 1480000.0  # N  (magnitude, applied downward)

    k_winkler = 120.0      # N/mm  (SAP2000 value)
    k_corner  = 202810.0   # N/mm  (SAP2000 value, Winkler + bolt spring)

    n_dof_node = 3  # {w, theta_x, theta_y}

    # ---- Mesh ----
    n_nodes, elements = build_mesh(nx, ny)
    n_total = n_nodes * n_dof_node

    # ---- Constitutive ----
    D_b = dkq_bending_matrix(E, nu, t)
    Ke  = dkq_stiffness(dx, dy, D_b)

    # ---- Assemble K ----
    K = np.zeros((n_total, n_total))
    for elem_nodes in elements:
        dofs = []
        for n in elem_nodes:
            for d in range(n_dof_node):
                dofs.append(n * n_dof_node + d)
        for a, ga in enumerate(dofs):
            for b, gb in enumerate(dofs):
                K[ga, gb] += Ke[a, b]

    # ---- Winkler springs on all nodes (DOF w = index 3*k) ----
    for k in range(n_nodes):
        K[3*k, 3*k] += k_winkler

    # ---- Corner springs (additional) ----
    node_id = lambda i, j: j*(nx+1) + i
    corners = [
        node_id(0,  0),    # bottom-left
        node_id(nx, 0),    # bottom-right
        node_id(0,  ny),   # top-left
        node_id(nx, ny),   # top-right
    ]
    k_extra = k_corner - k_winkler
    for k in corners:
        K[3*k, 3*k] += k_extra

    # ---- Load vector: point load at center node ----
    # Center node: i=nx/2, j=ny/2 → node index jc
    ic = nx // 2   # = 5
    jc = ny // 2   # = 6
    center_node = node_id(ic, jc)
    F = np.zeros(n_total)
    F[3*center_node] = -P  # downward (negative w)

    # ---- Solve ----
    U = np.linalg.solve(K, F)

    w_center = U[3*center_node]
    return w_center


# ===========================================================================
# CASE 2 — Losa 6×4m DKQ (Shell-Thin, simply supported)
# ===========================================================================
def case2_losa_dkq():
    """Losa 6000×4000mm, t=100mm, concrete.

    Mesh: 6×4 elements (7×5 nodes), dx=1000, dy=1000
    BC: simply supported — w=0 on all boundary nodes
        tangential slope = 0 on boundary (w' = 0 in tangential direction)
        Implemented as: w=0 enforced via penalty; rotations free
        (Penalty only on w DOF at boundary — consistent with Navier BC)
    Load: q = 0.01 N/mm^2  (= 10 kN/m^2)

    Navier series: w_center ≈ 6.623 mm
    SAP2000:       w_center ≈ 6.529 mm (Plate-Thin Shell-Thin)
    Calcpad DKQ:   w_center =  6.529 mm
    """
    # ---- Parameters ----
    E   = 35000.0    # MPa
    nu  = 0.15
    t   = 100.0      # mm
    Lx  = 6000.0     # mm
    Ly  = 4000.0     # mm
    nx  = 6
    ny  = 4
    dx  = Lx / nx    # 1000 mm
    dy  = Ly / ny    # 1000 mm
    q   = 0.01       # N/mm^2

    n_dof_node = 3   # {w, theta_x, theta_y}

    # ---- Mesh ----
    n_nodes, elements = build_mesh(nx, ny)
    n_total = n_nodes * n_dof_node
    node_id = lambda i, j: j*(nx+1) + i

    # ---- Constitutive ----
    D_b = dkq_bending_matrix(E, nu, t)
    Ke  = dkq_stiffness(dx, dy, D_b)

    # ---- Load element vector ----
    fe  = dkq_load_uniform(dx, dy, q)

    # ---- Assemble K and F ----
    K = np.zeros((n_total, n_total))
    F = np.zeros(n_total)
    for elem_nodes in elements:
        dofs = []
        for n in elem_nodes:
            for d in range(n_dof_node):
                dofs.append(n * n_dof_node + d)
        for a, ga in enumerate(dofs):
            for b, gb in enumerate(dofs):
                K[ga, gb] += Ke[a, b]
        for a, ga in enumerate(dofs):
            F[ga] += fe[a]

    # ---- Simply supported BCs: w=0 on all boundary nodes ----
    # Only constrain w (transverse displacement) at boundary nodes.
    # Rotations are FREE — this matches the Navier/SAP2000 SS condition.
    # Constraining rotations too would over-stiffen (produce ~clamped response).
    k_pen = 1e20
    for i in range(nx+1):
        for j in range(ny+1):
            if i == 0 or i == nx or j == 0 or j == ny:
                n = node_id(i, j)
                K[3*n, 3*n] += k_pen   # w = 0 only, rotations free

    # ---- Solve ----
    U = np.linalg.solve(K, F)

    # ---- Center deflection: node (nx/2, ny/2) ----
    center_node = node_id(nx//2, ny//2)
    w_center = U[3*center_node]
    return w_center


# ===========================================================================
# CASE 3 — Membrane wall (Q4 plane stress)
# ===========================================================================
def case3_membrane_wall():
    """Cantilever wall 5000×3000mm, t=200mm, concrete.

    Mesh: 8×6 elements (9×7 nodes), dx=625mm, dy=500mm
    BC: fixed base — u=v=0 at all nodes in bottom row (j=0)
    Load: P=100000 N lateral (x-direction) distributed at top nodes

    Euler-Bernoulli theory (cantilever):
      u_tip = PL^3 / (3EI)  where I = t*H^3/12
    """
    # ---- Parameters ----
    E   = 25000.0    # MPa = N/mm^2
    nu  = 0.2
    t   = 200.0      # mm
    W   = 5000.0     # mm  (length in x = span of wall)
    H   = 3000.0     # mm  (height in y)
    nx  = 8
    ny  = 6
    dx  = W / nx     # 625 mm
    dy  = H / ny     # 500 mm
    P   = 100000.0   # N

    n_dof_node = 2   # {u, v}

    # ---- Mesh ----
    n_nodes, elements = build_mesh(nx, ny)
    n_total = n_nodes * n_dof_node
    node_id = lambda i, j: j*(nx+1) + i

    # ---- Constitutive ----
    D_m = membrane_plane_stress(E, nu, t)
    Ke  = membrane_stiffness(dx, dy, D_m)

    # ---- Assemble K ----
    K = np.zeros((n_total, n_total))
    for elem_nodes in elements:
        dofs = []
        for n in elem_nodes:
            for d in range(n_dof_node):
                dofs.append(n * n_dof_node + d)
        for a, ga in enumerate(dofs):
            for b, gb in enumerate(dofs):
                K[ga, gb] += Ke[a, b]

    # ---- Load: distribute P evenly over top nodes ----
    F = np.zeros(n_total)
    n_top = nx + 1   # 9 nodes
    p_per_node = P / n_top
    for i in range(nx+1):
        n = node_id(i, ny)
        F[2*n] = p_per_node   # x-direction (u DOF)

    # ---- BCs: penalty on fixed base (j=0) ----
    k_pen = 1e20
    for i in range(nx+1):
        n = node_id(i, 0)
        K[2*n,   2*n]   += k_pen   # u = 0
        K[2*n+1, 2*n+1] += k_pen   # v = 0

    # ---- Solve ----
    U = np.linalg.solve(K, F)

    # ---- Max horizontal displacement at top ----
    u_vals = []
    for i in range(nx+1):
        n = node_id(i, ny)
        u_vals.append(U[2*n])
    u_max = max(u_vals, key=abs)

    # ---- Euler-Bernoulli reference ----
    # Cantilever beam: u = PL^3/(3EI), I = t*H^3/12
    # NOTE: Here L=W (horizontal), H=height, loading lateral (horizontal)
    # Actually the wall is loaded IN-PLANE: it's a shear wall
    # Width W along x, height H along y, load P in x at top
    # Treat as 2D plane stress, not a beam. Use simple shear estimate:
    #   u_shear = P*H / (G*A) = P*H / (E/(2*(1+nu)) * t*W)
    # Bending estimate:
    #   For cantilever fixed at base, free at top, loaded at tip:
    #   Treat width W as "thickness", height H as "length"
    #   EI = E * (W * t^3 / 12) — NOT applicable here (W >> t)
    # Better: use bending of shear wall treated as a deep beam
    #   Horizontal disp at top of vertical cantilever (W x H) under horizontal load P at top:
    #   u = P*H^3 / (3*E*I_v)  where I_v = t*W^3/12 (bending about vertical axis)
    # This gives cantilever bending of the wall (deflection along H)
    I_v = t * W**3 / 12.0
    u_bending = P * H**3 / (3.0 * E * I_v)

    return u_max, u_bending


# ===========================================================================
# CASE 4 — Mindlin plate (Losa 6×4m)
# ===========================================================================
def case4_losa_mindlin():
    """Same losa 6000×4000mm as Case 2 but with Mindlin element.

    t/L = 100/4000 = 1/40 → thin plate, Mindlin should match DKQ closely.

    Uses selective integration (1×1 shear) to avoid shear locking.
    """
    # ---- Parameters ----
    E   = 35000.0
    nu  = 0.15
    t   = 100.0
    Lx  = 6000.0
    Ly  = 4000.0
    nx  = 6
    ny  = 4
    dx  = Lx / nx
    dy  = Ly / ny
    q   = 0.01       # N/mm^2

    n_dof_node = 3

    # ---- Mesh ----
    n_nodes, elements = build_mesh(nx, ny)
    n_total = n_nodes * n_dof_node
    node_id = lambda i, j: j*(nx+1) + i

    # ---- Constitutive ----
    D_b = mindlin_bending_matrix(E, nu, t)
    D_s = mindlin_shear_matrix(E, nu, t)
    Ke  = mindlin_stiffness(dx, dy, D_b, D_s)
    fe  = mindlin_load_uniform(dx, dy, q)

    # ---- Assemble K and F ----
    K = np.zeros((n_total, n_total))
    F = np.zeros(n_total)
    for elem_nodes in elements:
        dofs = []
        for n in elem_nodes:
            for d in range(n_dof_node):
                dofs.append(n * n_dof_node + d)
        for a, ga in enumerate(dofs):
            for b, gb in enumerate(dofs):
                K[ga, gb] += Ke[a, b]
        for a, ga in enumerate(dofs):
            F[ga] += fe[a]

    # ---- Simply supported BCs (same as Case 2): w=0 only, rotations free ----
    k_pen = 1e20
    for i in range(nx+1):
        for j in range(ny+1):
            if i == 0 or i == nx or j == 0 or j == ny:
                n = node_id(i, j)
                K[3*n, 3*n] += k_pen   # w = 0 only

    # ---- Solve ----
    U = np.linalg.solve(K, F)

    center_node = node_id(nx//2, ny//2)
    w_center = U[3*center_node]
    return w_center


# ===========================================================================
# Navier analytical solution for simply supported rectangular plate
# ===========================================================================
def navier_plate(a, b, t, E, nu, q, n_terms=51):
    """Navier series solution for SS rectangular plate under uniform load.

    a, b: plate dimensions (mm)
    q:    uniform load (N/mm^2)
    Returns center deflection w (mm), positive downward.
    """
    D = E * t**3 / (12.0 * (1.0 - nu**2))
    w = 0.0
    for m in range(1, n_terms, 2):   # odd terms only
        for n in range(1, n_terms, 2):
            Amn = 16.0 * q / (np.pi**6 * D * m * n *
                  ((m/a)**2 + (n/b)**2)**2)
            w += Amn * np.sin(m*np.pi*0.5) * np.sin(n*np.pi*0.5)
    return w


# ===========================================================================
# Euler-Bernoulli reference for cantilever wall
# ===========================================================================
def eb_cantilever_wall(E, t, W, H, P):
    """Horizontal displacement at top of vertical plate wall under lateral load.

    The wall is modeled as a 2D shear wall. For pure bending of the wall
    (treated as a column with width W and height H):
      u_tip (flexural) = P * H^3 / (3 * E * I)
      where I = t * W^3 / 12 (bending about the out-of-plane axis of the wall)

    NOTE: Plane-stress FEA includes BOTH bending and shear deformation,
          while E-B only gives bending. For a short wall (H/W < 1), shear
          contributes significantly. Add shear deformation:
          u_shear = P * H / (G * A) = P * H * 2*(1+nu) / (E * t * W)
    """
    G = E / (2.0 * (1.0 + nu_wall))
    I = t * W**3 / 12.0
    u_bend  = P * H**3 / (3.0 * E * I)
    u_shear = P * H / (G * t * W)
    return u_bend, u_shear, u_bend + u_shear


nu_wall = 0.2  # module-level for EB function


# ===========================================================================
# MAIN — run all cases and print summary
# ===========================================================================
if __name__ == '__main__':
    print("=" * 70)
    print("  FEA VALIDATION — Python numpy vs SAP2000 vs Calcpad")
    print("=" * 70)

    # ---- Reference values (hardcoded) ----
    sap2000_placa   = -9.4336   # mm  (SAP2000 Shell-Thin, DKQ-equivalent)
    sap2000_losa    =  6.529    # mm  (SAP2000 Plate-Thin / Shell-Thin, 6x4 mesh)
    sap2000_membrane = 0.0589   # mm  (SAP2000 Membrane, 8x6 mesh)
    sap2000_mindlin =  6.529    # mm  (SAP2000 Plate-Thick, similar to DKQ for thin plate)

    calcpad_placa   = -9.4546   # mm  (Calcpad DKQ, 07_Placa_Base_e50_DKQ_regular.cpd)
    calcpad_losa_dkq=  6.529    # mm  (Calcpad DKQ, losa 6x4)
    # Mindlin and Membrane Calcpad values: run this script and report
    calcpad_mindlin = None       # to be filled from this run
    calcpad_membrane= None       # to be filled from this run

    # Navier analytical
    navier_losa = navier_plate(6000.0, 4000.0, 100.0, 35000.0, 0.15, 0.01)

    results = []

    # ---- CASE 1: Placa Base DKQ ----
    print("\n" + "-" * 70)
    print("  CASE 1: Placa Base 500x600mm, t=50mm, DKQ Shell-Thin")
    print("  Mesh 10x12 (11x13 nodes), dx=dy=50mm")
    print("  Springs: Winkler 120 N/mm + corner bolts 202810 N/mm")
    print("  Load: P = -1,480,000 N at center")
    print("-" * 70)

    w1 = case1_placa_base_dkq()
    print(f"  Python DKQ:   w_center = {w1:.4f} mm")
    print(f"  SAP2000:      w_center = {sap2000_placa:.4f} mm")
    print(f"  Calcpad:      w_center = {calcpad_placa:.4f} mm")
    print(f"  Ratio Py/SAP: {w1/sap2000_placa:.4f}")
    results.append(("Placa Base DKQ", w1, sap2000_placa, calcpad_placa,
                     w1/sap2000_placa))

    # ---- CASE 2: Losa 6x4 DKQ ----
    print("\n" + "-" * 70)
    print("  CASE 2: Losa 6000x4000mm, t=100mm, E=35000 MPa, DKQ Shell-Thin")
    print("  Mesh 6x4 elements (7x5 nodes), dx=dy=1000mm")
    print("  Simply supported, q = 0.01 N/mm²")
    print(f"  Navier analytical: w_center = {navier_losa:.4f} mm")
    print("-" * 70)

    w2 = case2_losa_dkq()
    print(f"  Python DKQ:   w_center = {w2:.4f} mm")
    print(f"  SAP2000:      w_center = {sap2000_losa:.4f} mm")
    print(f"  Calcpad DKQ:  w_center = {calcpad_losa_dkq:.4f} mm")
    print(f"  Navier:       w_center = {navier_losa:.4f} mm")
    print(f"  Ratio Py/SAP:    {w2/sap2000_losa:.4f}")
    print(f"  Ratio Py/Navier: {w2/navier_losa:.4f}")
    results.append(("Losa 6x4 DKQ", w2, sap2000_losa, calcpad_losa_dkq,
                     w2/sap2000_losa))

    # ---- CASE 3: Membrane wall ----
    print("\n" + "-" * 70)
    print("  CASE 3: Membrane wall 5000x3000mm, t=200mm, E=25000 MPa")
    print("  Mesh 8x6 elements (9x7 nodes), dx=625mm, dy=500mm")
    print("  Fixed base, lateral load P=100000 N at top")
    print("-" * 70)

    u3, u_eb = case3_membrane_wall()
    # Euler-Bernoulli bending + shear for shear wall (H=3000, W=5000, t=200)
    E_wall, nu_wall_val, t_wall = 25000.0, 0.2, 200.0
    W_wall, H_wall, P_wall = 5000.0, 3000.0, 100000.0
    G_wall = E_wall / (2.0*(1.0+nu_wall_val))
    I_v = t_wall * W_wall**3 / 12.0
    u_bend  = P_wall * H_wall**3 / (3.0 * E_wall * I_v)
    u_shear = P_wall * H_wall / (G_wall * t_wall * W_wall)
    u_total_eb = u_bend + u_shear

    print(f"  Python Q4:    u_top     = {u3:.4f} mm")
    print(f"  SAP2000:      u_top     = {sap2000_membrane:.4f} mm")
    print(f"  E-B bending:  u_bend    = {u_bend:.4f} mm")
    print(f"  E-B shear:    u_shear   = {u_shear:.4f} mm")
    print(f"  E-B total:    u_total   = {u_total_eb:.4f} mm")
    print(f"  Ratio Py/SAP: {u3/sap2000_membrane:.4f}")
    print(f"  Ratio Py/E-B: {u3/u_total_eb:.4f}")
    results.append(("Membrane wall 8x6", u3, sap2000_membrane, None,
                     u3/sap2000_membrane))

    # ---- CASE 4: Losa Mindlin ----
    print("\n" + "-" * 70)
    print("  CASE 4: Losa 6000x4000mm Mindlin (same as Case 2, selective int.)")
    print("  Mesh 6x4 elements, simply supported, q = 0.01 N/mm²")
    print("-" * 70)

    w4 = case4_losa_mindlin()
    print(f"  Python Mindlin: w_center = {w4:.4f} mm")
    print(f"  Python DKQ:     w_center = {w2:.4f} mm")
    print(f"  SAP2000:        w_center = {sap2000_losa:.4f} mm")
    print(f"  Navier:         w_center = {navier_losa:.4f} mm")
    print(f"  Ratio Mindlin/DKQ: {w4/w2:.4f}")
    print(f"  Ratio Mindlin/SAP: {w4/sap2000_losa:.4f}")
    results.append(("Losa 6x4 Mindlin", w4, sap2000_mindlin, None,
                     w4/sap2000_mindlin))

    # ---- SUMMARY TABLE ----
    print("\n")
    print("=" * 78)
    print("  SUMMARY TABLE")
    print("=" * 78)
    hdr = f"  {'Case':<22} {'Python (mm)':>12} {'SAP2000 (mm)':>13} {'Calcpad (mm)':>13} {'Ratio Py/SAP':>13}"
    print(hdr)
    print("  " + "-" * 74)
    for name, py_val, sap_val, cp_val, ratio in results:
        cp_str  = f"{cp_val:.4f}"  if cp_val  is not None else "    N/A"
        print(f"  {name:<22} {py_val:>12.4f} {sap_val:>13.4f} {cp_str:>13} {ratio:>13.4f}")
    print("  " + "-" * 74)

    print("\nNotes:")
    print(f"  Navier (losa 6x4): {navier_losa:.4f} mm")
    print(f"  E-B (membrane wall): bend={u_bend:.4f} + shear={u_shear:.4f} = {u_total_eb:.4f} mm")
    print("  DKQ = Discrete Kirchhoff Quadrilateral (thin plate, no shear)")
    print("  Mindlin = Mindlin-Reissner (includes shear, selective integration)")
    print("  Membrane = Q4 plane-stress (in-plane DOFs only)")
