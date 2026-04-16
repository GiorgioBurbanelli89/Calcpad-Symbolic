"""Zapata medianera + viga de amarre + zapata centrada
   MITC4 Shell-Thick, Winkler solo en zapatas"""
import numpy as np, math
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
from scipy.interpolate import griddata
from matplotlib.patches import Rectangle

# === Datos ===
E=25000.; nu=0.2; t_zap=500.; t_viga=500.
G=E/(2*(1+nu)); kappa=5./6.
ks=0.000002*9806.65  # N/mm^3

# Zapata 1 (medianera): 2000x2500, columna en borde izq
Lz1=2000.; Bz1=2500.; Lc1=400.
xC1=200.; yC1=1250.  # borde izq, centro Y

# Viga: 3000x400
Lv=3000.; Bv=400.

# Zapata 2 (centrada): 2000x2000, columna en centro
Lz2=2000.; Bz2=2000.; Lc2=400.
xC2=Lz1+Lv+Lz2/2; yC2=Bz2/2

# Cargas
P1=80.*9806.65; P2=120.*9806.65

Ltotal=Lz1+Lv+Lz2; Bmax=max(Bz1,Bz2)
yviga=(yC1+yC2)/2  # centro de viga en Y

print(f"Ltotal={Ltotal}mm, Bmax={Bmax}mm")
print(f"Col1: ({xC1},{yC1}), Col2: ({xC2},{yC2})")
print(f"Viga centro Y: {yviga}")

# === Malla ===
de=200.
nx=int(Ltotal/de); ny=int(Bmax/de)
nj1x=nx+1; nj1y=ny+1; nj=nj1x*nj1y; nd=3; nt=nd*nj

xj=np.zeros(nj); yj=np.zeros(nj)
for j in range(nj1y):
    for i in range(nj1x):
        k=j*nj1x+i; xj[k]=i*de; yj[k]=j*de

# Classify elements
ne=nx*ny
ej=np.zeros((ne,4),dtype=int)
eType=np.zeros(ne,dtype=int)
for j in range(ny):
    for i in range(nx):
        e=j*nx+i; bl=j*nj1x+i
        ej[e]=[bl,bl+1,bl+nj1x+1,bl+nj1x]
        xce=(i+0.5)*de; yce=(j+0.5)*de
        if xce<Lz1 and yce<Bz1:
            eType[e]=1  # zapata 1
        elif Lz1<=xce<=Lz1+Lv and abs(yce-yviga)<Bv/2+de/2:
            eType[e]=2  # viga
        elif xce>Lz1+Lv and yce<Bz2:
            eType[e]=3  # zapata 2

nea=np.sum(eType>0)
print(f"Elementos activos: {nea} de {ne}")

# === MITC4 Assembly ===
Db_z=E*t_zap**3/(12*(1-nu**2))*np.array([[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]])
Ds_z=kappa*G*t_zap*np.array([[1,0],[0,1]])
Db_v=E*t_viga**3/(12*(1-nu**2))*np.array([[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]])
Ds_v=kappa*G*t_viga*np.array([[1,0],[0,1]])
gp=1/math.sqrt(3)

K=np.zeros((nt,nt))
for e in range(ne):
    if eType[e]==0: continue
    ns=ej[e]; dx=xj[ns[2]]-xj[ns[0]]; dy=yj[ns[2]]-yj[ns[0]]
    J11=dx/2; J22=dy/2; detJ=J11*J22
    Db_e=Db_v if eType[e]==2 else Db_z
    Ds_e=Ds_v if eType[e]==2 else Ds_z
    Ke=np.zeros((12,12))
    for xi_g,eta_g in [(-gp,-gp),(gp,-gp),(gp,gp),(-gp,gp)]:
        dNdx=np.array([-(1-eta_g)/4,(1-eta_g)/4,(1+eta_g)/4,-(1+eta_g)/4])/J11
        dNdy=np.array([-(1-xi_g)/4,-(1+xi_g)/4,(1+xi_g)/4,(1-xi_g)/4])/J22
        Bb=np.zeros((3,12))
        for i in range(4):
            Bb[0,3*i+2]=-dNdx[i]; Bb[1,3*i+1]=dNdy[i]
            Bb[2,3*i+1]=dNdx[i]; Bb[2,3*i+2]=-dNdy[i]
        Ke+=Bb.T@Db_e@Bb*detJ
        # MITC4 shear
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
        Ke+=Bs_m.T@Ds_e@Bs_m*detJ
    dofs=[]
    for n in ns: dofs.extend([nd*n,nd*n+1,nd*n+2])
    for i in range(12):
        for j in range(12): K[dofs[i],dofs[j]]+=Ke[i,j]

# Winkler (solo zapatas)
for j in range(nj1y):
    for i in range(nj1x):
        k=j*nj1x+i
        en_zap=False
        if xj[k]<=Lz1 and yj[k]<=Bz1: en_zap=True
        if xj[k]>=Lz1+Lv and yj[k]<=Bz2: en_zap=True
        if en_zap:
            tx=de if 0<i<nx else de/2
            ty=de if 0<j<ny else de/2
            K[nd*k,nd*k]+=ks*tx*ty

# Penalty for nodes outside domain
pen=1e12
for j in range(nj1y):
    for i in range(nj1x):
        k=j*nj1x+i
        en_dom=False
        if xj[k]<=Lz1+de/2 and yj[k]<=Bz1+de/2: en_dom=True
        if Lz1-de/2<=xj[k]<=Lz1+Lv+de/2 and abs(yj[k]-yviga)<=Bv/2+de: en_dom=True
        if xj[k]>=Lz1+Lv-de/2 and yj[k]<=Bz2+de/2: en_dom=True
        if not en_dom:
            K[nd*k,nd*k]+=pen; K[nd*k+1,nd*k+1]+=pen; K[nd*k+2,nd*k+2]+=pen

