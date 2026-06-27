# ============================================================================
# generate_slab_s2k.ps1
# ----------------------------------------------------------------------------
# Genera un archivo .s2k (formato texto de SAP2000) que reproduce el modelo
# de "Rectangular Slab FEA.cpd" — sin necesidad de OAPI (que crashea en v24).
#
# Uso: powershell -ExecutionPolicy Bypass -File generate_slab_s2k.ps1
# Salida: $env:TEMP\slab_validate.s2k
#
# Pasos para validar:
#   1. Ejecutar este script → genera el .s2k
#   2. Abrir SAP2000 v24, File > Import > SAP2000 .s2k Text File
#   3. Run Analysis (F5)
#   4. Anotar deflexion central + Mx/My maximos
#   5. Comparar con valor teorico Timoshenko (6.62 mm)
# ============================================================================
param(
    [Parameter(Mandatory=$false)] [int]    $Na = 6,
    [Parameter(Mandatory=$false)] [int]    $Nb = 4
)

# Datos
$a   = 6.0; $b = 4.0; $t = 0.1
$q   = 10.0
$E   = 3.5e7   # kN/m^2
$nu  = 0.15
$dx  = $a / $Na
$dy  = $b / $Nb

$out = "$env:TEMP\slab_validate.s2k"
$sb  = New-Object System.Text.StringBuilder

# Header
$null = $sb.AppendLine('File C:\Slab_Validate.s2k saved 12/01/2026')
$null = $sb.AppendLine('')
$null = $sb.AppendLine('TABLE:  "PROGRAM CONTROL"')
$null = $sb.AppendLine('   ProgramName=SAP2000   Version="24.0.0"   CurrUnits="KN, m, C"')
$null = $sb.AppendLine('')
$null = $sb.AppendLine('TABLE:  "COORDINATE SYSTEMS"')
$null = $sb.AppendLine('   Name=GLOBAL   Type=Cartesian   X=0   Y=0   Z=0   AboutZ=0   AboutY=0   AboutX=0')
$null = $sb.AppendLine('')

# Joints
$null = $sb.AppendLine('TABLE:  "JOINT COORDINATES"')
$nJoint = 1
$joints = @{}
for ($i = 0; $i -le $Na; $i++) {
    for ($j = 0; $j -le $Nb; $j++) {
        $x  = $i * $dx
        $y  = $j * $dy
        $joints["$i,$j"] = $nJoint
        $null = $sb.AppendLine(("   Joint={0}   CoordSys=GLOBAL   CoordType=Cartesian   XorR={1}   Y={2}   Z=0" -f $nJoint, $x, $y))
        $nJoint++
    }
}
$null = $sb.AppendLine('')

# Material
$null = $sb.AppendLine('TABLE:  "MATERIAL PROPERTIES 02 - BASIC MECHANICAL PROPERTIES"')
$null = $sb.AppendLine(("   Material=CONC35   UnitMass=2.5   UnitWeight=24.516625   E1={0}   U12={1}   A1=0.0000099   G12={2}" -f $E, $nu, ($E / (2 * (1 + $nu)))))
$null = $sb.AppendLine('')

# Area section (slab thin)
$null = $sb.AppendLine('TABLE:  "AREA SECTION PROPERTIES"')
$null = $sb.AppendLine(("   Section=SLAB10   Material=CONC35   MatAngle=0   AreaType=Shell   Type=ShellThin   Thickness={0}   BendThick={0}" -f $t))
$null = $sb.AppendLine('')

# Area elements
$null = $sb.AppendLine('TABLE:  "CONNECTIVITY - AREA"')
$nArea = 1
for ($i = 0; $i -lt $Na; $i++) {
    for ($j = 0; $j -lt $Nb; $j++) {
        $j1 = $joints["$i,$j"]
        $j2 = $joints["$($i+1),$j"]
        $j3 = $joints["$($i+1),$($j+1)"]
        $j4 = $joints["$i,$($j+1)"]
        $null = $sb.AppendLine(("   Area={0}   NumJoints=4   Joint1={1}   Joint2={2}   Joint3={3}   Joint4={4}" -f $nArea, $j1, $j2, $j3, $j4))
        $nArea++
    }
}
$null = $sb.AppendLine('')

