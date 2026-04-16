"""Placa Base e50 - DKQ Shell-Thin validation: Python vs SAP2000 vs Calcpad
Malla irregular 9x9 identica a SAP2000"""

import numpy as np
import math

# --- Material ---
E = 200000.0  # MPa = N/mm2
nu = 0.3
t = 50.0  # mm

# --- Mesh ---
x_ax = np.array([0, 50, 100, 175, 250, 325, 400, 450, 500], dtype=float)
y_ax = np.array([0, 50, 112, 174, 300, 426, 488, 550, 600], dtype=float)
nx, ny = len(x_ax), len(y_ax)  # 9, 9
n_j = nx * ny  # 81
n_e = (nx-1) * (ny-1)  # 64
ndof = 3
n_total = ndof * n_j  # 243

# Node coordinates
x_j = np.zeros(n_j)
y_j = np.zeros(n_j)
for iy in range(ny):
    for ix in range(nx):
        k = iy * nx + ix
        x_j[k] = x_ax[ix]
        y_j[k] = y_ax[iy]

# Element connectivity (0-based)
e_j = np.zeros((n_e, 4), dtype=int)
for iy in range(ny-1):
    for ix in range(nx-1):
        e = iy * (nx-1) + ix
        bl = iy * nx + ix
        e_j[e] = [bl, bl+1, bl+nx+1, bl+nx]

# --- D matrix ---
D_b = E * t**3 / (12 * (1 - nu**2)) * np.array([
    [1, nu, 0],
    [nu, 1, 0],
    [0, 0, (1-nu)/2]
])

# --- Gauss 2x2 ---
g = 1.0 / math.sqrt(3)
gauss_pts = [(-g, -g), (g, -g), (g, g), (-g, g)]


def dkq_element_stiffness(dx, dy, D_b):
    """DKQ element stiffness 12x12 for rectangular element dx x dy"""
    K_e = np.zeros((12, 12))
    dJ = dx * dy / 4.0

    for xi, eta in gauss_pts:
        B = np.zeros((3, 12))

        # Row 1: kappa_x = d(beta_x)/dx, beta_x = theta_y
        # Cols: w, theta_x, theta_y per node
        for i, (s_xi, s_eta) in enumerate([(-1,-1),(1,-1),(1,1),(-1,1)]):
            c = 3 * i
            # sign factors
            sx = s_xi  # +1 or -1 for xi direction
            se = s_eta  # +1 or -1 for eta direction

        # Node 1 (-1,-1)
        B[0, 0] = 3*xi*(1-eta)/dx**2
        B[0, 2] = (1-eta)*(3*xi-1)/(2*dx)
        # Node 2 (+1,-1)
        B[0, 3] = -3*xi*(1-eta)/dx**2
        B[0, 5] = (1-eta)*(1+3*xi)/(2*dx)
        # Node 3 (+1,+1)
        B[0, 6] = -3*xi*(1+eta)/dx**2
        B[0, 8] = (1+eta)*(1+3*xi)/(2*dx)
        # Node 4 (-1,+1)
        B[0, 9] = 3*xi*(1+eta)/dx**2
        B[0, 11] = (1+eta)*(3*xi-1)/(2*dx)

        # Row 2: kappa_y = d(beta_y)/dy = -d(theta_x)/dy
        B[1, 0] = 3*(1-xi)*eta/dy**2
        B[1, 1] = -(1-xi)*(3*eta-1)/(2*dy)
        B[1, 3] = 3*(1+xi)*eta/dy**2
        B[1, 4] = -(1+xi)*(3*eta-1)/(2*dy)
        B[1, 6] = -3*(1+xi)*eta/dy**2
        B[1, 7] = -(1+xi)*(1+3*eta)/(2*dy)
        B[1, 9] = -3*(1-xi)*eta/dy**2
        B[1, 10] = -(1-xi)*(1+3*eta)/(2*dy)

        # Row 3: 2*kappa_xy
        TT = 3*(2 - xi**2 - eta**2)/(2*dx*dy)
        B[2, 0] = TT
        B[2, 1] = -(1-eta)*(1+3*eta)/(4*dx)
        B[2, 2] = (1-xi)*(1+3*xi)/(4*dy)
        B[2, 3] = -TT
        B[2, 4] = (1-eta)*(1+3*eta)/(4*dx)
        B[2, 5] = (1+xi)*(1-3*xi)/(4*dy)
        B[2, 6] = TT
        B[2, 7] = -(1+eta)*(3*eta-1)/(4*dx)
        B[2, 8] = (1+xi)*(3*xi-1)/(4*dy)
        B[2, 9] = -TT
        B[2, 10] = -(1+eta)*(1-3*eta)/(4*dx)
        B[2, 11] = -(1-xi)*(1+3*xi)/(4*dy)

        K_e += B.T @ D_b @ B * dJ

    return K_e


