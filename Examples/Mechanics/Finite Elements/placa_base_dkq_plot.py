"""Placa Base 600x500 - DKQ Shell-Thin
   Contacto compresion-only (iterativo), Von Mises, reacciones pernos tonf
   Orientacion SAP2000: SAP X=500 horiz, SAP Y=600 vert"""
import numpy as np
import math
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.colors import Normalize
from scipy.interpolate import griddata

# ========== 1. DATOS ==========
E = 200000.0; nu = 0.3; t = 50.0
Pu_tf = 148.0; Mx_tfm = 50.0
Pu = Pu_tf * 9806.65; Mu = Mx_tfm * 9806650.0
kb = E * math.pi * (25.4/2)**2 / 500.0
kw = 50000 / 1e6  # N/mm^3

# ========== 2. MALLA 9x9 (Calcpad: X=600, Y=500) ==========
xa = np.array([0,50,112,174,300,426,488,550,600], dtype=float)
ya = np.array([0,50,100,175,250,325,400,450,500], dtype=float)
nj=81; ne=64; nd=3; nt=nd*nj
xj=np.zeros(nj); yj=np.zeros(nj)
for j in range(9):
    for i in range(9):
        k=j*9+i; xj[k]=xa[i]; yj[k]=ya[j]
ej=np.zeros((ne,4),dtype=int)
for j in range(8):
    for i in range(8):
        e=j*8+i; bl=j*9+i; ej[e]=[bl,bl+1,bl+10,bl+9]

# ========== 3. DKQ Element Stiffness ==========
Db = E*t**3/(12*(1-nu**2))*np.array([[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]])
g=1/math.sqrt(3); gpts=[(-g,-g),(g,-g),(g,g),(-g,g)]

def build_B(xi, eta, dx, dy):
    B=np.zeros((3,12))
    B[0,0]=3*xi*(1-eta)/dx**2; B[0,2]=(1-eta)*(3*xi-1)/(2*dx)
    B[0,3]=-3*xi*(1-eta)/dx**2; B[0,5]=(1-eta)*(1+3*xi)/(2*dx)
    B[0,6]=-3*xi*(1+eta)/dx**2; B[0,8]=(1+eta)*(1+3*xi)/(2*dx)
    B[0,9]=3*xi*(1+eta)/dx**2; B[0,11]=(1+eta)*(3*xi-1)/(2*dx)
    B[1,0]=3*(1-xi)*eta/dy**2; B[1,1]=-(1-xi)*(3*eta-1)/(2*dy)
    B[1,3]=3*(1+xi)*eta/dy**2; B[1,4]=-(1+xi)*(3*eta-1)/(2*dy)
    B[1,6]=-3*(1+xi)*eta/dy**2; B[1,7]=-(1+xi)*(1+3*eta)/(2*dy)
    B[1,9]=-3*(1-xi)*eta/dy**2; B[1,10]=-(1-xi)*(1+3*eta)/(2*dy)
    TT=3*(2-xi**2-eta**2)/(2*dx*dy)
    B[2,0]=TT; B[2,1]=-(1-eta)*(1+3*eta)/(4*dx); B[2,2]=(1-xi)*(1+3*xi)/(4*dy)
    B[2,3]=-TT; B[2,4]=(1-eta)*(1+3*eta)/(4*dx); B[2,5]=(1+xi)*(1-3*xi)/(4*dy)
    B[2,6]=TT; B[2,7]=-(1+eta)*(3*eta-1)/(4*dx); B[2,8]=(1+xi)*(3*xi-1)/(4*dy)
    B[2,9]=-TT; B[2,10]=-(1+eta)*(1-3*eta)/(4*dx); B[2,11]=-(1-xi)*(1+3*xi)/(4*dy)
    return B

# Base stiffness (DKQ only, without springs)
K_base = np.zeros((nt,nt))
for e in range(ne):
    ns=ej[e]; dx=xj[ns[2]]-xj[ns[0]]; dy=yj[ns[2]]-yj[ns[0]]; dJ=dx*dy/4
    Ke=np.zeros((12,12))
    for xi,eta in gpts:
        B = build_B(xi, eta, dx, dy)
        Ke += B.T @ Db @ B * dJ
    ds_=[]
    for n in ns: ds_.extend([nd*n, nd*n+1, nd*n+2])
    for i in range(12):
        for j in range(12):
            K_base[ds_[i],ds_[j]] += Ke[i,j]

# ========== 4. TRIBUTARY AREAS for Winkler ==========
trib = np.zeros(nj)
for j in range(9):
    for i in range(9):
        if i==0: tx=(xa[1]-xa[0])/2
        elif i==8: tx=(xa[8]-xa[7])/2
        else: tx=(xa[i+1]-xa[i-1])/2
        if j==0: ty=(ya[1]-ya[0])/2
        elif j==8: ty=(ya[8]-ya[7])/2
        else: ty=(ya[j+1]-ya[j-1])/2
        trib[j*9+i] = tx*ty

