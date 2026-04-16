"""DKT (Discrete Kirchhoff Triangle) - Batoz, Bathe & Ho 1980
   Derivacion desde restricciones de Kirchhoff en puntos medios.
   3 nodos, 3 DOF/nodo (w, θx, θy), 9 DOF/elem
   Validacion: losa simplemente apoyada 6x4m, q=10 kN/m²"""
import numpy as np
import math
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
from scipy.interpolate import griddata

def dkt_stiffness(xy, Db):
    """DKT element stiffness matrix.
    xy: (3,2) coordinates of triangle vertices
    Db: (3,3) bending constitutive matrix
    Returns: (9,9) stiffness matrix, scalar area

    DOFs: [w1, θx1, θy1, w2, θx2, θy2, w3, θx3, θy3]
    Convention: βx = -θy (slope in x), βy = θx (slope in y)

    Side numbering:
    Side 4: nodes 2→3 (opposite node 1)
    Side 5: nodes 3→1 (opposite node 2)
    Side 6: nodes 1→2 (opposite node 3)
    """
    x = xy[:,0]; y = xy[:,1]

    # Side vectors
    x12=x[0]-x[1]; x23=x[1]-x[2]; x31=x[2]-x[0]
    y12=y[0]-y[1]; y23=y[1]-y[2]; y31=y[2]-y[0]
    L4sq=x23**2+y23**2; L5sq=x31**2+y31**2; L6sq=x12**2+y12**2

    # DKT parameters
    P4=-6*x23/L4sq; P5=-6*x31/L5sq; P6=-6*x12/L6sq
    T4=-6*y23/L4sq; T5=-6*y31/L5sq; T6=-6*y12/L6sq
    Q4=3*x23*y23/L4sq; Q5=3*x31*y31/L5sq; Q6=3*x12*y12/L6sq
    R4=3*y23**2/L4sq; R5=3*y31**2/L5sq; R6=3*y12**2/L6sq

    # Area
    x21=-x12; y21=-y12
    detJ = x21*y31 - x31*y21  # = 2*Area (signed)
    Area = abs(detJ)/2

    # Quadratic shape function derivatives
    # N4=4*L2*L3, N5=4*L3*L1, N6=4*L1*L2 with L1=1-xi-eta, xi=L2, eta=L3
    # dN4/dxi=4*L3, dN4/deta=4*L2
    # dN5/dxi=-4*L3, dN5/deta=4*(L1-L3)
    # dN6/dxi=4*(L1-L2), dN6/deta=-4*L2

    # Gauss 3-point rule for triangle
    gpts = [(2./3, 1./6, 1./6), (1./6, 2./3, 1./6), (1./6, 1./6, 2./3)]

    Ke = np.zeros((9,9))

    for L1, L2, L3 in gpts:
        # Shape function derivatives
        dN4x=4*L3; dN4e=4*L2
        dN5x=-4*L3; dN5e=4*(L1-L3)
        dN6x=4*(L1-L2); dN6e=-4*L2

        # dHx/dxi (9 components) - derived from Hx = f(N4,N5,N6,L1,L2,L3)
        # Hx[i] for βx: {w1,θx1,θy1, w2,θx2,θy2, w3,θx3,θy3}
        dHx_xi = np.array([
            dN6x*(-P6/4) + dN5x*(P5/4),                     # w1
            dN6x*(-Q6/4) + dN5x*(-Q5/4),                     # θx1
            1 + dN6x*(3-R6)/4 + dN5x*(3-R5)/4,              # θy1 (-dL1/dxi=1)
            dN6x*(P6/4) + dN4x*(-P4/4),                      # w2
            dN6x*(-Q6/4) + dN4x*(-Q4/4),                     # θx2
            -1 + dN6x*(3-R6)/4 + dN4x*(3-R4)/4,             # θy2 (-dL2/dxi=-... wait)
            dN4x*(P4/4) + dN5x*(-P5/4),                      # w3
            dN4x*(-Q4/4) + dN5x*(-Q5/4),                     # θx3
            dN4x*(3-R4)/4 + dN5x*(3-R5)/4,                   # θy3 (dL3/dxi=0)
        ])

        # dHx/deta (9 components)
        dHx_eta = np.array([
            dN6e*(-P6/4) + dN5e*(P5/4),
            dN6e*(-Q6/4) + dN5e*(-Q5/4),
            1 + dN6e*(3-R6)/4 + dN5e*(3-R5)/4,              # -dL1/deta=1
            dN6e*(P6/4) + dN4e*(-P4/4),
            dN6e*(-Q6/4) + dN4e*(-Q4/4),
            dN6e*(3-R6)/4 + dN4e*(3-R4)/4,                   # dL2/deta=0
            dN4e*(P4/4) + dN5e*(-P5/4),
            dN4e*(-Q4/4) + dN5e*(-Q5/4),
            -1 + dN4e*(3-R4)/4 + dN5e*(3-R5)/4,             # -dL3/deta=-1 -> +(-1)
        ])

        # dHy/dxi and dHy/deta (for βy)
        dHy_xi = np.array([
            dN6x*(-T6/4) + dN5x*(T5/4),                     # w1
            1 + dN6x*(-R6/4) + dN5x*(-R5/4),                # θx1 (-dL1/dxi=1)
            dN6x*(Q6/4) + dN5x*(Q5/4),                       # θy1
            dN6x*(T6/4) + dN4x*(-T4/4),                      # w2
            -1 + dN6x*(-R6/4) + dN4x*(-R4/4),               # θx2
            dN6x*(Q6/4) + dN4x*(Q4/4),                       # θy2
            dN4x*(T4/4) + dN5x*(-T5/4),                      # w3
            dN4x*(-R4/4) + dN5x*(-R5/4),                     # θx3
            dN4x*(Q4/4) + dN5x*(Q5/4),                       # θy3
        ])

        dHy_eta = np.array([
            dN6e*(-T6/4) + dN5e*(T5/4),
            1 + dN6e*(-R6/4) + dN5e*(-R5/4),                # -dL1/deta=1
            dN6e*(Q6/4) + dN5e*(Q5/4),
            dN6e*(T6/4) + dN4e*(-T4/4),
            dN6e*(-R6/4) + dN4e*(-R4/4),                     # dL2/deta=0
            dN6e*(Q6/4) + dN4e*(Q4/4),
            dN4e*(T4/4) + dN5e*(-T5/4),
            dN4e*(-R4/4) + dN5e*(-R5/4),
            -1 + dN4e*(Q4/4) + dN5e*(Q5/4),                 # -dL3/deta=-1
        ])

        # Transform to physical coordinates
        # df/dx = (y31*df/dxi - y21*df/deta) / detJ
        # df/dy = (-x31*df/dxi + x21*df/deta) / detJ
        dHx_dx = (y31*dHx_xi - y21*dHx_eta) / detJ
        dHx_dy = (-x31*dHx_xi + x21*dHx_eta) / detJ
        dHy_dx = (y31*dHy_xi - y21*dHy_eta) / detJ
        dHy_dy = (-x31*dHy_xi + x21*dHy_eta) / detJ

        # B-matrix: {κx, κy, 2κxy} = {dβx/dx, -dβy/dy, dβx/dy - dβy/dx}
        B = np.zeros((3, 9))
        B[0,:] = dHx_dx       # κx = dβx/dx
        B[1,:] = -dHy_dy      # κy = -dβy/dy
        B[2,:] = dHx_dy - dHy_dx  # 2κxy

        # Weight: 1/3 for each Gauss point, area = |detJ|/2
        Ke += B.T @ Db @ B * abs(detJ)/2 * (1./3)

    return Ke, Area

