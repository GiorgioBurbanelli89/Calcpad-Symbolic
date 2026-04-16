"""Dos Zapatas con AREA SPRINGS — para ver Soil Pressure en SAP2000"""
import sys, os, time, ctypes
from ctypes import wintypes
sys.path.insert(0, r'C:\Users\j-b-j\Documents\Hekatan Calc 1.0.0\Pipy Api sap2000 hekatan\src')
import pysap2000

E=25000000.; nu=0.2; t=0.5; ks=20000.; de=0.25
Lz1=2.; Bz1=2.; xC1=0.25; yC1=1.0; P1=80.*9.80665; Lc1=0.4
Lz2=2.5; Bz2=2.; P2=120.*9.80665; Lc2=0.4
xoff2=5.0; xC2=xoff2+Lz2/2; yC2=Bz2/2

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

eid=1
for j in range(ny1):
    for i in range(nx1):
        pts=[nodes1[(i,j)],nodes1[(i+1,j)],nodes1[(i+1,j+1)],nodes1[(i,j+1)]]
        model.AreaObj.AddByPoint(4, pts, f'Z1E{eid}', 'zapata'); eid+=1

# === Zapata 2 ===
nx2=int(Lz2/de); ny2=int(Bz2/de)
nodes2={}
for j in range(ny2+1):
    for i in range(nx2+1):
        name=str(nid); model.PointObj.AddCartesian(xoff2+i*de, j*de, 0, '', name)
        nodes2[(i,j)]=name; nid+=1

for j in range(ny2):
    for i in range(nx2):
        pts=[nodes2[(i,j)],nodes2[(i+1,j)],nodes2[(i+1,j+1)],nodes2[(i,j+1)]]
        model.AreaObj.AddByPoint(4, pts, f'Z2E{eid}', 'zapata'); eid+=1

print(f"Nodes: {nid-1}, Elements: {eid-1}")

# === AREA SPRINGS (using Group='ALL') ===
raw = model.AreaObj._cAreaObj__com_SetSpring
ret_val = ctypes.c_long(0)
vec = [0.0, 0.0, 0.0]
# Name='ALL', MyType=1, s=ks, SimpleSpringType=1, LinkProp='', Face=-1(bottom),
# SpringLocalOneType=1, Dir=3, Outward=True, Vec, Ang=0, Replace=False, CSys='Local', ItemType=1(Group)
raw('ALL', 1, ks, 1, '', -1, 1, 3, True, vec, 0.0, False, 'Local', 1, ctypes.byref(ret_val))
print(f"Area springs: ret={ret_val.value} {'OK' if ret_val.value==0 else 'FAIL'}")

# === Loads ===
model.LoadPatterns.Add('CARGA', 1, 0, True)

# Col1 (same tolerance as Python: Lc/2 + de/2)
nc1=0
for j in range(ny1+1):
    for i in range(nx1+1):
        x=i*de; y=j*de
        if abs(x-xC1)<=Lc1/2+de/2 and abs(y-yC1)<=Lc1/2+de/2:
            tx=de if 0<i<nx1 else de/2; ty=de if 0<j<ny1 else de/2
            model.PointObj.SetLoadForce(nodes1[(i,j)], 'CARGA', [0,0,-P1/(Lc1**2)*tx*ty,0,0,0])
            nc1+=1

# Col2
nc2=0
for j in range(ny2+1):
    for i in range(nx2+1):
        x=xoff2+i*de; y=j*de
        if abs(x-xC2)<=Lc2/2+de/2 and abs(y-yC2)<=Lc2/2+de/2:
            tx=de if 0<i<nx2 else de/2; ty=de if 0<j<ny2 else de/2
            model.PointObj.SetLoadForce(nodes2[(i,j)], 'CARGA', [0,0,-P2/(Lc2**2)*tx*ty,0,0,0])
            nc2+=1

print(f"Col1: {nc1} nodes, Col2: {nc2} nodes")

# === Save + Run ===
save_path = os.path.join(r'C:\CSiAPIexample', 'dos_zapatas_areaspring.sdb')
model.File.Save(save_path)
model.Analyze.SetRunCaseFlag('DEAD', False)
model.Analyze.SetRunCaseFlag('CARGA', True)
ret = model.Analyze.RunAnalysis()
print(f"Analysis: ret={ret}")

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
def close_sap(hwnd, lparam):
    cls=ctypes.create_unicode_buffer(256); GetClassName(hwnd, cls, 256)
    if cls.value=='#32770':
        l=GetWindowTextLength(hwnd)
        if l>0:
            buf=ctypes.create_unicode_buffer(l+1); GetWindowText(hwnd, buf, l+1)
            if any(k in buf.value.lower() for k in ['sap','analysis','complete']):
                def click(child, lp):
                    c=ctypes.create_unicode_buffer(256); GetClassName(child, c, 256)
                    if 'Button' in c.value: SendMessage(child, BM_CLICK, 0, 0)
                    return True
                EnumChildWindows(hwnd, CHILDPROC(click), 0)
                PostMessage(hwnd, WM_CLOSE, 0, 0)
    return True
for _ in range(3):
    EnumWindows(WNDENUMPROC(close_sap), 0); time.sleep(1)
def close_ac(hwnd, lparam):
    l=GetWindowTextLength(hwnd)
    if l>0:
        buf=ctypes.create_unicode_buffer(l+1); GetWindowText(hwnd, buf, l+1)
        if 'Analysis Complete' in buf.value: PostMessage(hwnd, WM_CLOSE, 0, 0)
    return True
EnumWindows(WNDENUMPROC(close_ac), 0)

# === Results ===
model.Results.Setup.DeselectAllCasesAndCombosForOutput()
model.Results.Setup.SetCaseSelectedForOutput('CARGA')
NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]; OE=0

i1=int(round(xC1/de)); j1=int(round(yC1/de))
[NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret]=model.Results.JointDispl(nodes1[(i1,j1)],OE,NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3)
w1=U3[0]*1000 if NR>0 else 0

NR=0; Obj=Elm=AC=ST=SN=U1=U2=U3=R1=R2=R3=[]
i2=int(round((xC2-xoff2)/de)); j2=int(round(yC2/de))
[NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3,ret]=model.Results.JointDispl(nodes2[(i2,j2)],OE,NR,Obj,Elm,AC,ST,SN,U1,U2,U3,R1,R2,R3)
w2=U3[0]*1000 if NR>0 else 0

import gc; del model; gc.collect()

print(f'\\n===== RESULTADOS =====')
print(f'SAP Col1: {w1:.3f} mm (Calcpad: -74.312)')
print(f'SAP Col2: {w2:.3f} mm (Calcpad: -42.766)')
print(f'Ratio Col1: {-74.312/w1:.4f}' if w1!=0 else '')
print(f'Ratio Col2: {-42.766/w2:.4f}' if w2!=0 else '')
print(f'\\nSAP2000 libre. Usa Display > Show Forces/Stresses > Soil Pressure')
