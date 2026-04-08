"""Validar zapata medianera + viga + zapata centrada vs SAP2000
   Usa pysap2000 wrapper. Datos sincronizados con Calcpad."""
import sys, os, time, ctypes
from ctypes import wintypes
sys.path.insert(0, r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Pipy Api sap2000 hekatan\src')
import pysap2000

# === Datos IGUALES al Calcpad (kN, m) ===
E = 25000000.0  # kN/m^2 (25000 MPa)
nu = 0.2
t_zap = 0.5; t_viga = 0.5  # m
ks = 20000.0  # kN/m^3

# Zapata 1 (medianera)
Lz1=2.0; Bz1=2.0; Lc1=0.4
xC1=0.2; yC1=1.0  # borde izq, centro Y

# Viga
Lv=3.0; Bv=0.4

# Zapata 2 (centrada)
Lz2=2.5; Bz2=2.0; Lc2=0.4
xC2=Lz1+Lv+Lz2/2; yC2=Bz2/2  # 6.25, 1.0

Ltotal=Lz1+Lv+Lz2  # 7.5
Bmax=max(Bz1,Bz2)   # 2.0
yviga=(yC1+yC2)/2    # 1.0

P1=80.*9.80665   # kN
P2=120.*9.80665  # kN
de=0.2  # m

print(f"Ltotal={Ltotal}m, Bmax={Bmax}m")
print(f"Col1:({xC1},{yC1}), Col2:({xC2},{yC2})")
print(f"yviga={yviga}")

# --- Connect ---
model = pysap2000.attach()
pysap2000.set_units('kN_m')
model.InitializeNewModel()
model.File.NewBlank()

# --- Material ---
model.PropMaterial.SetMaterial('CONC', 2)
model.PropMaterial.SetMPIsotropic('CONC', E, nu, 0)

# --- Sections: Shell-Thick ---
model.PropArea.SetShell_1('zapata', 2, False, 'CONC', 0, t_zap, t_zap)
model.PropArea.SetShell_1('viga', 2, False, 'CONC', 0, t_viga, t_viga)

# --- Nodes ---
nx=int(round(Ltotal/de)); ny=int(round(Bmax/de))
nj1x=nx+1; nj1y=ny+1
nodes={}
nid=1
for j in range(nj1y):
    for i in range(nj1x):
        name=str(nid)
        model.PointObj.AddCartesian(i*de, j*de, 0, '', name)
        nodes[(i,j)]=name; nid+=1
print(f"Nodes: {nid-1} ({nj1x}x{nj1y})")

# --- Elements (clasificar por zona) ---
eid=1
for j in range(ny):
    for i in range(nx):
        xce=(i+0.5)*de; yce=(j+0.5)*de
        in_z1 = xce < Lz1 and yce < Bz1
        in_viga = Lz1 <= xce <= Lz1+Lv and abs(yce-yviga) < Bv/2+de/2
        in_z2 = xce > Lz1+Lv and yce < Bz2
        if in_z1 or in_viga or in_z2:
            pts=[nodes[(i,j)],nodes[(i+1,j)],nodes[(i+1,j+1)],nodes[(i,j+1)]]
            sec='viga' if in_viga else 'zapata'
            model.AreaObj.AddByPoint(4, pts, f'E{eid}', sec)
            eid+=1
print(f"Elements: {eid-1}")

# --- Springs: SOLO en zapatas, NO en viga ---
nsp=0
for j in range(nj1y):
    for i in range(nj1x):
        x=i*de; y=j*de
        en_z1 = x <= Lz1 and y <= Bz1
        en_z2 = x >= Lz1+Lv and y <= Bz2
        if en_z1 or en_z2:
            tx=de if 0<i<nx else de/2
            ty=de if 0<j<ny else de/2
            kw=ks*tx*ty  # kN/m
            model.PointObj.SetSpring(nodes[(i,j)], [0,0,kw,0,0,0], 0, False, True)
            nsp+=1
print(f"Springs (solo zapatas): {nsp}")

# --- Restrain out-of-domain ---
nrest=0
for j in range(nj1y):
    for i in range(nj1x):
        x=i*de; y=j*de
        en_dom=False
        if x<=Lz1+de/2 and y<=Bz1+de/2: en_dom=True
        if Lz1-de/2<=x<=Lz1+Lv+de/2 and abs(y-yviga)<=Bv/2+de: en_dom=True
        if x>=Lz1+Lv-de/2 and y<=Bz2+de/2: en_dom=True
        if not en_dom:
            model.PointObj.SetRestraint(nodes[(i,j)], [True]*6)
            nrest+=1
print(f"Restrained: {nrest}")

# --- Loads ---
model.LoadPatterns.Add('CARGA', 1, 0, True)

nc1=0; nc2=0
for j in range(nj1y):
    for i in range(nj1x):
        x=i*de; y=j*de
        if abs(x-xC1)<=Lc1/2+0.01 and abs(y-yC1)<=Lc1/2+0.01:
            tx=de if 0<i<nx else de/2; ty=de if 0<j<ny else de/2
            fz=-P1/(Lc1**2)*tx*ty
            model.PointObj.SetLoadForce(nodes[(i,j)], 'CARGA', [0,0,fz,0,0,0])
            nc1+=1
        if abs(x-xC2)<=Lc2/2+0.01 and abs(y-yC2)<=Lc2/2+0.01:
            tx=de if 0<i<nx else de/2; ty=de if 0<j<ny else de/2
            fz=-P2/(Lc2**2)*tx*ty
            model.PointObj.SetLoadForce(nodes[(i,j)], 'CARGA', [0,0,fz,0,0,0])
            nc2+=1
print(f"Col1: {nc1} nodos, Col2: {nc2} nodos")

# --- Save and Run ---
save_dir = r'C:\CSiAPIexample'
os.makedirs(save_dir, exist_ok=True)
save_path = os.path.join(save_dir, 'zapata_medianera_viga_v2.sdb')
model.File.Save(save_path)
model.Analyze.SetRunCaseFlag('DEAD', False)
model.Analyze.SetRunCaseFlag('CARGA', True)
ret = model.Analyze.RunAnalysis()
print(f"Analysis ret={ret}")

# --- Auto-close Analysis Complete dialog ---
time.sleep(2)
EnumWindows = ctypes.windll.user32.EnumWindows
WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
GetWindowText = ctypes.windll.user32.GetWindowTextW
GetWindowTextLength = ctypes.windll.user32.GetWindowTextLengthW
GetClassName = ctypes.windll.user32.GetClassNameW
SendMessage = ctypes.windll.user32.SendMessageW
PostMessage = ctypes.windll.user32.PostMessageW
EnumChildWindows = ctypes.windll.user32.EnumChildWindows
CHILDPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
BM_CLICK = 0x00F5; WM_CLOSE = 0x0010

def close_sap_dialogs(hwnd, lparam):
    cls = ctypes.create_unicode_buffer(256)
    GetClassName(hwnd, cls, 256)
    if cls.value == '#32770':
        length = GetWindowTextLength(hwnd)
        if length > 0:
            buf = ctypes.create_unicode_buffer(length + 1)
            GetWindowText(hwnd, buf, length + 1)
            if any(k in buf.value.lower() for k in ['sap', 'analysis', 'complete']):
                def click(child, lp):
                    c = ctypes.create_unicode_buffer(256)
                    GetClassName(child, c, 256)
                    if 'Button' in c.value: SendMessage(child, BM_CLICK, 0, 0)
                    return True
                EnumChildWindows(hwnd, CHILDPROC(click), 0)
                PostMessage(hwnd, WM_CLOSE, 0, 0)
    return True

for _ in range(3):
    EnumWindows(WNDENUMPROC(close_sap_dialogs), 0)
    time.sleep(1)
# Close Analysis Complete windows
def close_ac(hwnd, lparam):
    length = GetWindowTextLength(hwnd)
    if length > 0:
        buf = ctypes.create_unicode_buffer(length + 1)
        GetWindowText(hwnd, buf, length + 1)
        if 'Analysis Complete' in buf.value:
            PostMessage(hwnd, WM_CLOSE, 0, 0)
    return True
EnumWindows(WNDENUMPROC(close_ac), 0)

# --- Read Results ---
model.Results.Setup.DeselectAllCasesAndCombosForOutput()
model.Results.Setup.SetCaseSelectedForOutput('CARGA')

NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]; OE=0

