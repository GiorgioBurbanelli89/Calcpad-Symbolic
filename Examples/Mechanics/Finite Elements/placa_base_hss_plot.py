"""Placa Base HSS Tubular - DKQ Shell-Thin
   Verificacion Python. Contacto compresion-only, Mx + My."""
import numpy as np, math
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from scipy.interpolate import griddata

E=200000.; nu=0.3; t=40.
Pu_tf=80.; Mx_tfm=20.; My_tfm=10.
Pu=Pu_tf*9806.65; MxN=Mx_tfm*9806650.; MyN=My_tfm*9806650.
kb=E*math.pi*(19.05/2)**2/400.
kw=50000/1e6
Lp=500.; Bp=400.
Lh=250.; Bh=150.; th=10.

# Mesh 9x7
xa=np.array([0,50,125,187.5,250,312.5,375,450,500])
ya=np.array([0,50,125,200,275,350,400])
nx=9; ny=7; nj=nx*ny; ne=(nx-1)*(ny-1); nd=3; nt=nd*nj
xj=np.zeros(nj); yj=np.zeros(nj)
for j in range(ny):
    for i in range(nx): k=j*nx+i; xj[k]=xa[i]; yj[k]=ya[j]
ej=np.zeros((ne,4),dtype=int)
for j in range(ny-1):
    for i in range(nx-1): e=j*(nx-1)+i; bl=j*nx+i; ej[e]=[bl,bl+1,bl+nx+1,bl+nx]

# DKQ
Db=E*t**3/(12*(1-nu**2))*np.array([[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]])
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

K_base=np.zeros((nt,nt))
for e in range(ne):
    ns=ej[e]; dx=xj[ns[2]]-xj[ns[0]]; dy=yj[ns[2]]-yj[ns[0]]; dJ=dx*dy/4
    Ke=np.zeros((12,12))
    for xi,eta in gpts: Ke+=build_B(xi,eta,dx,dy).T@Db@build_B(xi,eta,dx,dy)*dJ
    ds_=[]
    for n in ns: ds_.extend([nd*n,nd*n+1,nd*n+2])
    for i in range(12):
        for j in range(12): K_base[ds_[i],ds_[j]]+=Ke[i,j]

# Tributary areas
trib=np.zeros(nj)
for j in range(ny):
    for i in range(nx):
        tx = (xa[min(i+1,nx-1)]-xa[max(i-1,0)])/2 if 0<i<nx-1 else (xa[1]-xa[0])/2 if i==0 else (xa[-1]-xa[-2])/2
        ty = (ya[min(j+1,ny-1)]-ya[max(j-1,0)])/2 if 0<j<ny-1 else (ya[1]-ya[0])/2 if j==0 else (ya[-1]-ya[-2])/2
        trib[j*nx+i]=tx*ty

# Bolts 4x3=12
bolt_x=[50,150,350,450]; bolt_y=[50,200,350]
bolt_nodes=[]
for bx in bolt_x:
    for by in bolt_y:
        d2=[(xj[k]-bx)**2+(yj[k]-by)**2 for k in range(nj)]
        bolt_nodes.append(np.argmin(d2))

# HSS profile nodes
xcH=Lp/2; ycH=Bp/2
xL=xcH-Lh/2; xR=xcH+Lh/2; yB=ycH-Bh/2; yT=ycH+Bh/2
prof_nodes=[]; left_nodes=[]; right_nodes=[]; bot_nodes=[]; top_nodes=[]
for k in range(nj):
    on=False
    if abs(xj[k]-xL)<2 and yB-1<=yj[k]<=yT+1: on=True; left_nodes.append(k)
    if abs(xj[k]-xR)<2 and yB-1<=yj[k]<=yT+1: on=True; right_nodes.append(k)
    if abs(yj[k]-yB)<2 and xL-1<=xj[k]<=xR+1: on=True; bot_nodes.append(k)
    if abs(yj[k]-yT)<2 and xL-1<=xj[k]<=xR+1: on=True; top_nodes.append(k)
    if on: prof_nodes.append(k)
prof_nodes=sorted(set(prof_nodes))
left_nodes=sorted(set(left_nodes)); right_nodes=sorted(set(right_nodes))
bot_nodes=sorted(set(bot_nodes)); top_nodes=sorted(set(top_nodes))

# Loads
F=np.zeros(nt)
fp=-Pu/len(prof_nodes)
for n in prof_nodes: F[nd*n]+=fp
# Mx: left down, right up
fmxL=-MxN/Lh/len(left_nodes); fmxR=MxN/Lh/len(right_nodes)
for n in left_nodes: F[nd*n]+=fmxL
for n in right_nodes: F[nd*n]+=fmxR
# My: bottom down, top up
fmyB=-MyN/Bh/len(bot_nodes); fmyT=MyN/Bh/len(top_nodes)
for n in bot_nodes: F[nd*n]+=fmyB
for n in top_nodes: F[nd*n]+=fmyT

