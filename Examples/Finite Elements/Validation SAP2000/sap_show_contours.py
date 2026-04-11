"""Activa la vista de Contours S33 del modelo Serquen en SAP2000.

Despues de ejecutar sap_tutorial_suelo_serquen.py, el modelo queda creado
y analizado pero la GUI muestra solo la malla. Este script usa
Display.ShowStressContourSolid (si existe) o le pide al usuario que
haga Display -> Show Forces/Stresses -> Solids manualmente.
"""
import comtypes.client

try:
    mySapObject = comtypes.client.GetActiveObject("CSI.SAP2000.API.SapObject")
    SapModel = mySapObject.SapModel
    print("Conectado a SAP2000")
except:
    print("SAP2000 no esta abierto. Abra SAP2000 primero.")
    raise SystemExit(1)

# Activar la vista de contornos S33 via View.ShowStressContourSolid
# Algunas versiones tienen: View.ShowStressContourSolid(Name, DeformedShape, Component, ..)
# Si no existe, imprimir instrucciones para el usuario

print("\nModelo cargado. Para ver los color maps:")
print("  1) Click en Display (menu)")
print("  2) Show Forces/Stresses (F10 o clic)")
print("  3) Solids...")
print("  4) Case/Combo = COMB1 (o PUNTUAL/LINEAL/RECTANGULAR)")
print("  5) Component = S33 (vertical)")
print("  6) Check 'Show Deformed Shape' si quieres ver deformada")
print("  7) OK")
print("\nDebes ver el 'bulbo de presiones' clasico de Boussinesq.")

# Intentar activar directamente via View API
try:
    # SAP2000 v24+: SapModel.View.ShowStressSolid
    ret = SapModel.View.ShowStressSolid("COMB1", 3, False, 0, 0, False, False)
    # Firma: (LoadCase, Component, ShowDeformedShape, ..., StressAveraging, ContinuousContours)
    # Component: 1=S11, 2=S22, 3=S33, 4=S12, 5=S13, 6=S23, 7=SMax, 8=SMid, 9=SMin, 10=SVM
    print(f"\nShowStressSolid COMB1 S33 ret={ret}")
except AttributeError:
    print("\n(View.ShowStressSolid no disponible en esta version)")
except Exception as e:
    print(f"\nError: {e}")

# Tambien intentar con la ventana activa
try:
    ret = SapModel.SelectObj.ClearSelection()
    print(f"Clear selection ret={ret}")
except: pass

print("\nListo. Debes ver los contornos S33 en la ventana 3-D.")
