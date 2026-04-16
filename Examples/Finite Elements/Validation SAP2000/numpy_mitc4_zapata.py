"""Validar Tutorial MITC4 con NumPy puro (sin SAP2000).
   Replica exacta del tutorial Calcpad: zapata 1.5x1.5m, P=20tonf, Area Spring.
   Implementa MITC4 + Winkler Area Spring desde cero.
   Esto es una verificacion independiente del resultado de Calcpad."""
import numpy as np

# === Datos (N, mm) — iguales a Calcpad ===
E = 22000.0
nu = 0.2
G = E / (2 * (1 + nu))
t = 400.0
kappa = 5/6
ks = 0.03  # N/mm^3
L = 1500.0; B = 1500.0
P = 20 * 9806.65  # N
ne1 = 6
de = L / ne1
nj1 = ne1 + 1
nj = nj1 * nj1
nd = 3
nt = nd * nj

print(f"=== NumPy MITC4 — Zapata 1.5x1.5m ===")
print(f"E={E} MPa, t={t}mm, ks={ks}, de={de}mm")

# Nodes
xj = np.zeros(nj); yj = np.zeros(nj)
for j in range(nj1):
    for i in range(nj1):
        k = j*nj1 + i
        xj[k] = i*de; yj[k] = j*de

# Connectivity
ne = ne1 * ne1
ej = np.zeros((ne, 4), dtype=int)
for j in range(ne1):
    for i in range(ne1):
        e = j*ne1 + i
        bl = j*nj1 + i
        ej[e, 0] = bl
        ej[e, 1] = bl + 1
        ej[e, 2] = bl + nj1 + 1
        ej[e, 3] = bl + nj1

# Constitutive matrices
D_b = E * t**3 / (12 * (1 - nu**2)) * np.array([
    [1, nu, 0],
    [nu, 1, 0],
    [0, 0, (1-nu)/2]
])
D_s = kappa * G * t * np.array([[1, 0], [0, 1]])

# Shape function derivatives
def dN_xi(eta):
    return np.array([-(1-eta)/4, (1-eta)/4, (1+eta)/4, -(1+eta)/4])
def dN_eta(xi):
    return np.array([-(1-xi)/4, -(1+xi)/4, (1+xi)/4, (1-xi)/4])
def N_q4(xi, eta):
    return np.array([
        (1-xi)*(1-eta)/4,
        (1+xi)*(1-eta)/4,
        (1+xi)*(1+eta)/4,
        (1-xi)*(1+eta)/4
    ])

# Gauss 2x2
gp = 1/np.sqrt(3)
gauss_pts = [(-gp,-gp),(gp,-gp),(gp,gp),(-gp,gp)]

# Assemble global K
K = np.zeros((nt, nt))
for e in range(ne):
    n1, n2, n3, n4 = ej[e]
    dx = xj[n3] - xj[n1]
    dy = yj[n3] - yj[n1]
    J11 = dx/2; J22 = dy/2
    detJ = J11 * J22

    Ke = np.zeros((12, 12))

    # Bending (2x2 Gauss)
    for xi, eta in gauss_pts:
        dNx = dN_xi(eta) / J11
        dNy = dN_eta(xi) / J22
        Bb = np.zeros((3, 12))
        for i in range(4):
            Bb[0, 3*i+2] = -dNx[i]
            Bb[1, 3*i+1] = dNy[i]
            Bb[2, 3*i+1] = dNx[i]
            Bb[2, 3*i+2] = -dNy[i]
        Ke += Bb.T @ D_b @ Bb * detJ

    # Shear MITC4 (2x2 Gauss)
    for xi, eta in gauss_pts:
        Bs = np.zeros((2, 12))
        # gamma_xz interpolated at eta=-1 and eta=+1
        for eta_t, sign in [(-1, (1-eta)/2), (1, (1+eta)/2)]:
            wt = sign
            dNx_t = dN_xi(eta_t) / J11
            N_t = N_q4(xi, eta_t)
            for i in range(4):
                Bs[0, 3*i+0] += wt * dNx_t[i]
                Bs[0, 3*i+2] += -wt * N_t[i]
        # gamma_yz interpolated at xi=-1 and xi=+1
        for xi_t, sign in [(-1, (1-xi)/2), (1, (1+xi)/2)]:
            wt = sign
            dNy_t = dN_eta(xi_t) / J22
            N_t = N_q4(xi_t, eta)
            for i in range(4):
                Bs[1, 3*i+0] += wt * dNy_t[i]
                Bs[1, 3*i+1] += wt * N_t[i]
        Ke += Bs.T @ D_s @ Bs * detJ

    # Winkler Area Spring: K_w = ks * integral(N^T N) dA
    # Only affects DOF w (1st DOF of each node)
    Kw = np.zeros((4, 4))
    for xi, eta in gauss_pts:
        N = N_q4(xi, eta)
        Kw += np.outer(N, N) * detJ
    Kw *= ks
    for i in range(4):
        for j in range(4):
            Ke[3*i+0, 3*j+0] += Kw[i, j]

    # Assemble to global K
    dofs = []
    for n in [n1, n2, n3, n4]:
        dofs.extend([3*n, 3*n+1, 3*n+2])
    for i in range(12):
        for j in range(12):
            K[dofs[i], dofs[j]] += Ke[i, j]

# Load vector — center node
ic = ne1 // 2
jc = ne1 // 2
kc = jc*nj1 + ic
F = np.zeros(nt)
F[3*kc + 0] = -P  # w DOF
print(f"Load at node {kc+1} (i={ic}, j={jc}) = ({xj[kc]},{yj[kc]})")

# Solve
u = np.linalg.solve(K, F)

# Extract w displacements
w = u[0::3]
print(f"\nw_center = {w[kc]:.5f} mm")
print(f"w_corner = {w[0]:.5f} mm")
print(f"w_max = {w.min():.5f} mm (most compressed)")
print(f"\nCalcpad reference: w_center = -2.95408 mm")
print(f"Difference: {abs(w[kc] - (-2.95408)):.5f} mm ({abs((w[kc] + 2.95408)/2.95408)*100:.3f}%)")

# Soil pressure
q = ks * w / 9806.65 * 1e6  # tonf/m2
print(f"\nq_center = {q[kc]:.2f} tonf/m2")
print(f"q_max = {q.min():.2f} tonf/m2 (compression)")
print(f"Calcpad: q_max = -9.04 tonf/m2")

# Integral of pressure = total force
q_N_per_mm2 = ks * w  # N/mm^2 (NEGATIVE)
# Integrate using element areas
total_force = 0
for e in range(ne):
    n1, n2, n3, n4 = ej[e]
    dx = xj[n3] - xj[n1]
    dy = yj[n3] - yj[n1]
    A_e = dx * dy
    # Average pressure of element
    q_avg = (q_N_per_mm2[n1] + q_N_per_mm2[n2] + q_N_per_mm2[n3] + q_N_per_mm2[n4]) / 4
    total_force += q_avg * A_e
print(f"\nTotal reaction from soil = {total_force:.1f} N")
print(f"Applied load = -{P:.1f} N")
print(f"Equilibrium ratio = {total_force / (-P):.6f} (should be ~1.0)")
