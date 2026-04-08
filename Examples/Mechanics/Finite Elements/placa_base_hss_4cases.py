"""Placa Base HSS - 4 casos: Pu, Mx, My, Pu+Mx+My
   DKQ Shell-Thin, contacto compresion-only, 10 pernos afuera"""
import numpy as np, math
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
from scipy.interpolate import griddata

E=200000.;nu=0.3;t=40.
Pu_tf=80.;Mx_tfm=20.;My_tfm=10.
kb=E*math.pi*(19.05/2)**2/400.;kw=50000/1e6
Lp=500.;Bp=400.;Lh=250.;Bh=150.

xa=np.array([0,50,125,187.5,250,312.5,375,450,500])
ya=np.array([0,50,125,200,275,350,400])
nx=9;ny=7;nj=nx*ny;ne=(nx-1)*(ny-1);nd=3;nt=nd*nj
xj=np.zeros(nj);yj=np.zeros(nj)
for j in range(ny):
    for i in range(nx):k=j*nx+i;xj[k]=xa[i];yj[k]=ya[j]
ej=np.zeros((ne,4),dtype=int)
for j in range(ny-1):
    for i in range(nx-1):e=j*(nx-1)+i;bl=j*nx+i;ej[e]=[bl,bl+1,bl+nx+1,bl+nx]

Db=E*t**3/(12*(1-nu**2))*np.array([[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]])
g=1/math.sqrt(3);gpts=[(-g,-g),(g,-g),(g,g),(-g,g)]

def build_B(xi,eta,dx,dy):
    B=np.zeros((3,12))
    B[0,0]=3*xi*(1-eta)/dx**2;B[0,2]=(1-eta)*(3*xi-1)/(2*dx)
    B[0,3]=-3*xi*(1-eta)/dx**2;B[0,5]=(1-eta)*(1+3*xi)/(2*dx)
    B[0,6]=-3*xi*(1+eta)/dx**2;B[0,8]=(1+eta)*(1+3*xi)/(2*dx)
    B[0,9]=3*xi*(1+eta)/dx**2;B[0,11]=(1+eta)*(3*xi-1)/(2*dx)
    B[1,0]=3*(1-xi)*eta/dy**2;B[1,1]=-(1-xi)*(3*eta-1)/(2*dy)
    B[1,3]=3*(1+xi)*eta/dy**2;B[1,4]=-(1+xi)*(3*eta-1)/(2*dy)
    B[1,6]=-3*(1+xi)*eta/dy**2;B[1,7]=-(1+xi)*(1+3*eta)/(2*dy)
    B[1,9]=-3*(1-xi)*eta/dy**2;B[1,10]=-(1-xi)*(1+3*eta)/(2*dy)
    TT=3*(2-xi**2-eta**2)/(2*dx*dy)
    B[2,0]=TT;B[2,1]=-(1-eta)*(1+3*eta)/(4*dx);B[2,2]=(1-xi)*(1+3*xi)/(4*dy)
    B[2,3]=-TT;B[2,4]=(1-eta)*(1+3*eta)/(4*dx);B[2,5]=(1+xi)*(1-3*xi)/(4*dy)
    B[2,6]=TT;B[2,7]=-(1+eta)*(3*eta-1)/(4*dx);B[2,8]=(1+xi)*(3*xi-1)/(4*dy)
    B[2,9]=-TT;B[2,10]=-(1+eta)*(1-3*eta)/(4*dx);B[2,11]=-(1-xi)*(1+3*xi)/(4*dy)
    return B

# Base stiffness
K_base=np.zeros((nt,nt))
for e in range(ne):
    ns=ej[e];dx=xj[ns[2]]-xj[ns[0]];dy=yj[ns[2]]-yj[ns[0]];dJ=dx*dy/4
    Ke=np.zeros((12,12))
    for xi,eta in gpts:Ke+=build_B(xi,eta,dx,dy).T@Db@build_B(xi,eta,dx,dy)*dJ
    ds_=[];
    for n in ns:ds_.extend([nd*n,nd*n+1,nd*n+2])
    for i in range(12):
        for j in range(12):K_base[ds_[i],ds_[j]]+=Ke[i,j]