# ========== 5. BOLTS ==========
bolt_xy = [(50,50),(50,175),(50,325),(50,450),
           (174,50),(174,175),(174,325),(174,450),
           (426,50),(426,175),(426,325),(426,450),
           (550,50),(550,175),(550,325),(550,450)]
bolt_nodes=[]
for bx,by in bolt_xy:
    d2=[(xj[k]-bx)**2+(yj[k]-by)**2 for k in range(nj)]
    bolt_nodes.append(np.argmin(d2))

# ========== 6. LOADS ==========
F=np.zeros(nt)
alma=[4*9+2,4*9+3,4*9+4,4*9+5,4*9+6]
ala_izq=[2*9+2,3*9+2,4*9+2,5*9+2,6*9+2]  # Calcpad X=112 = SAP Y=112
ala_der=[2*9+6,3*9+6,4*9+6,5*9+6,6*9+6]  # Calcpad X=488 = SAP Y=488
prof=sorted(set(alma+ala_izq+ala_der))  # 13 nodes
for n in prof: F[n*nd] -= Pu/len(prof)
brazo = 488 - 112  # 376mm
Fm = Mu/brazo; fm = Fm/5
for n in ala_izq: F[n*nd] -= fm
for n in ala_der: F[n*nd] += fm

# ========== 7. ITERATIVE SOLVE: Compression-only Winkler ==========
print("="*65)
print("CONTACTO COMPRESION-ONLY (iterativo)")
print("="*65)

active_winkler = np.ones(nj, dtype=bool)  # all active initially
max_iter = 20
for it in range(max_iter):
    # Assemble K = K_base + Winkler(active) + Bolts
    K = K_base.copy()
    for k in range(nj):
        if active_winkler[k]:
            K[k*nd, k*nd] += kw * trib[k]
    for bn in bolt_nodes:
        K[bn*nd, bn*nd] += kb

    # Solve
    Z = np.linalg.solve(K, F)
    w = Z[::3]

    # Check: any active Winkler node with w > 0 (lifting)?
    violators = []
    for k in range(nj):
        if active_winkler[k] and w[k] > 0:
            violators.append(k)

    n_active = np.sum(active_winkler)
    n_lifted = len(violators)
    print(f"  Iter {it+1}: {n_active} Winkler activos, {n_lifted} levantados")

    if n_lifted == 0:
        print(f"  ** Convergencia en {it+1} iteraciones **")
        break

    # Deactivate violators
    for k in violators:
        active_winkler[k] = False
else:
    print(f"  !! No convergio en {max_iter} iteraciones !!")

n_contact = np.sum(active_winkler)
n_gap = nj - n_contact
print(f"\nResultado: {n_contact} nodos en contacto, {n_gap} nodos con gap (separados)")
print(f"Nodos con gap:")
for k in range(nj):
    if not active_winkler[k]:
        print(f"  Nodo {k} ({xj[k]:.0f},{yj[k]:.0f}) w={w[k]:.4f} mm")

# ========== 8. RESULTS ==========
print(f"\n{'='*65}")
print(f"RESULTADOS FINALES")
print(f"{'='*65}")
print(f"w_center = {w[4*9+4]:.4f} mm")
print(f"w_min = {w.min():.4f} mm  (max hundimiento)")
print(f"w_max = {w.max():.4f} mm  (max levantamiento)")

# Bolt reactions
print(f"\n{'#':>3} {'SAP_X':>6} {'SAP_Y':>6} {'w(mm)':>8} {'R(tonf)':>9} {'Tipo':>12}")
print("-"*50)
R_bolts_total = 0
for idx,(bx,by) in enumerate(bolt_xy):
    bn = bolt_nodes[idx]
    R_N = kb * w[bn]
    R_tf = R_N / 9806.65
    R_bolts_total += R_N
    sap_x_b, sap_y_b = by, bx  # swap for SAP
    typ = "COMPRESION" if R_tf < 0 else "TRACCION"
    print(f"{idx+1:>3} {sap_x_b:>6.0f} {sap_y_b:>6.0f} {w[bn]:>8.3f} {R_tf:>9.3f} {typ:>12}")

# Winkler reaction
R_winkler = 0
for k in range(nj):
    if active_winkler[k]:
        R_winkler += kw * trib[k] * w[k]