# Loads
F=np.zeros(nt)
for j in range(nj1y):
    for i in range(nj1x):
        k=j*nj1x+i
        if abs(xj[k]-xC1)<=Lc1/2+1 and abs(yj[k]-yC1)<=Lc1/2+1:
            tx=de if 0<i<nx else de/2; ty=de if 0<j<ny else de/2
            F[nd*k]-=P1/(Lc1**2)*tx*ty
        if abs(xj[k]-xC2)<=Lc2/2+1 and abs(yj[k]-yC2)<=Lc2/2+1:
            tx=de if 0<i<nx else de/2; ty=de if 0<j<ny else de/2
            F[nd*k]-=P2/(Lc2**2)*tx*ty

# Solve
Z=np.linalg.solve(K,F)
w=Z[::3]

# Filter penalized nodes
w_plot=w.copy()
for j in range(nj1y):
    for i in range(nj1x):
        k=j*nj1x+i
        if abs(w[k])>50: w_plot[k]=np.nan

print(f"\nw at Col1 ({xC1},{yC1}): {w[int(yC1/de)*nj1x+int(xC1/de)]:.3f} mm")
print(f"w at Col2 ({xC2},{yC2}): {w[int(yC2/de)*nj1x+int(xC2/de)]:.3f} mm")
print(f"w_min: {np.nanmin(w_plot):.3f} mm")

# === PLOT ===
fig, axes = plt.subplots(2, 1, figsize=(14, 8))
fig.suptitle('Zapata Medianera + Viga + Zapata Centrada (MITC4)\n'
             f'P1={P1/9806.65:.0f}tf (medianera), P2={P2/9806.65:.0f}tf (centrada)',
             fontsize=13, fontweight='bold')

# Mask out-of-domain nodes
mask = np.isfinite(w_plot)
xg=np.linspace(0,Ltotal,200); yg=np.linspace(0,Bmax,80)
Xg,Yg=np.meshgrid(xg,yg)

# Plot 1: Deflection
ax=axes[0]; ax.set_aspect('equal')
Wg=griddata((xj[mask],yj[mask]),w_plot[mask],(Xg,Yg),method='cubic')
cf=ax.contourf(Xg,Yg,Wg,levels=20,cmap='jet')
plt.colorbar(cf,ax=ax,label='w (mm)',shrink=0.8)
ax.set_title('Deflexion w (mm)')

# Draw geometry
ax.add_patch(Rectangle((0,0),Lz1,Bz1,fill=False,ec='white',lw=2,ls='--'))
ax.add_patch(Rectangle((Lz1,yviga-Bv/2),Lv,Bv,fill=False,ec='yellow',lw=2,ls='--'))
ax.add_patch(Rectangle((Lz1+Lv,0),Lz2,Bz2,fill=False,ec='white',lw=2,ls='--'))
ax.plot(xC1,yC1,'ws',ms=10,markeredgecolor='red',markeredgewidth=2)
ax.plot(xC2,yC2,'ws',ms=10,markeredgecolor='red',markeredgewidth=2)
ax.text(xC1,yC1+200,'Col1\n(med)',ha='center',fontsize=8,color='white',fontweight='bold')
ax.text(xC2,yC2+200,'Col2\n(cen)',ha='center',fontsize=8,color='white',fontweight='bold')
ax.text(Lz1/2,-150,'Zapata 1',ha='center',fontsize=9,color='steelblue',fontweight='bold')
ax.text(Lz1+Lv/2,-150,'Viga',ha='center',fontsize=9,color='orange',fontweight='bold')
ax.text(Lz1+Lv+Lz2/2,-150,'Zapata 2',ha='center',fontsize=9,color='green',fontweight='bold')
ax.set_xlim(-100,Ltotal+100); ax.set_ylim(-300,Bmax+100)

# Plot 2: Cut along column axis
ax2=axes[1]
# Cut at Y = yC1 (same for both columns since yC1≈yC2≈center)
cut_j = int(round(yC1/de))
cut_nodes = [cut_j*nj1x+i for i in range(nj1x)]
cut_x = [xj[k] for k in cut_nodes]
cut_w = [w_plot[k] if abs(w[k])<50 else np.nan for k in cut_nodes]
ax2.plot(cut_x, cut_w, 'b-', linewidth=2)
ax2.fill_between(cut_x, cut_w, alpha=0.2, color='blue')
ax2.axvline(xC1, color='red', ls='--', lw=1, label=f'Col1 (x={xC1:.0f})')
ax2.axvline(xC2, color='red', ls='--', lw=1, label=f'Col2 (x={xC2:.0f})')
ax2.axvline(Lz1, color='orange', ls=':', lw=1)
ax2.axvline(Lz1+Lv, color='orange', ls=':', lw=1)
ax2.set_title(f'Corte longitudinal en Y={yC1:.0f}mm (eje columnas)')
ax2.set_xlabel('X (mm)'); ax2.set_ylabel('w (mm)')
ax2.legend(fontsize=8); ax2.grid(True, alpha=0.3)
ax2.set_xlim(0,Ltotal)

plt.tight_layout()
plt.savefig('zapata_medianera_viga.png', dpi=150, bbox_inches='tight')
print("\nSaved: zapata_medianera_viga.png")
