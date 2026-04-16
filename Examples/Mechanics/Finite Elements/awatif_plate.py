"""Awatif Plate Example in Python
   Irregular quadrilateral plate, Shell-Thick MITC4, uniform load, all edges fixed.
   Points: (0,0), (15,0), (15,10), (0,5) — trapezoidal shape
"""
import numpy as np
import math
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.tri as mtri
from matplotlib.patches import Polygon
from matplotlib.collections import PatchCollection

# === Data ===
E = 100.0; nu = 0.3; t = 1.0; G = E/(2*(1+nu)); kappa = 5./6.
q = -3.0  # uniform load in Z

# Plate corners (same as Awatif)
corners = np.array([
    [0, 0],    # 0
    [15, 0],   # 1
    [15, 10],  # 2
    [0, 5],    # 3
])

# === Generate mesh: structured quad mesh mapped to irregular shape ===
nx = 20; ny = 14
nj = (nx+1)*(ny+1); nd = 3; nt = nd*nj

# Map regular (s,t) in [0,1]^2 to physical coords using bilinear interpolation
xj = np.zeros(nj); yj = np.zeros(nj)
for j in range(ny+1):
    t_param = j / ny
    for i in range(nx+1):
        s_param = i / nx
        k = j*(nx+1)+i
        # Bilinear: (1-s)(1-t)*P0 + s(1-t)*P1 + st*P2 + (1-s)t*P3
        xj[k] = (1-s_param)*(1-t_param)*corners[0,0] + s_param*(1-t_param)*corners[1,0] + \
                 s_param*t_param*corners[2,0] + (1-s_param)*t_param*corners[3,0]
        yj[k] = (1-s_param)*(1-t_param)*corners[0,1] + s_param*(1-t_param)*corners[1,1] + \
                 s_param*t_param*corners[2,1] + (1-s_param)*t_param*corners[3,1]

ne = nx*ny
ej = np.zeros((ne,4), dtype=int)
for j in range(ny):
    for i in range(nx):
        e = j*nx+i; bl = j*(nx+1)+i
        ej[e] = [bl, bl+1, bl+nx+2, bl+nx+1]

print(f"Mesh: {nj} nodes, {ne} elements (mapped quads)")

# === MITC4 Assembly ===
Db = E*t**3/(12*(1-nu**2))*np.array([[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]])
Ds = kappa*G*t*np.array([[1,0],[0,1]])
gp = 1/math.sqrt(3)

K = np.zeros((nt,nt))
for e in range(ne):
    ns = ej[e]
    # Physical coordinates of element nodes
    xe = xj[ns]; ye = yj[ns]
    Ke = np.zeros((12,12))

    for xi_g, eta_g in [(-gp,-gp),(gp,-gp),(gp,gp),(-gp,gp)]:
        # Shape function derivatives in natural coords
        dNdxi = np.array([-(1-eta_g)/4, (1-eta_g)/4, (1+eta_g)/4, -(1+eta_g)/4])
        dNdeta = np.array([-(1-xi_g)/4, -(1+xi_g)/4, (1+xi_g)/4, (1-xi_g)/4])
        N = np.array([(1-xi_g)*(1-eta_g)/4, (1+xi_g)*(1-eta_g)/4,
                      (1+xi_g)*(1+eta_g)/4, (1-xi_g)*(1+eta_g)/4])

        # Jacobian
        J = np.array([[dNdxi@xe, dNdxi@ye],
                       [dNdeta@xe, dNdeta@ye]])
        detJ = np.linalg.det(J)
        Jinv = np.linalg.inv(J)

        # Physical derivatives
        dNdx = Jinv[0,0]*dNdxi + Jinv[0,1]*dNdeta
        dNdy = Jinv[1,0]*dNdxi + Jinv[1,1]*dNdeta

        # Bending B
        Bb = np.zeros((3,12))
        for i in range(4):
            Bb[0,3*i+2] = -dNdx[i]
            Bb[1,3*i+1] = dNdy[i]
            Bb[2,3*i+1] = dNdx[i]
            Bb[2,3*i+2] = -dNdy[i]
        Ke += Bb.T @ Db @ Bb * detJ

        # MITC4 Shear
        Bs_m = np.zeros((2,12))
        for eta_t in [-1.,1.]:
            w_t = (1+eta_t*eta_g)/2
            dNdxi_t = np.array([-(1-eta_t)/4, (1-eta_t)/4, (1+eta_t)/4, -(1+eta_t)/4])
            dNdeta_t = np.array([-(1-xi_g)/4, -(1+xi_g)/4, (1+xi_g)/4, (1-xi_g)/4])
            N_t = np.array([(1-xi_g)*(1-eta_t)/4, (1+xi_g)*(1-eta_t)/4,
                            (1+xi_g)*(1+eta_t)/4, (1-xi_g)*(1+eta_t)/4])
            J_t = np.array([[dNdxi_t@xe, dNdxi_t@ye],
                             [dNdeta_t@xe, dNdeta_t@ye]])
            Jinv_t = np.linalg.inv(J_t)
            dNdx_t = Jinv_t[0,0]*dNdxi_t + Jinv_t[0,1]*dNdeta_t
            for i in range(4):
                Bs_m[0,3*i] += w_t*dNdx_t[i]
                Bs_m[0,3*i+2] += w_t*(-N_t[i])

        for xi_t in [-1.,1.]:
            w_t = (1+xi_t*xi_g)/2
            dNdxi_t = np.array([-(1-eta_g)/4, (1-eta_g)/4, (1+eta_g)/4, -(1+eta_g)/4])
            dNdeta_t = np.array([-(1-xi_t)/4, -(1+xi_t)/4, (1+xi_t)/4, (1-xi_t)/4])
            N_t = np.array([(1-xi_t)*(1-eta_g)/4, (1+xi_t)*(1-eta_g)/4,
                            (1+xi_t)*(1+eta_g)/4, (1-xi_t)*(1+eta_g)/4])
            J_t = np.array([[dNdxi_t@xe, dNdxi_t@ye],
                             [dNdeta_t@xe, dNdeta_t@ye]])
            Jinv_t = np.linalg.inv(J_t)
            dNdy_t = Jinv_t[1,0]*dNdxi_t + Jinv_t[1,1]*dNdeta_t
            for i in range(4):
                Bs_m[1,3*i] += w_t*dNdy_t[i]
                Bs_m[1,3*i+1] += w_t*N_t[i]

        Ke += Bs_m.T @ Ds @ Bs_m * detJ

    dofs = []
    for n in ns: dofs.extend([nd*n, nd*n+1, nd*n+2])
    for i in range(12):
        for j in range(12):
            K[dofs[i],dofs[j]] += Ke[i,j]