# Iterative compression-only
active=np.ones(nj,dtype=bool)
for it in range(10):
    K=K_base.copy()
    for k in range(nj):
        if active[k]: K[k*nd,k*nd]+=kw*trib[k]
    for bn in bolt_nodes: K[bn*nd,bn*nd]+=kb
    Z=np.linalg.solve(K,F); w=Z[::3]
    violators=[k for k in range(nj) if active[k] and w[k]>0]
    print(f"Iter {it+1}: {sum(active)} activos, {len(violators)} levantados")
    if not violators: break
    for k in violators: active[k]=False

# Results
kc=(ny//2)*nx+nx//2
print(f"\nw_centro = {w[kc]:.4f} mm")
print(f"w_min = {w.min():.4f} mm")
print(f"w_max = {w.max():.4f} mm")

print(f"\n{'#':>3} {'X':>6} {'Y':>6} {'w(mm)':>8} {'R(tonf)':>9}")
Rb_total=0
for idx,bn in enumerate(bolt_nodes):
    R=kb*w[bn]/9806.65; Rb_total+=kb*w[bn]
    print(f"{idx+1:>3} {xj[bn]:>6.0f} {yj[bn]:>6.0f} {w[bn]:>8.3f} {R:>9.3f}")
Rw=sum(kw*trib[k]*w[k] for k in range(nj) if active[k])
print(f"\nR_pernos = {Rb_total/9806.65:.3f} tonf")
print(f"R_winkler = {Rw/9806.65:.3f} tonf")
print(f"R_total = {(Rb_total+Rw)/9806.65:.3f} tonf (Pu={Pu_tf})")

# Von Mises
Mx_n=np.zeros(nj); My_n=np.zeros(nj); Mxy_n=np.zeros(nj); cnt=np.zeros(nj)
for e in range(ne):
    ns=ej[e]; dx=xj[ns[2]]-xj[ns[0]]; dy=yj[ns[2]]-yj[ns[0]]
    Ze=np.zeros(12)
    for ii,n in enumerate(ns): Ze[3*ii:3*ii+3]=Z[nd*n:nd*n+3]
    for xi,eta in gpts:
        M=Db@(build_B(xi,eta,dx,dy)@Ze)
        for n in ns: Mx_n[n]+=M[0]; My_n[n]+=M[1]; Mxy_n[n]+=M[2]; cnt[n]+=1
for n in range(nj):
    if cnt[n]>0: Mx_n[n]/=cnt[n]; My_n[n]/=cnt[n]; Mxy_n[n]/=cnt[n]
sx=6*Mx_n/t**2; sy=6*My_n/t**2; txy=6*Mxy_n/t**2
vm=np.sqrt(sx**2-sx*sy+sy**2+3*txy**2)
print(f"\nVM max = {vm.max():.1f} MPa ({vm.max()*10.197:.0f} kgf/cm2)")

# Plot
fig,axes=plt.subplots(2,2,figsize=(12,10))
fig.suptitle(f'Placa Base HSS 250x150x10 - DKQ\nPu={Pu_tf}tonf, Mx={Mx_tfm}tonf*m, My={My_tfm}tonf*m',fontsize=12,fontweight='bold')
xg=np.linspace(0,Lp,80); yg=np.linspace(0,Bp,64); Xg,Yg=np.meshgrid(xg,yg)

for idx,(data,title,cmap) in enumerate([(w,'Deflexion w (mm)','jet'),
    (vm,'Von Mises (MPa)','jet'),(Mx_n/9806.65,'Mx (tonf*m/m)','jet'),
    (My_n/9806.65,'My (tonf*m/m)','jet')]):
    ax=axes[idx//2,idx%2]; ax.set_aspect('equal')
    Dg=griddata((xj,yj),data,(Xg,Yg),method='cubic')
    cf=ax.contourf(Xg,Yg,Dg,levels=20,cmap=cmap)
    plt.colorbar(cf,ax=ax,shrink=0.8)
    ax.set_title(title); ax.plot([xL,xR,xR,xL,xL],[yB,yB,yT,yT,yB],'w-',lw=2)
    ax.plot([xL+th,xR-th,xR-th,xL+th,xL+th],[yB+th,yB+th,yT-th,yT-th,yB+th],'w--',lw=1)
    for bx in bolt_x:
        for by in bolt_y: ax.plot(bx,by,'ws',ms=4,markeredgecolor='k',markeredgewidth=0.5)

plt.tight_layout()
plt.savefig('placa_base_hss_results.png',dpi=150,bbox_inches='tight')
print("\nSaved: placa_base_hss_results.png")