i1=int(round(xC1/de)); j1=int(round(yC1/de))
n1=nodes[(i1,j1)]
[NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret] = model.Results.JointDispl(n1,OE,NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3)
w1 = U3[0]*1000 if NR>0 else 0
print(f"\nSAP Col1 ({xC1},{yC1}): Uz = {w1:.4f} mm")

NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]
i2=int(round(xC2/de)); j2=int(round(yC2/de))
n2=nodes[(i2,j2)]
[NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret] = model.Results.JointDispl(n2,OE,NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3)
w2 = U3[0]*1000 if NR>0 else 0
print(f"SAP Col2 ({xC2},{yC2}): Uz = {w2:.4f} mm")

# Release COM
import gc
del model
gc.collect()

print(f"\n{'='*60}")
print(f"VALIDACION: Zapata Medianera + Viga + Zapata Centrada")
print(f"{'='*60}")
print(f"{'':>20} {'Calcpad':>10} {'SAP2000':>10} {'Ratio':>8}")
print(f"{'Col1 (med) mm':>20} {-30.99:>10.3f} {w1:>10.3f} {-30.99/w1 if w1!=0 else 0:>8.4f}")
print(f"{'Col2 (cen) mm':>20} {'TBD':>10} {w2:>10.3f} {'':>8}")
print(f"{'='*60}")
print("SAP2000 libre - sin dialogos bloqueantes")