# === Load: uniform q distributed to nodes ===
F = np.zeros(nt)
for e in range(ne):
    ns = ej[e]
    xe = xj[ns]; ye = yj[ns]
    # Element area (quad)
    Ae = 0.5*abs((xe[2]-xe[0])*(ye[3]-ye[1]) - (xe[3]-xe[1])*(ye[2]-ye[0]))
    for n in ns:
        F[nd*n] += q * Ae / 4

# === BC: all boundary nodes fixed (w=θx=θy=0) ===
penalty = 1e10
for j in range(ny+1):
    for i in range(nx+1):
        k = j*(nx+1)+i
        if i == 0 or i == nx or j == 0 or j == ny:
            K[nd*k, nd*k] += penalty
            K[nd*k+1, nd*k+1] += penalty
            K[nd*k+2, nd*k+2] += penalty

# === Solve ===
Z = np.linalg.solve(K, F)
w = Z[::3]

print(f"w_min = {w.min():.6f}")
print(f"w_max = {w.max():.6f}")

# Find center-ish node
kc = (ny//2)*(nx+1) + nx//2
print(f"w_center = {w[kc]:.6f}")

# === Plot ===
fig, axes = plt.subplots(1, 2, figsize=(14, 5))

# 1. Color map with element patches
ax = axes[0]
ax.set_aspect('equal')
ax.set_title(f'Awatif Plate (MITC4) — w min={w.min():.4f}')

patches = []
colors_elem = []
for e in range(ne):
    ns = ej[e]
    verts = list(zip(xj[ns], yj[ns]))
    patches.append(Polygon(verts, closed=True))
    colors_elem.append(np.mean(w[ns]))

pc = PatchCollection(patches, cmap='jet')
pc.set_array(np.array(colors_elem))
ax.add_collection(pc)
plt.colorbar(pc, ax=ax, label='w')
ax.set_xlim(-1, 16); ax.set_ylim(-1, 11)

# Draw mesh edges
for e in range(ne):
    ns = ej[e]
    xs = list(xj[ns]) + [xj[ns[0]]]
    ys = list(yj[ns]) + [yj[ns[0]]]
    ax.plot(xs, ys, 'k-', lw=0.3, alpha=0.5)

# 2. Contour plot with triangulation
ax2 = axes[1]
ax2.set_aspect('equal')
ax2.set_title('Contour (triangulated)')

# Triangulate from quads
tris = []
for e in range(ne):
    ns = ej[e]
    tris.append([ns[0], ns[1], ns[2]])
    tris.append([ns[0], ns[2], ns[3]])
tris = np.array(tris)

triang = mtri.Triangulation(xj, yj, tris)
cf = ax2.tricontourf(triang, w, levels=20, cmap='jet')
plt.colorbar(cf, ax=ax2, label='w')
ax2.triplot(triang, 'k-', lw=0.2, alpha=0.3)

plt.tight_layout()
plt.savefig('awatif_plate_results.png', dpi=150, bbox_inches='tight')
print("Saved: awatif_plate_results.png")
