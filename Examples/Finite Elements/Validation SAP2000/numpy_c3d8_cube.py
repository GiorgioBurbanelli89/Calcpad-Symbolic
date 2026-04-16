"""Validacion independiente del Tutorial C3D8 con NumPy puro.

Replica exactamente la formulacion del C3D8 hexaedro en Calcpad:
- 8 shape functions trilineales
- Jacobiano 3x3 (constante para cubo)
- Matriz D 6x6 isotropica 3D
- Matriz B 6x24 (3 filas por nudo)
- Integracion Gauss 2x2x2 (8 puntos)
- K_ele = sum B^T D B det(J)

Objetivo: verificar que w_calcpad = w_numpy para el cubo 1000mm bajo
compresion uniforme 100 kN. Validacion analitica: w = -P*L/(A*E) = -0.004545 mm
"""
import numpy as np

# === Datos (N-mm) — iguales al tutorial Calcpad ===
Lx = 1000.0
Ly = 1000.0
Lz = 1000.0
E = 22000.0
nu = 0.2
lam = E * nu / ((1 + nu) * (1 - 2*nu))  # Lame 1
mu = E / (2 * (1 + nu))                  # Lame 2 (G)

# 8 nudos del cubo (orden Abaqus C3D8)
Xn = np.array([0, Lx, Lx, 0, 0, Lx, Lx, 0])
Yn = np.array([0, 0, Ly, Ly, 0, 0, Ly, Ly])
Zn = np.array([0, 0, 0, 0, Lz, Lz, Lz, Lz])

# === Matriz constitutiva D (6x6) — 3D isotropico ===
D = np.array([
    [lam + 2*mu, lam, lam, 0, 0, 0],
    [lam, lam + 2*mu, lam, 0, 0, 0],
    [lam, lam, lam + 2*mu, 0, 0, 0],
    [0, 0, 0, mu, 0, 0],
    [0, 0, 0, 0, mu, 0],
    [0, 0, 0, 0, 0, mu],
])
print("D (6x6):")
print(D)

# === Shape function derivatives (trilinear) ===
# Signos de cada nudo en coordenadas naturales
xi_sign = np.array([-1, 1, 1, -1, -1, 1, 1, -1], dtype=float)
eta_sign = np.array([-1, -1, 1, 1, -1, -1, 1, 1], dtype=float)
zeta_sign = np.array([-1, -1, -1, -1, 1, 1, 1, 1], dtype=float)

def dN(xi, eta, zeta):
    """Derivadas (dN_i/dxi, dN_i/deta, dN_i/dzeta) para i=0..7."""
    dNxi = np.zeros(8)
    dNeta = np.zeros(8)
    dNzeta = np.zeros(8)
    for i in range(8):
        dNxi[i] = xi_sign[i] * (1 + eta_sign[i]*eta) * (1 + zeta_sign[i]*zeta) / 8
        dNeta[i] = (1 + xi_sign[i]*xi) * eta_sign[i] * (1 + zeta_sign[i]*zeta) / 8
        dNzeta[i] = (1 + xi_sign[i]*xi) * (1 + eta_sign[i]*eta) * zeta_sign[i] / 8
    return dNxi, dNeta, dNzeta

# Jacobiano constante para el cubo
J11 = Lx / 2
J22 = Ly / 2
J33 = Lz / 2
detJ = J11 * J22 * J33

# === Ensamblaje K con Gauss 2x2x2 ===
gp = 1.0 / np.sqrt(3)
gauss_pts = [(-gp, -gp, -gp), (gp, -gp, -gp), (gp, gp, -gp), (-gp, gp, -gp),
             (-gp, -gp, gp), (gp, -gp, gp), (gp, gp, gp), (-gp, gp, gp)]

K_ele = np.zeros((24, 24))

for xi, eta, zeta in gauss_pts:
    dNxi, dNeta, dNzeta = dN(xi, eta, zeta)
    dNx = dNxi / J11
    dNy = dNeta / J22
    dNz = dNzeta / J33

    # Matriz B (6 x 24)
    B = np.zeros((6, 24))
    for i in range(8):
        c = 3 * i
        B[0, c + 0] = dNx[i]        # eps_xx = du/dx
        B[1, c + 1] = dNy[i]        # eps_yy = dv/dy
        B[2, c + 2] = dNz[i]        # eps_zz = dw/dz
        B[3, c + 0] = dNy[i]        # eps_xy = du/dy + dv/dx
        B[3, c + 1] = dNx[i]
        B[4, c + 1] = dNz[i]        # eps_yz = dv/dz + dw/dy
        B[4, c + 2] = dNy[i]
        B[5, c + 0] = dNz[i]        # eps_zx = du/dz + dw/dx
        B[5, c + 2] = dNx[i]

    # Contribucion al K
    K_ele += B.T @ D @ B * detJ

print(f"\nK_ele (24x24), det={np.linalg.det(K_ele):.3e}")
print(f"K_ele[0,0] = {K_ele[0, 0]:.4f} N/mm")
print(f"K_ele[2,2] = {K_ele[2, 2]:.4f} N/mm")

# === Aplicar BCs y cargas ===
K = K_ele.copy()

# Cargas: 25 kN compresion en cada nudo superior (5,6,7,8 → indices 4,5,6,7)
# DOF UZ de cada nudo: 3*i + 2 (0-indexed)
P_total = 100000.0  # N (100 kN)
P_per_node = P_total / 4
F = np.zeros(24)
for i in [4, 5, 6, 7]:
    F[3 * i + 2] = -P_per_node

# Restricciones (penalty method):
# - Nudos base (0,1,2,3): UZ = 0
# - Nudo 0: UX = UY = 0 (fija 3 traslaciones)
# - Nudo 1: UY = 0 (fija rotacion en Z)
pen = 1e15
# UZ en nudos 0-3
for i in [0, 1, 2, 3]:
    K[3*i + 2, 3*i + 2] += pen
# UX en nudo 0
K[0, 0] += pen
# UY en nudos 0, 1
K[1, 1] += pen
K[4, 4] += pen

# Resolver
u = np.linalg.solve(K, F)

print("\n=== Desplazamientos ===")
for i in range(8):
    print(f"  Nudo {i+1}: UX={u[3*i]:.7f}  UY={u[3*i+1]:.7f}  UZ={u[3*i+2]:.7f} mm")

# UZ promedio superior
uz_avg = np.mean([u[3*i + 2] for i in [4, 5, 6, 7]])
print(f"\nUZ promedio (nudos 5-8) = {uz_avg:.7f} mm")

# Teorico
uz_teo = -P_total * Lz / (Lx * Ly * E)
print(f"Teorico (P*L/(A*E))    = {uz_teo:.7f} mm")

# Calcpad
uz_calcpad = -0.004545455
print(f"Calcpad FEM C3D8       = {uz_calcpad:.7f} mm")

# Comparacion
diff_np_calcpad = abs(uz_avg - uz_calcpad)
diff_np_teo = abs(uz_avg - uz_teo)

print(f"\nDiff NumPy vs Calcpad: {diff_np_calcpad:.3e} mm")
print(f"Diff NumPy vs Teorico: {diff_np_teo:.3e} mm")

print(f"\n=== VALIDACION ===")
if diff_np_calcpad < 1e-6 and diff_np_teo < 1e-6:
    print("PERFECT MATCH: NumPy = Calcpad = Teorico")
    print("La formulacion C3D8 de Calcpad es MATEMATICAMENTE CORRECTA")
else:
    print("Diferencias detectadas")