# Assign section
$null = $sb.AppendLine('TABLE:  "AREA SECTION ASSIGNMENTS"')
for ($k = 1; $k -lt $nArea; $k++) {
    $null = $sb.AppendLine(("   Area={0}   SectionType=Shell   Section=SLAB10   MatProp=Default" -f $k))
}
$null = $sb.AppendLine('')

# Restraints (perimeter Uz=0)
$null = $sb.AppendLine('TABLE:  "JOINT RESTRAINT ASSIGNMENTS"')
foreach ($key in $joints.Keys) {
    $ij = $key.Split(',')
    $i  = [int]$ij[0]; $j = [int]$ij[1]
    if ($i -eq 0 -or $i -eq $Na -or $j -eq 0 -or $j -eq $Nb) {
        $jId = $joints[$key]
        $null = $sb.AppendLine(("   Joint={0}   U1=No   U2=No   U3=Yes   R1=No   R2=No   R3=No" -f $jId))
    }
}
$null = $sb.AppendLine('')

# Load pattern
$null = $sb.AppendLine('TABLE:  "LOAD PATTERN DEFINITIONS"')
$null = $sb.AppendLine('   LoadPat=Q   DesignType=LIVE   SelfWtMult=0')
$null = $sb.AppendLine('')

# Uniform load (negative Z = downward in SAP)
$null = $sb.AppendLine('TABLE:  "AREA LOADS - UNIFORM"')
for ($k = 1; $k -lt $nArea; $k++) {
    $null = $sb.AppendLine(("   Area={0}   LoadPat=Q   CoordSys=GLOBAL   Dir=Gravity   UnifLoad={1}" -f $k, $q))
}
$null = $sb.AppendLine('')

# End
$null = $sb.AppendLine('END TABLE DATA')

[System.IO.File]::WriteAllText($out, $sb.ToString(), [System.Text.Encoding]::UTF8)

Write-Output "===== Datos del modelo ====="
Write-Output ("a={0} m, b={1} m, t={2} m" -f $a, $b, $t)
Write-Output ("q={0} kN/m^2, E={1} kN/m^2, nu={2}" -f $q, $E, $nu)
Write-Output ("Malla: {0} x {1} = {2} elementos shell thin" -f $Na, $Nb, ($Na * $Nb))
Write-Output ""
$D       = $E * [Math]::Pow($t, 3) / (12 * (1 - $nu * $nu))
$w_theo  = 0.00772 * $q * [Math]::Pow($b, 4) / $D
$Mx_theo = 0.0812 * $q * $b * $b
$My_theo = 0.0498 * $q * $b * $b
Write-Output "===== Valores teoricos (Timoshenko, a/b=1.5) ====="
Write-Output ("w_max  = {0:N3} mm (centro)" -f ($w_theo * 1000))
Write-Output ("Mx_max = {0:N3} kNm/m" -f $Mx_theo)
Write-Output ("My_max = {0:N3} kNm/m" -f $My_theo)
Write-Output ""
Write-Output "[OK] Archivo .s2k generado en:"
Write-Output "  $out"
Write-Output ""
Write-Output "Para validar en SAP2000:"
Write-Output "  1. Abrir SAP2000 v24"
Write-Output "  2. File > Import > SAP2000 .s2k Text File > seleccionar el .s2k"
Write-Output "  3. Verificar modelo: 6m x 4m placa con bordes restringidos en Uz"
Write-Output "  4. F5 (Run Analysis)"
Write-Output "  5. Display > Show Deformed Shape > caso Q > anotar Uz del centro"
Write-Output "  6. Comparar con valor teorico arriba (~6.62 mm)"