print(f"\nR_pernos   = {R_bolts_total/9806.65:.3f} tonf")
print(f"R_winkler  = {R_winkler/9806.65:.3f} tonf (solo nodos en contacto)")
print(f"R_total    = {(R_bolts_total+R_winkler)/9806.65:.3f} tonf (Pu={Pu_tf:.0f})")

# ========== 9. MOMENTS + VON MISES ==========
Mx_n=np.zeros(nj); My_n=np.zeros(nj); Mxy_n=np.zeros(nj); cnt=np.zeros(nj)
for e in range(ne):
    ns=ej[e]; dx=xj[ns[2]]-xj[ns[0]]; dy=yj[ns[2]]-yj[ns[0]]
    Ze=np.zeros(12)
    for ii,n in enumerate(ns): Ze[3*ii:3*ii+3]=Z[nd*n:nd*n+3]
    for xi,eta in gpts:
        B = build_B(xi, eta, dx, dy)
        M = Db @ (B @ Ze)
        for n in ns:
            Mx_n[n]+=M[0]; My_n[n]+=M[1]; Mxy_n[n]+=M[2]; cnt[n]+=1
for n in range(nj):
    if cnt[n]>0:
        Mx_n[n]/=cnt[n]; My_n[n]/=cnt[n]; Mxy_n[n]/=cnt[n]

# Bending stresses at surface: sigma = 6*M/t^2
sx = 6*Mx_n/t**2
sy = 6*My_n/t**2
txy = 6*Mxy_n/t**2
vm = np.sqrt(sx**2 - sx*sy + sy**2 + 3*txy**2)

print(f"\nVon Mises max = {vm.max():.1f} MPa en nodo {np.argmax(vm)}")
print(f"  Calcpad ({xj[np.argmax(vm)]:.0f},{yj[np.argmax(vm)]:.0f})")
print(f"  SAP     ({yj[np.argmax(vm)]:.0f},{xj[np.argmax(vm)]:.0f})")
print(f"  Fy(A36) = 250 MPa -> ratio = {vm.max()/250:.3f}")

# ========== 10. PLOTS (SAP2000 orientation) ==========
sap_x = yj; sap_y = xj  # Calcpad Y->SAP X, Calcpad X->SAP Y
xg = np.linspace(0, 500, 100); yg = np.linspace(0, 600, 120)
Xg, Yg = np.meshgrid(xg, yg)

fig, axes = plt.subplots(2, 2, figsize=(14, 16))
fig.suptitle(f'Placa Base 500x600 (SAP2000) - DKQ Shell-Thin\n'
             f'Pu={Pu_tf:.0f} tonf, Mx={Mx_tfm:.0f} tonf*m | '
             f'Contacto compresion-only: {n_contact} nodos activos, {n_gap} gap',
             fontsize=12, fontweight='bold')

def draw_profile_sap(ax, color='white', lw_alma=2, lw_ala=3):
    ax.plot([250,250],[112,488], color=color, linewidth=lw_alma)
    ax.plot([100,400],[112,112], color=color, linewidth=lw_ala)
    ax.plot([100,400],[488,488], color=color, linewidth=lw_ala)

def draw_bolts_sap(ax, show_R=False):
    bsx_list = [50,175,325,450]; bsy_list = [50,174,426,550]
    idx = 0
    for bsy in bsy_list:
        for bsx in bsx_list:
            bn = bolt_nodes[idx]
            R_tf = kb*w[bn]/9806.65
            if show_R:
                color = 'blue' if R_tf < 0 else 'red'
                sz = min(abs(R_tf)*0.35 + 4, 14)
                ax.plot(bsx, bsy, 's', color=color, ms=sz, markeredgecolor='black',
                        markeredgewidth=0.8, zorder=10, alpha=0.8)
                ax.annotate(f'{R_tf:.1f}', (bsx, bsy), textcoords="offset points",
                            xytext=(0, -sz-3), ha='center', fontsize=6,
                            fontweight='bold', color=color)
            else:
                ax.plot(bsx, bsy, 'ws', ms=5, markeredgecolor='k',
                        markeredgewidth=0.8, zorder=10)
            idx += 1

def draw_contact_sap(ax):
    """Draw contact/gap status"""
    for k in range(nj):
        sx, sy = yj[k], xj[k]  # SAP coords
        if active_winkler[k]:
            ax.plot(sx, sy, '^', color='lime', ms=3, alpha=0.5, zorder=3)
        else:
            ax.plot(sx, sy, 'x', color='red', ms=4, alpha=0.7, zorder=3)

