"""Dos Zapatas Independientes en SAP2000 — sin viga de amarre
   Zapata 1: 2x2m, Col (250,1000), P1=80tf
   Zapata 2: 2.5x2m, Col centro, P2=120tf"""
import sys, os, time, ctypes
from ctypes import wintypes
sys.path.insert(0, r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Pipy Api sap2000 hekatan\src')
import pysap2000

E=25000000.; nu=0.2; t=0.5  # kN, m
ks=20000.  # kN/m^3
de=0.25  # m

# Zapata 1
Lz1=2.; Bz1=2.; xC1=0.25; yC1=1.0; P1=80.*9.80665; Lc1=0.4
# Zapata 2 (offset 5m en X)
Lz2=2.5; Bz2=2.; xC2=5.+Lz2/2; yC2=Bz2/2; P2=120.*9.80665; Lc2=0.4
xoff2=5.0  # offset X de zapata 2

model = pysap2000.attach()
pysap2000.set_units('kN_m')
model.InitializeNewModel()
model.File.NewBlank()

model.PropMaterial.SetMaterial('CONC', 2)
model.PropMaterial.SetMPIsotropic('CONC', E, nu, 0)
model.PropArea.SetShell_1('zapata', 2, False, 'CONC', 0, t, t)

# === Zapata 1 ===
nx1=int(Lz1/de); ny1=int(Bz1/de)
nodes1={}; nid=1
for j in range(ny1+1):
    for i in range(nx1+1):
        name=str(nid); model.PointObj.AddCartesian(i*de, j*de, 0, '', name)
        nodes1[(i,j)]=name; nid+=1
print(f"Zap1 nodes: {nid-1}")

eid=1
for j in range(ny1):
    for i in range(nx1):
        pts=[nodes1[(i,j)],nodes1[(i+1,j)],nodes1[(i+1,j+1)],nodes1[(i,j+1)]]
        model.AreaObj.AddByPoint(4, pts, f'E{eid}', 'zapata'); eid+=1
print(f"Zap1 elements: {eid-1}")

# Springs zap1
for j in range(ny1+1):
    for i in range(nx1+1):
        tx=de if 0<i<nx1 else de/2; ty=de if 0<j<ny1 else de/2
        model.PointObj.SetSpring(nodes1[(i,j)], [0,0,ks*tx*ty,0,0,0], 0, False, True)

# === Zapata 2 ===
nx2=int(Lz2/de); ny2=int(Bz2/de)
nodes2={}
for j in range(ny2+1):
    for i in range(nx2+1):
        name=str(nid); model.PointObj.AddCartesian(xoff2+i*de, j*de, 0, '', name)
        nodes2[(i,j)]=name; nid+=1
print(f"Zap2 nodes: {nid-1-81}")

for j in range(ny2):
    for i in range(nx2):
        pts=[nodes2[(i,j)],nodes2[(i+1,j)],nodes2[(i+1,j+1)],nodes2[(i,j+1)]]
        model.AreaObj.AddByPoint(4, pts, f'E{eid}', 'zapata'); eid+=1
print(f"Zap2 elements: {eid-1-64}")

# Springs zap2
for j in range(ny2+1):
    for i in range(nx2+1):
        tx=de if 0<i<nx2 else de/2; ty=de if 0<j<ny2 else de/2
        model.PointObj.SetSpring(nodes2[(i,j)], [0,0,ks*tx*ty,0,0,0], 0, False, True)

# === Loads ===
model.LoadPatterns.Add('CARGA', 1, 0, True)

# Col1 - distribute like Python: tolerance = Lc/2 + de/2
nc1=0
for j in range(ny1+1):
    for i in range(nx1+1):
        x=i*de; y=j*de
        if abs(x-xC1)<=Lc1/2+de/2 and abs(y-yC1)<=Lc1/2+de/2:
            nc1+=1
for j in range(ny1+1):
    for i in range(nx1+1):
        x=i*de; y=j*de
        if abs(x-xC1)<=Lc1/2+de/2 and abs(y-yC1)<=Lc1/2+de/2:
            tx=de if 0<i<nx1 else de/2; ty=de if 0<j<ny1 else de/2
            model.PointObj.SetLoadForce(nodes1[(i,j)], 'CARGA', [0,0,-P1/(Lc1**2)*tx*ty,0,0,0])
print(f"Col1: {nc1} nodos cargados")

# Col2
nc2=0
for j in range(ny2+1):
    for i in range(nx2+1):
        x=xoff2+i*de; y=j*de
        if abs(x-xC2)<=Lc2/2+de/2 and abs(y-yC2)<=Lc2/2+de/2:
            nc2+=1
for j in range(ny2+1):
    for i in range(nx2+1):
        x=xoff2+i*de; y=j*de
        if abs(x-xC2)<=Lc2/2+de/2 and abs(y-yC2)<=Lc2/2+de/2:
            tx=de if 0<i<nx2 else de/2; ty=de if 0<j<ny2 else de/2
            model.PointObj.SetLoadForce(nodes2[(i,j)], 'CARGA', [0,0,-P2/(Lc2**2)*tx*ty,0,0,0])
print(f"Col2: {nc2} nodos cargados")

# === Save + Run ===
save_dir=r'C:\CSiAPIexample'; os.makedirs(save_dir, exist_ok=True)
model.File.Save(os.path.join(save_dir, 'dos_zapatas_sin_viga.sdb'))
model.Analyze.SetRunCaseFlag('DEAD', False)
model.Analyze.SetRunCaseFlag('CARGA', True)
ret=model.Analyze.RunAnalysis()
print(f"Analysis ret={ret}")

# Auto-close dialogs
time.sleep(2)
EnumWindows=ctypes.windll.user32.EnumWindows
WNDENUMPROC=ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
GetWindowText=ctypes.windll.user32.GetWindowTextW
GetWindowTextLength=ctypes.windll.user32.GetWindowTextLengthW
GetClassName=ctypes.windll.user32.GetClassNameW
SendMessage=ctypes.windll.user32.SendMessageW
PostMessage=ctypes.windll.user32.PostMessageW
EnumChildWindows=ctypes.windll.user32.EnumChildWindows
CHILDPROC=ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
BM_CLICK=0x00F5; WM_CLOSE=0x0010

def close_sap_dialogs(hwnd, lparam):
    cls=ctypes.create_unicode_buffer(256); GetClassName(hwnd, cls, 256)
    if cls.value=='#32770':
        length=GetWindowTextLength(hwnd)
        if length>0:
            buf=ctypes.create_unicode_buffer(length+1); GetWindowText(hwnd, buf, length+1)
            if any(k in buf.value.lower() for k in ['sap','analysis','complete']):
                def click(child, lp):
                    c=ctypes.create_unicode_buffer(256); GetClassName(child, c, 256)
                    if 'Button' in c.value: SendMessage(child, BM_CLICK, 0, 0)
                    return True
                EnumChildWindows(hwnd, CHILDPROC(click), 0)
                PostMessage(hwnd, WM_CLOSE, 0, 0)
    return True

for _ in range(3):
    EnumWindows(WNDENUMPROC(close_sap_dialogs), 0); time.sleep(1)
def close_ac(hwnd, lparam):
    length=GetWindowTextLength(hwnd)
    if length>0:
        buf=ctypes.create_unicode_buffer(length+1); GetWindowText(hwnd, buf, length+1)
        if 'Analysis Complete' in buf.value: PostMessage(hwnd, WM_CLOSE, 0, 0)
    return True
EnumWindows(WNDENUMPROC(close_ac), 0)

# === Read Results ===
model.Results.Setup.DeselectAllCasesAndCombosForOutput()
model.Results.Setup.SetCaseSelectedForOutput('CARGA')

NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]; OE=0

