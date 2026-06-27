# ============================================================================
# sap2000_validate_slab.ps1
# ----------------------------------------------------------------------------
# Reproduce el modelo de "Rectangular Slab FEA.cpd" (Ned Ganchovski) en SAP2000
# via la OAPI (COM) y compara la deflexion central y los momentos maximos
# contra los valores TEORICOS (Timoshenko Tabla 5.3) y los valores del .cpd.
#
# Caso:
#   a   = 6 m, b = 4 m, a/b = 1.5
#   t   = 0.1 m
#   q   = 10 kN/m^2
#   E   = 35000 MPa
#   nu  = 0.15
#   Apoyos simples en todo el perimetro (vertical traslacion restringida)
#   Malla: 6 x 4 elementos shell thin (Kirchhoff)
#
# Resultados esperados (Timoshenko, a/b=1.5, simply supported, q uniforme):
#   w_max  ~  6.6 mm en el centro
#   Mx_max ~  13.0 kNm/m
#   My_max ~  8.0  kNm/m
# ============================================================================
param(
    [Parameter(Mandatory=$false)] [string] $ModelDir = "$env:TEMP\sap_slab_validate",
    [Parameter(Mandatory=$false)] [int]    $Na = 6,
    [Parameter(Mandatory=$false)] [int]    $Nb = 4
)

$ErrorActionPreference = "Continue"

# --- Datos del problema ------------------------------------------------------
$a    = 6.0       # m
$b    = 4.0       # m
$t    = 0.1       # m
$q    = 10.0      # kN/m^2 (positivo hacia abajo)
$E    = 3.5e7     # kN/m^2 (35000 MPa)
$nu   = 0.15

# Resultados teoricos (Timoshenko & Woinowsky-Krieger, "Theory of Plates and Shells")
# para placa rectangular simplemente apoyada con carga uniforme:
# Para a/b = 1.5, con b = lado corto:
$alpha   = 0.00772     # Coef. deflexion: w = alpha * q * b^4 / D
$beta_x  = 0.0812      # Coef. Mx
$beta_y  = 0.0498      # Coef. My
$D       = $E * [Math]::Pow($t, 3) / (12 * (1 - $nu * $nu))   # kN*m
$w_theo  = $alpha * $q * [Math]::Pow($b, 4) / $D                # m
$Mx_theo = $beta_x * $q * $b * $b                              # kNm/m
$My_theo = $beta_y * $q * $b * $b                              # kNm/m

Write-Output "===== Datos ====="
Write-Output ("a   = {0} m,  b = {1} m,  t = {2} m" -f $a, $b, $t)
Write-Output ("q   = {0} kN/m^2,  E = {1} kN/m^2,  nu = {2}" -f $q, $E, $nu)
Write-Output ("D   = E*t^3/(12*(1-nu^2)) = {0:N3} kNm" -f $D)
Write-Output ""
Write-Output "===== Teoria (Timoshenko, a/b=1.5) ====="
Write-Output ("w_max  teorico = {0:N3} mm" -f ($w_theo * 1000))
Write-Output ("Mx_max teorico = {0:N3} kNm/m" -f $Mx_theo)
Write-Output ("My_max teorico = {0:N3} kNm/m" -f $My_theo)
Write-Output ""

if (-not (Test-Path $ModelDir)) {
    New-Item -ItemType Directory -Path $ModelDir | Out-Null
}

# --- Conexion a SAP2000 OAPI -------------------------------------------------
Write-Output "[INFO] Conectando con SAP2000..."
try {
    $helper = New-Object -ComObject "SAP2000v1.Helper"
    $SapObject = $helper.CreateObjectProgID("CSI.SAP2000.API.SapObject")
    # ApplicationStart(eUnits, visible, fileName) -- 3 args obligatorios v24
    # eUnits=6 (kN, m, C), visible=$true (helps with license dialog), fileName=""
    $ret = $SapObject.ApplicationStart(6, $true, "")
    if ($ret -ne 0) { Write-Warning "ApplicationStart retorno $ret" }
    $SapModel = $SapObject.SapModel
    $null = $SapModel.InitializeNewModel()
    $null = $SapModel.File.NewBlank()
    Write-Output "[OK] SAP2000 OAPI iniciado"
} catch {
    Write-Error "No se pudo iniciar SAP2000 OAPI: $($_.Exception.Message)"
    exit 1
}