# --- 1. Deflection ---
ax = axes[0,0]; ax.set_title('Deflexion w (mm)', fontsize=11); ax.set_aspect('equal')
Wg = griddata((sap_x, sap_y), w, (Xg, Yg), method='cubic')
cf = ax.contourf(Xg, Yg, Wg, levels=20, cmap='jet')
ax.contour(Xg, Yg, Wg, levels=20, colors='k', linewidths=0.3, alpha=0.3)
# Zero contour (contact boundary)
cs0 = ax.contour(Xg, Yg, Wg, levels=[0], colors='white', linewidths=2, linestyles='--')
ax.clabel(cs0, fmt='w=0', fontsize=8, colors='white')
plt.colorbar(cf, ax=ax, label='w (mm)', shrink=0.8)
draw_profile_sap(ax); draw_bolts_sap(ax); draw_contact_sap(ax)
ax.set_xlabel('SAP X (mm)'); ax.set_ylabel('SAP Y (mm)')
ax.set_xlim(0,500); ax.set_ylim(0,600)

# --- 2. Von Mises ---
ax = axes[0,1]; ax.set_aspect('equal')
ax.set_title(f'Von Mises (MPa) - max={vm.max():.1f}\nFy(A36)=250 MPa, ratio={vm.max()/250:.2f}', fontsize=11)
VMg = griddata((sap_x, sap_y), vm, (Xg, Yg), method='cubic')
cf2 = ax.contourf(Xg, Yg, VMg, levels=20, cmap='jet')
ax.contour(Xg, Yg, VMg, levels=20, colors='k', linewidths=0.3, alpha=0.3)
plt.colorbar(cf2, ax=ax, label='Von Mises (MPa)', shrink=0.8)
draw_profile_sap(ax); draw_bolts_sap(ax)
ax.set_xlabel('SAP X (mm)'); ax.set_ylabel('SAP Y (mm)')
ax.set_xlim(0,500); ax.set_ylim(0,600)

# --- 3. Contact status + Bolt reactions ---
ax = axes[1,0]; ax.set_aspect('equal')
ax.set_title(f'Contacto + Reacciones Pernos (tonf)\n'
             f'Verde=contacto ({n_contact}), Rojo X=gap ({n_gap})', fontsize=10)
cf3 = ax.contourf(Xg, Yg, Wg, levels=20, cmap='jet', alpha=0.2)
draw_profile_sap(ax, color='darkred')
# Mesh
for e in range(ne):
    ns=ej[e]; c=[(yj[ns[k]],xj[ns[k]]) for k in [0,1,2,3,0]]
    ax.plot(*zip(*c),'g-',linewidth=0.3,alpha=0.3)
draw_contact_sap(ax)
draw_bolts_sap(ax, show_R=True)
# Contact boundary
cs1 = ax.contour(Xg, Yg, Wg, levels=[0], colors='black', linewidths=2, linestyles='--')
ax.clabel(cs1, fmt='w=0\n(borde contacto)', fontsize=7)
ax.plot([],[], '^', color='lime', ms=6, label=f'Contacto ({n_contact})')
ax.plot([],[], 'x', color='red', ms=6, label=f'Gap ({n_gap})')
ax.plot([],[], 's', color='blue', ms=8, label='Perno compresion')
ax.plot([],[], 's', color='red', ms=8, label='Perno traccion')
ax.legend(fontsize=7, loc='upper right')
ax.set_xlabel('SAP X (mm)'); ax.set_ylabel('SAP Y (mm)')
ax.set_xlim(-30,530); ax.set_ylim(-30,630)

# --- 4. Bearing pressure (Winkler) ---
ax = axes[1,1]; ax.set_aspect('equal')
# Bearing pressure = kw * |w| where w < 0 and contact active
bearing = np.zeros(nj)
for k in range(nj):
    if active_winkler[k] and w[k] < 0:
        bearing[k] = -kw * w[k]  # positive pressure (MPa)
Bg = griddata((sap_x, sap_y), bearing, (Xg, Yg), method='cubic')
Bg = np.clip(Bg, 0, None)
ax.set_title(f'Presion de apoyo (MPa)\nmax={bearing.max():.4f} MPa = {bearing.max()*1000:.2f} kPa', fontsize=11)
cf4 = ax.contourf(Xg, Yg, Bg, levels=20, cmap='YlOrRd')
plt.colorbar(cf4, ax=ax, label='Bearing pressure (MPa)', shrink=0.8)
draw_profile_sap(ax, color='black')
draw_contact_sap(ax)
# Contact boundary
cs2 = ax.contour(Xg, Yg, Wg, levels=[0], colors='black', linewidths=2, linestyles='--')
ax.set_xlabel('SAP X (mm)'); ax.set_ylabel('SAP Y (mm)')
ax.set_xlim(0,500); ax.set_ylim(0,600)

plt.tight_layout()
plt.savefig('placa_base_dkq_results.png', dpi=150, bbox_inches='tight')
print("\nSaved: placa_base_dkq_results.png")