trib=np.zeros(nj)
for j in range(ny):
    for i in range(nx):
        tx=(xa[min(i+1,nx-1)]-xa[max(i-1,0)])/2 if 0<i<nx-1 else (xa[1]-xa[0])/2 if i==0 else (xa[-1]-xa[-2])/2
        ty=(ya[min(j+1,ny-1)]-ya[max(j-1,0)])/2 if 0<j<ny-1 else (ya[1]-ya[0])/2 if j==0 else (ya[-1]-ya[-2])/2
        trib[j*nx+i]=tx*ty

xcH=Lp/2;ycH=Bp/2;xL=xcH-Lh/2;xR=xcH+Lh/2;yB=ycH-Bh/2;yT=ycH+Bh/2
bx_all=[50,175,325,450];by_all=[50,200,350]
bolt_nodes=[]
for bx in bx_all:
    for by in by_all:
        if xL+1<bx<xR-1 and yB+1<by<yT-1:continue
        d2=[(xj[k]-bx)**2+(yj[k]-by)**2 for k in range(nj)]
        bolt_nodes.append(np.argmin(d2))

# Profile nodes
prof=[];left=[];right=[];bot=[];top=[]
for k in range(nj):
    on=False
    if abs(xj[k]-xL)<2 and yB-1<=yj[k]<=yT+1:on=True;left.append(k)
    if abs(xj[k]-xR)<2 and yB-1<=yj[k]<=yT+1:on=True;right.append(k)
    if abs(yj[k]-yB)<2 and xL-1<=xj[k]<=xR+1:on=True;bot.append(k)
    if abs(yj[k]-yT)<2 and xL-1<=xj[k]<=xR+1:on=True;top.append(k)
    if on:prof.append(k)
prof=sorted(set(prof));left=sorted(set(left));right=sorted(set(right))
bot=sorted(set(bot));top=sorted(set(top))

def build_F(Pu_N, MxN, MyN):
    F=np.zeros(nt)
    if abs(Pu_N)>0:
        fp=-Pu_N/len(prof)
        for n in prof:F[nd*n]+=fp
    if abs(MxN)>0:
        for n in left:F[nd*n]-=MxN/Lh/len(left)
        for n in right:F[nd*n]+=MxN/Lh/len(right)
    if abs(MyN)>0:
        for n in bot:F[nd*n]-=MyN/Bh/len(bot)
        for n in top:F[nd*n]+=MyN/Bh/len(top)
    return F

def solve_case(F):
    active=np.ones(nj,dtype=bool)
    for it in range(10):
        K=K_base.copy()
        for k in range(nj):
            if active[k]:K[k*nd,k*nd]+=kw*trib[k]
        for bn in bolt_nodes:K[bn*nd,bn*nd]+=kb
        Z=np.linalg.solve(K,F);w=Z[::3]
        viol=[k for k in range(nj) if active[k] and w[k]>0]
        if not viol:break
        for k in viol:active[k]=False
    # Moments + VM
    Mx_n=np.zeros(nj);My_n=np.zeros(nj);Mxy_n=np.zeros(nj);cnt=np.zeros(nj)
    for e in range(ne):
        ns=ej[e];dx=xj[ns[2]]-xj[ns[0]];dy=yj[ns[2]]-yj[ns[0]]
        Ze=np.zeros(12)
        for ii,n in enumerate(ns):Ze[3*ii:3*ii+3]=Z[nd*n:nd*n+3]
        for xi,eta in gpts:
            M=Db@(build_B(xi,eta,dx,dy)@Ze)
            for n in ns:Mx_n[n]+=M[0];My_n[n]+=M[1];Mxy_n[n]+=M[2];cnt[n]+=1
    for n in range(nj):
        if cnt[n]>0:Mx_n[n]/=cnt[n];My_n[n]/=cnt[n];Mxy_n[n]/=cnt[n]
    sx=6*Mx_n/t**2;sy=6*My_n/t**2;txy=6*Mxy_n/t**2
    vm=np.sqrt(sx**2-sx*sy+sy**2+3*txy**2)
    return w, Mx_n, My_n, vm, active