# Col1
i1=int(round(xC1/de)); j1=int(round(yC1/de))
n1=nodes1[(i1,j1)]
[NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret]=model.Results.JointDispl(n1,OE,NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3)
w1_sap=U3[0]*1000 if NR>0 else 0
print(f"\nSAP Col1 ({xC1},{yC1}): Uz = {w1_sap:.3f} mm")

# Col2
NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]
i2=int(round((xC2-xoff2)/de)); j2=int(round(yC2/de))
n2=nodes2[(i2,j2)]
[NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret]=model.Results.JointDispl(n2,OE,NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3)
w2_sap=U3[0]*1000 if NR>0 else 0
print(f"SAP Col2 ({xC2},{yC2}): Uz = {w2_sap:.3f} mm")

# Release COM
import gc; del model; gc.collect()

print(f"\n{'='*60}")
print(f"VALIDACION: Dos Zapatas Sin Viga")
print(f"{'='*60}")
print(f"{'':>20} {'Calcpad':>10} {'Python':>10} {'SAP2000':>10}")
print(f"{'Col1 (med) mm':>20} {-74.312:>10.3f} {-74.312:>10.3f} {w1_sap:>10.3f}")
print(f"{'Col2 (cen) mm':>20} {-42.766:>10.3f} {-42.766:>10.3f} {w2_sap:>10.3f}")
if w1_sap != 0:
    print(f"{'Ratio Col1':>20} {'':>10} {'':>10} {-74.312/w1_sap:>10.4f}")
    print(f"{'Ratio Col2':>20} {'':>10} {'':>10} {-42.766/w2_sap:>10.4f}")
print(f"{'='*60}")
print("SAP2000 libre - sin dialogos bloqueantes")