# --- Assembly ---
K = np.zeros((n_total, n_total))

for e in range(n_e):
    nodes = e_j[e]
    x1, y1 = x_j[nodes[0]], y_j[nodes[0]]
    x3, y3 = x_j[nodes[2]], y_j[nodes[2]]
    dx = x3 - x1
    dy = y3 - y1
    K_e = dkq_element_stiffness(dx, dy, D_b)

    # Assemble
    dofs = []
    for n in nodes:
        dofs.extend([ndof*n, ndof*n+1, ndof*n+2])
    dofs = np.array(dofs)
    for i in range(12):
        for j in range(12):
            K[dofs[i], dofs[j]] += K_e[i, j]

# --- Springs (tributary area, same as SAP2000) ---
k_w = 50000.0  # kN/m3

for iy in range(ny):
    for ix in range(nx):
        # Tributary area
        if ix == 0:
            tx = (x_ax[1] - x_ax[0]) / 2.0
        elif ix == nx-1:
            tx = (x_ax[-1] - x_ax[-2]) / 2.0
        else:
            tx = (x_ax[ix] - x_ax[ix-1]) / 2.0 + (x_ax[ix+1] - x_ax[ix]) / 2.0

        if iy == 0:
            ty = (y_ax[1] - y_ax[0]) / 2.0
        elif iy == ny-1:
            ty = (y_ax[-1] - y_ax[-2]) / 2.0
        else:
            ty = (y_ax[iy] - y_ax[iy-1]) / 2.0 + (y_ax[iy+1] - y_ax[iy]) / 2.0

        kn = k_w * tx * ty / 1e9  # kN/mm -> N/mm: * 1000, but k_w is kN/m3
        # k_w kN/m3 * tx mm * ty mm / 1e9 = kN/mm
        # Convert to N/mm: * 1000
        kn_N = kn * 1000.0

        k_idx = (iy * nx + ix) * ndof
        K[k_idx, k_idx] += kn_N

# --- Bolt springs ---
k_bolt = E * math.pi * (25.4/2)**2 / 500.0  # N/mm
bolt_positions = [(1, 1), (7, 1), (1, 7), (7, 7)]  # (ix, iy)
for ix, iy in bolt_positions:
    k_idx = (iy * nx + ix) * ndof
    K[k_idx, k_idx] += k_bolt

# --- Load ---
F = np.zeros(n_total)
# Center node: ix=4, iy=4 -> node 4*9+4 = 40 (0-based)
jc = 4 * nx + 4  # node 40 (0-based)
print(f"Center node (0-based): {jc}, coords: ({x_j[jc]}, {y_j[jc]})")
F[jc * ndof] = -1480000.0  # N

# --- Solve ---
Z = np.linalg.solve(K, F)
w_center = Z[jc * ndof]

print(f"\n{'='*60}")
print(f"PLACA BASE e50 - DKQ Shell-Thin - Malla irregular 9x9")
print(f"{'='*60}")
print(f"  Python DKQ:     w_center = {w_center:.6f} mm")
print(f"  SAP2000:        w_center = -6.056858 mm")
print(f"  Calcpad DKQ:    w_center = -6.219302 mm")
print(f"  Ratio Py/SAP:   {w_center / -6.056858:.6f}")
print(f"  Ratio Py/Calc:  {w_center / -6.219302:.6f}")
print(f"{'='*60}")

# Also print some other node displacements for verification
print("\nDisplacements at corner and edge nodes:")
for node_name, ix, iy in [("Corner(0,0)", 0, 0), ("Bolt(50,50)", 1, 1),
                           ("Edge(250,0)", 4, 0), ("Center(250,300)", 4, 4),
                           ("Bolt(450,550)", 7, 7), ("Corner(500,600)", 8, 8)]:
    k = iy * nx + ix
    w = Z[k * ndof]
    print(f"  {node_name:20s}: w = {w:.6f} mm")