# === Test: Losa simplemente apoyada 6x4m ===
a=6000.; b=4000.; t=100.; E=35000.; nu=0.15; q=0.01  # N/mm²
D = E*t**3/(12*(1-nu**2))
Db = D*np.array([[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]])

# Navier exact
w_navier = 0
for m in range(1,20,2):
    for n in range(1,20,2):
        amn = 16*q/(math.pi**6*m*n*((m/a)**2+(n/b)**2)**2*D)
        w_navier += amn*math.sin(m*math.pi/2)*math.sin(n*math.pi/2)
print(f"Navier w_center = {w_navier:.4f} mm")

# Mesh
nx=12; ny=8; nd=3
nj=(nx+1)*(ny+1); nt=nd*nj
xj=np.zeros(nj); yj=np.zeros(nj)
for j in range(ny+1):
    for i in range(nx+1): k=j*(nx+1)+i; xj[k]=i*a/nx; yj[k]=j*b/ny

ne=2*nx*ny
ej=np.zeros((ne,3),dtype=int)
e=0
for j in range(ny):
    for i in range(nx):
        bl=j*(nx+1)+i
        ej[e]=[bl,bl+1,bl+nx+2]; e+=1
        ej[e]=[bl,bl+nx+2,bl+nx+1]; e+=1

print(f"Malla: {nj} nodos, {ne} triangulos")

# Assemble
K=np.zeros((nt,nt))
for e in range(ne):
    ns=ej[e]; xy=np.column_stack([xj[ns],yj[ns]])
    Ke,Ae = dkt_stiffness(xy, Db)
    dofs=[]
    for n in ns: dofs.extend([nd*n,nd*n+1,nd*n+2])
    for i in range(9):
        for j in range(9): K[dofs[i],dofs[j]]+=Ke[i,j]

# Load (tributary area per node)
F=np.zeros(nt)
for e in range(ne):
    ns=ej[e]; xy=np.column_stack([xj[ns],yj[ns]])
    A2=abs((xy[1,0]-xy[0,0])*(xy[2,1]-xy[0,1])-(xy[2,0]-xy[0,0])*(xy[1,1]-xy[0,1]))
    Ae=A2/2
    for n in ns: F[nd*n]-=q*Ae/3

# BC: simply supported (w=0 on edges)
penalty=1e15
for k in range(nj):
    if abs(xj[k])<1 or abs(xj[k]-a)<1 or abs(yj[k])<1 or abs(yj[k]-b)<1:
        K[nd*k,nd*k]+=penalty

Z=np.linalg.solve(K,F)
w=Z[::3]

kc=(ny//2)*(nx+1)+nx//2
print(f"\nDKT w_center = {w[kc]:.4f} mm")
print(f"Navier       = {w_navier:.4f} mm")
print(f"Ratio        = {w[kc]/w_navier:.4f}")

# Plot
xg=np.linspace(0,a,100); yg=np.linspace(0,b,80)
Xg,Yg=np.meshgrid(xg,yg)
Wg=griddata((xj,yj),w,(Xg,Yg),method='cubic')
fig,ax=plt.subplots(figsize=(10,7)); ax.set_aspect('equal')
cf=ax.contourf(Xg,Yg,Wg,levels=20,cmap='jet')
plt.colorbar(cf,ax=ax,label='w (mm)')
ax.set_title(f'DKT Losa 6x4m - w={w[kc]:.3f}mm (Navier={w_navier:.3f}mm) ratio={w[kc]/w_navier:.3f}')
for e in range(ne):
    ns=ej[e]; tri=np.append(ns,ns[0])
    ax.plot(xj[tri],yj[tri],'w-',lw=0.15,alpha=0.3)
plt.tight_layout()
plt.savefig('placa_dkt_results.png',dpi=150,bbox_inches='tight')
print("Saved: placa_dkt_results.png")