# 4 cases
cases = [
    ("Pu=80tf solo", Pu_tf*9806.65, 0, 0),
    ("Mx=20tf*m solo", 0, Mx_tfm*9806650., 0),
    ("My=10tf*m solo", 0, 0, My_tfm*9806650.),
    ("Pu+Mx+My", Pu_tf*9806.65, Mx_tfm*9806650., My_tfm*9806650.),
]

results = []
for name, pu, mx, my in cases:
    F = build_F(pu, mx, my)
    w, Mx_n, My_n, vm, active = solve_case(F)
    nc = sum(active)
    ng = nj - nc
    print(f"{name}: w_min={w.min():.3f} w_max={w.max():.3f} VM={vm.max():.1f}MPa contacto={nc} gap={ng}")
    results.append((name, w, Mx_n, My_n, vm))

# PLOT: 4 rows (cases) x 4 cols (w, VM, Mx, My)
fig, axes = plt.subplots(4, 4, figsize=(18, 20))
fig.suptitle('Placa Base HSS 250x150x10 - DKQ\n4 casos de carga: Pu, Mx, My, Combinado', fontsize=14, fontweight='bold')

xg=np.linspace(0,Lp,80);yg=np.linspace(0,Bp,64);Xg,Yg=np.meshgrid(xg,yg)

for row, (name, w, Mx_n, My_n, vm) in enumerate(results):
    for col, (data, title, unit) in enumerate([
        (w, 'w', 'mm'),
        (vm, 'Von Mises', 'MPa'),
        (Mx_n/9806.65, 'Mx', 'tonf*m/m'),
        (My_n/9806.65, 'My', 'tonf*m/m'),
    ]):
        ax = axes[row, col]; ax.set_aspect('equal')
        Dg = griddata((xj,yj), data, (Xg,Yg), method='cubic')
        cf = ax.contourf(Xg, Yg, Dg, levels=15, cmap='jet')
        plt.colorbar(cf, ax=ax, shrink=0.7, format='%.2f')
        ax.plot([xL,xR,xR,xL,xL],[yB,yB,yT,yT,yB],'w-',lw=1.5)
        for bn in bolt_nodes:
            ax.plot(yj[bn] if False else xj[bn], yj[bn], 'ws', ms=2.5, markeredgecolor='k', markeredgewidth=0.3)
        if col == 0:
            ax.set_ylabel(name, fontsize=10, fontweight='bold')
            # Contact boundary
            cs = ax.contour(Xg, Yg, Dg, levels=[0], colors='white', linewidths=1.5, linestyles='--')
        if row == 0:
            ax.set_title(f'{title} ({unit})', fontsize=10)
        ax.set_xticks([]); ax.set_yticks([])

plt.tight_layout()
plt.savefig('placa_base_hss_4cases.png', dpi=150, bbox_inches='tight')
print("\nSaved: placa_base_hss_4cases.png")

# Summary table
print("\n" + "="*70)
print(f"{'Caso':<20} {'w_min':>8} {'w_max':>8} {'VM_max':>8} {'Mx_max':>10} {'My_max':>10}")
print("="*70)
for name, w, Mx_n, My_n, vm in results:
    print(f"{name:<20} {w.min():>8.3f} {w.max():>8.3f} {vm.max():>8.1f} {Mx_n.max()/9806.65:>10.3f} {My_n.max()/9806.65:>10.3f}")
print("="*70)
print("Unidades: w(mm), VM(MPa), Mx/My(tonf*m/m)")
