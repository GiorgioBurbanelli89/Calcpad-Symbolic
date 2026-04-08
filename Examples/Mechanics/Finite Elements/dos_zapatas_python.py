"""Dos Zapatas Independientes - Python MITC4
   Zapata 1 (medianera): 2x2m, Col en (250,1000), P1=80tf
   Zapata 2 (centrada): 2.5x2m, Col en centro, P2=120tf
   Comparar presion de contacto con Calcpad y SAP2000"""
import numpy as np
import math
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle
from matplotlib.collections import PatchCollection

E=25000.; nu=0.2; t=500.; G=E/(2*(1+nu)); kappa=5./6.
ks=0.000002*9806.65  # N/mm^3

def mitc4_solve(Lx, Ly, de, xCol, yCol, Pu_tf, E, nu, t, ks):
    """Solve single footing with MITC4"""
    G=E/(2*(1+nu)); kappa=5./6.
    Pu=Pu_tf*9806.65
    nx=int(round(Lx/de)); ny=int(round(Ly/de))
    nj1x=nx+1; nj1y=ny+1; nj=nj1x*nj1y; nd=3; nt=nd*nj

    xj=np.zeros(nj); yj=np.zeros(nj)
    for j in range(nj1y):
        for i in range(nj1x):
            k=j*nj1x+i; xj[k]=i*de; yj[k]=j*de

    ej=np.zeros((nx*ny,4),dtype=int)
    for j in range(ny):
        for i in range(nx):
            e=j*nx+i; bl=j*nj1x+i; ej[e]=[bl,bl+1,bl+nj1x+1,bl+nj1x]

    Db=E*t**3/(12*(1-nu**2))*np.array([[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]])
    Ds=kappa*G*t*np.array([[1,0],[0,1]])
    gp=1/math.sqrt(3)

    K=np.zeros((nt,nt))
    for e in range(nx*ny):
        ns=ej[e]; dx_e=xj[ns[2]]-xj[ns[0]]; dy_e=yj[ns[2]]-yj[ns[0]]
        J11=dx_e/2; J22=dy_e/2; detJ=J11*J22
        Ke=np.zeros((12,12))
        for xi_g,eta_g in [(-gp,-gp),(gp,-gp),(gp,gp),(-gp,gp)]:
            dNdx=np.array([-(1-eta_g)/4,(1-eta_g)/4,(1+eta_g)/4,-(1+eta_g)/4])/J11
            dNdy=np.array([-(1-xi_g)/4,-(1+xi_g)/4,(1+xi_g)/4,(1-xi_g)/4])/J22
            Bb=np.zeros((3,12))
            for i in range(4):
                Bb[0,3*i+2]=-dNdx[i]; Bb[1,3*i+1]=dNdy[i]
                Bb[2,3*i+1]=dNdx[i]; Bb[2,3*i+2]=-dNdy[i]
            Ke+=Bb.T@Db@Bb*detJ
            # MITC4
            Bs_m=np.zeros((2,12))
            for eta_t in [-1.,1.]:
                w_t=(1+eta_t*eta_g)/2
                dNdx_t=np.array([-(1-eta_t)/4,(1-eta_t)/4,(1+eta_t)/4,-(1+eta_t)/4])/J11
                N_t=np.array([(1-xi_g)*(1-eta_t)/4,(1+xi_g)*(1-eta_t)/4,(1+xi_g)*(1+eta_t)/4,(1-xi_g)*(1+eta_t)/4])
                for i in range(4):
                    Bs_m[0,3*i]+=w_t*dNdx_t[i]; Bs_m[0,3*i+2]+=w_t*(-N_t[i])
            for xi_t in [-1.,1.]:
                w_t=(1+xi_t*xi_g)/2
                dNdy_t=np.array([-(1-xi_t)/4,-(1+xi_t)/4,(1+xi_t)/4,(1-xi_t)/4])/J22
                N_t=np.array([(1-xi_t)*(1-eta_g)/4,(1+xi_t)*(1-eta_g)/4,(1+xi_t)*(1+eta_g)/4,(1-xi_t)*(1+eta_g)/4])
                for i in range(4):
                    Bs_m[1,3*i]+=w_t*dNdy_t[i]; Bs_m[1,3*i+1]+=w_t*N_t[i]
            Ke+=Bs_m.T@Ds@Bs_m*detJ
        dofs=[]
        for n in ns: dofs.extend([nd*n,nd*n+1,nd*n+2])
        for i in range(12):
            for j in range(12): K[dofs[i],dofs[j]]+=Ke[i,j]

    # Winkler
    for j in range(nj1y):
        for i in range(nj1x):
            k=j*nj1x+i
            tx=de if 0<i<nx else de/2
            ty=de if 0<j<ny else de/2
            K[nd*k,nd*k]+=ks*tx*ty

    # Load
    Lc=400.
    F=np.zeros(nt)
    qc=Pu/(Lc**2)
    for j in range(nj1y):
        for i in range(nj1x):
            k=j*nj1x+i
            if abs(xj[k]-xCol)<=Lc/2+de/2 and abs(yj[k]-yCol)<=Lc/2+de/2:
                tx=de if 0<i<nx else de/2; ty=de if 0<j<ny else de/2
                if abs(xj[k]-xCol)==Lc/2: tx=de/2
                if abs(yj[k]-yCol)==Lc/2: ty=de/2
                F[nd*k]-=qc*tx*ty

    Z=np.linalg.solve(K,F)
    w=Z[::3]
    q_soil = ks * w / 9806.65 * 1e6  # tonf/m^2

    return xj, yj, w, q_soil, ej, nx, ny