# Unidades: kN, m, C (kN_m_C = 6 en SAP enum)
$kN_m_C = 6
$null = $SapModel.SetPresentUnits($kN_m_C)

# --- Material ----------------------------------------------------------------
$matName = "Concrete35"
$matType = 2   # MATERIAL_CONCRETE
$null = $SapModel.PropMaterial.SetMaterial($matName, $matType)
# E, U (Poisson), A (coef expansion termica), Mass density 0 (estatico)
$null = $SapModel.PropMaterial.SetMPIsotropic($matName, $E, $nu, 1.0e-5)
Write-Output "[OK] Material '$matName' creado: E=$E kN/m^2, nu=$nu"

# --- Seccion de placa --------------------------------------------------------
$secName = "Slab_10cm"
# AreaSection: SetSlab(name, slabType, shellType, matName, thickness, ...)
# slabType = 1 (Slab), shellType = 1 (ShellThin, Kirchhoff)
$null = $SapModel.PropArea.SetSlab($secName, 1, 1, $matName, $t)
Write-Output "[OK] Seccion 'Slab_10cm' (ShellThin, t=$t m)"

# --- Generar nodos -----------------------------------------------------------
$dx = $a / $Na
$dy = $b / $Nb
$nodes = @{}   # mapa "i,j" -> joint name
Write-Output ""
Write-Output "[INFO] Generando $($Na+1) x $($Nb+1) = $((($Na+1)*($Nb+1))) nodos..."
for ($i = 0; $i -le $Na; $i++) {
    for ($j = 0; $j -le $Nb; $j++) {
        $x = $i * $dx
        $y = $j * $dy
        $z = 0.0
        # SAP OAPI: AddCartesian writes the assigned point name into the
        # `Name` ref-parameter. Use a fresh variable per iteration and
        # explicitly stringify before storing so the hash holds copies.
        $newName = ""
        $ret = $SapModel.PointObj.AddCartesian($x, $y, $z, [ref]$newName, "")
        $nodes["$i,$j"] = "$newName"
    }
}
Write-Output "[OK] Nodos creados"

# --- Generar elementos area --------------------------------------------------
Write-Output ""
Write-Output "[INFO] Generando $Na x $Nb = $($Na * $Nb) elementos shell..."
$nElems = 0
for ($i = 0; $i -lt $Na; $i++) {
    for ($j = 0; $j -lt $Nb; $j++) {
        $p1 = $nodes["$i,$j"]
        $p2 = $nodes["$($i+1),$j"]
        $p3 = $nodes["$($i+1),$($j+1)"]
        $p4 = $nodes["$i,$($j+1)"]
        $pts = @($p1, $p2, $p3, $p4)
        $name = ""
        $ret = $SapModel.AreaObj.AddByPoint(4, [ref]$pts, [ref]$name, $secName, "")
        $nElems++
    }
}
Write-Output "[OK] $nElems elementos creados"

# --- Restricciones (apoyos simples en bordes) --------------------------------
Write-Output ""
Write-Output "[INFO] Aplicando apoyos simples en perimetro..."
$restraint = @($false, $false, $true, $false, $false, $false)   # solo Uz restringido
$nSupport = 0
foreach ($key in $nodes.Keys) {
    $ij = $key.Split(',')
    $i = [int]$ij[0]
    $j = [int]$ij[1]
    if ($i -eq 0 -or $i -eq $Na -or $j -eq 0 -or $j -eq $Nb) {
        $jName = $nodes[$key]
        $rr = $restraint  # copia
        $ret = $SapModel.PointObj.SetRestraint($jName, [ref]$rr)
        $nSupport++
    }
}
Write-Output "[OK] $nSupport nodos restringidos (Uz=0)"