# === Zapata 1: medianera ===
xj1, yj1, w1, q1, ej1, nx1, ny1 = mitc4_solve(2000, 2000, 250, 250, 1000, 80, E, nu, t, ks)

# === Zapata 2: centrada ===
xj2, yj2, w2, q2, ej2, nx2, ny2 = mitc4_solve(2500, 2000, 250, 1250, 1000, 120, E, nu, t, ks)

print("=== PYTHON MITC4 ===")
print(f"Zapata 1: w_min={w1.min():.3f}mm, q_min={q1.min():.2f} tonf/m2, q_max={q1.max():.2f} tonf/m2")
print(f"Zapata 2: w_min={w2.min():.3f}mm, q_min={q2.min():.2f} tonf/m2, q_max={q2.max():.2f} tonf/m2")

# Check equilibrium
R1 = sum(ks * w1[k] * (250 if 0<k%(nx1+1)<nx1 else 125) * (250 if 0<k//(nx1+1)<ny1 else 125) for k in range(len(w1)))
R2 = sum(ks * w2[k] * (250 if 0<k%(nx2+1)<nx2 else 125) * (250 if 0<k//(nx2+1)<ny2 else 125) for k in range(len(w2)))
print(f"R1 = {R1/9806.65:.2f} tonf (P1=80), R2 = {R2/9806.65:.2f} tonf (P2=120)")

# === PLOT ===
fig, axes = plt.subplots(2, 2, figsize=(14, 8))
fig.suptitle('Dos Zapatas Independientes - Python MITC4', fontsize=13, fontweight='bold')

for col, (xj_p, yj_p, data, ej_p, title, Lx, Ly) in enumerate([
    (xj1, yj1, w1, ej1, 'Zapata 1 (medianera)\nDeflexion w (mm)', 2000, 2000),
    (xj2, yj2, w2, ej2, 'Zapata 2 (centrada)\nDeflexion w (mm)', 2500, 2000),
]):
    ax = axes[0, col]; ax.set_aspect('equal')
    patches = []
    colors_e = []
    ne = len(ej_p)
    for e in range(ne):
        ns = ej_p[e]
        verts = list(zip(xj_p[ns], yj_p[ns]))
        patches.append(plt.Polygon(verts, closed=True))
        colors_e.append(np.mean(data[ns]))
    pc = PatchCollection(patches, cmap='jet')
    pc.set_array(np.array(colors_e))
    ax.add_collection(pc); plt.colorbar(pc, ax=ax, shrink=0.8)
    ax.set_title(title); ax.set_xlim(-50, Lx+50); ax.set_ylim(-50, Ly+50)
    for e in range(ne):
        ns = ej_p[e]
        xs = list(xj_p[ns]) + [xj_p[ns[0]]]; ys = list(yj_p[ns]) + [yj_p[ns[0]]]
        ax.plot(xs, ys, 'k-', lw=0.3, alpha=0.3)

for col, (xj_p, yj_p, data, ej_p, title, Lx, Ly) in enumerate([
    (xj1, yj1, q1, ej1, 'Zapata 1\nPresion q (tonf/m2)', 2000, 2000),
    (xj2, yj2, q2, ej2, 'Zapata 2\nPresion q (tonf/m2)', 2500, 2000),
]):
    ax = axes[1, col]; ax.set_aspect('equal')
    patches = []
    colors_e = []
    ne = len(ej_p)
    for e in range(ne):
        ns = ej_p[e]
        verts = list(zip(xj_p[ns], yj_p[ns]))
        patches.append(plt.Polygon(verts, closed=True))
        colors_e.append(np.mean(data[ns]))
    pc = PatchCollection(patches, cmap='jet')
    pc.set_array(np.array(colors_e))
    ax.add_collection(pc); plt.colorbar(pc, ax=ax, shrink=0.8)
    ax.set_title(title); ax.set_xlim(-50, Lx+50); ax.set_ylim(-50, Ly+50)
    for e in range(ne):
        ns = ej_p[e]
        xs = list(xj_p[ns]) + [xj_p[ns[0]]]; ys = list(yj_p[ns]) + [yj_p[ns[0]]]
        ax.plot(xs, ys, 'k-', lw=0.3, alpha=0.3)

plt.tight_layout()
plt.savefig('dos_zapatas_python.png', dpi=150, bbox_inches='tight')
print("\nSaved: dos_zapatas_python.png")

# === Comparar con Calcpad ===
print(f"\n=== COMPARACION ===")
print(f"Calcpad Zapata 1: w Col1 = -74.312 mm")
print(f"Python  Zapata 1: w Col1 = {w1[4*(nx1+1)+1]:.3f} mm")
print(f"Calcpad Zapata 2: w Col2 = -42.766 mm")
kc2 = (ny2//2)*(nx2+1) + nx2//2
print(f"Python  Zapata 2: w Col2 = {w2[kc2]:.3f} mm")