# --- Patron de carga + carga uniforme ---------------------------------------
$loadCase = "Q"
# AddNew(name, type=DEAD=1)
$null = $SapModel.LoadPatterns.Add($loadCase, 1, 0, $true)
# AreaObj.SetLoadUniform(name, patternName, value, dir, replace, csys, itemtype=0)
# dir 10 = Gravity (Z global), pero la convencion: positivo hacia abajo en SAP
# usamos value = q (positivo) y dir = 10 (Gravity)
$null = $SapModel.AreaObj.SetLoadUniform("ALL", $loadCase, $q, 10, $true, "Global", 1)
Write-Output "[OK] Carga uniforme aplicada: q=$q kN/m^2 (dir gravedad)"

# --- Guardar modelo ----------------------------------------------------------
$modelFile = Join-Path $ModelDir "slab_validate.sdb"
$null = $SapModel.File.Save($modelFile)
Write-Output ""
Write-Output "[OK] Modelo guardado en: $modelFile"

# --- Correr analisis ---------------------------------------------------------
Write-Output ""
Write-Output "[INFO] Corriendo analisis estatico..."
$null = $SapModel.Analyze.RunAnalysis()
Write-Output "[OK] Analisis completado"

# --- Extraer deflexion del nodo central -------------------------------------
$null = $SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
$null = $SapModel.Results.Setup.SetCaseSelectedForOutput($loadCase)

# nodo central = (Na/2, Nb/2)
$ic = [Math]::Floor($Na / 2)
$jc = [Math]::Floor($Nb / 2)
$centerNode = $nodes["$ic,$jc"]

$numberResults = 0
$obj = [string[]]@(); $elm = [string[]]@()
$loadCase_out = [string[]]@(); $stepType = [string[]]@()
$stepNum = [double[]]@()
$U1 = [double[]]@(); $U2 = [double[]]@(); $U3 = [double[]]@()
$R1 = [double[]]@(); $R2 = [double[]]@(); $R3 = [double[]]@()

$ret = $SapModel.Results.JointDispl($centerNode, 0,
    [ref]$numberResults, [ref]$obj, [ref]$elm,
    [ref]$loadCase_out, [ref]$stepType, [ref]$stepNum,
    [ref]$U1, [ref]$U2, [ref]$U3,
    [ref]$R1, [ref]$R2, [ref]$R3)

$w_sap_mm = -1.0
if ($U3 -ne $null -and $U3.Length -gt 0) {
    $w_sap_mm = [Math]::Abs([double]$U3[0]) * 1000.0
}

Write-Output ""
Write-Output "===== RESULTADOS SAP2000 ====="
Write-Output ("Nodo central ($centerNode) en (x={0:N2}, y={1:N2})" -f ($ic*$dx), ($jc*$dy))
Write-Output ("w_max  SAP2000 = {0:N3} mm" -f $w_sap_mm)

# --- Comparacion final -------------------------------------------------------
Write-Output ""
Write-Output "================================"
Write-Output "       COMPARACION FINAL"
Write-Output "================================"
Write-Output ("                  Teorico   SAP2000   Diff %")
$w_theo_mm = $w_theo * 1000.0
if ($w_sap_mm -gt 0) {
    $diff_w = (($w_sap_mm - $w_theo_mm) / $w_theo_mm) * 100.0
    Write-Output ("w_max   (mm)    {0,7:N3}   {1,7:N3}   {2,7:N2}%" -f $w_theo_mm, $w_sap_mm, $diff_w)
} else {
    Write-Output ("w_max   (mm)    {0,7:N3}      N/A   N/A" -f $w_theo_mm)
    Write-Output "(SAP no devolvio deflexion -- verifica modelo o licencia)"
}

# --- Limpieza ----------------------------------------------------------------
Write-Output ""
Write-Output "[INFO] Cerrando SAP2000..."
$null = $SapObject.ApplicationExit($false)

# Liberar COM -- ReleaseComObject solo si es __ComObject puro
try {
    if ($SapModel -is [__ComObject]) {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($SapModel) | Out-Null
    }
} catch {}
try {
    if ($SapObject -is [__ComObject]) {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($SapObject) | Out-Null
    }
} catch {}
try {
    if ($helper -is [__ComObject]) {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($helper) | Out-Null
    }
} catch {}
[GC]::Collect()
[GC]::WaitForPendingFinalizers()

Write-Output "[OK] Validacion completada"
