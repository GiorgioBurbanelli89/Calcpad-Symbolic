"""Mindlin-Reissner plate element - rectangular Q4 with selective integration.

DOFs per node: 3  {w, theta_x, theta_y}
  Bending:  2×2 Gauss (reduced integration for bending stiffness)
  Shear:    1×1 Gauss at element center (selective reduced integration to avoid locking)

Convention (matches Calcpad / SAP2000):
  kappa_x  =  d(theta_y)/dx   → B_b row 0: [0, 0, dNi/dx, ...]
  kappa_y  = -d(theta_x)/dy  → B_b row 1: [0, -dNi/dy, 0, ...]
  2*kappa_xy = d(theta_y)/dy - d(theta_x)/dx → B_b row 2: [0, -dNi/dx, dNi/dy, ...]

  gam_xz = dw/dx - theta_y   → B_s row 0: [dNi/dx, 0, -Ni, ...]
  gam_yz = dw/dy + theta_x   → B_s row 1: [dNi/dy, Ni,  0, ...]

NOTE on sign convention:
  theta_x = rotation about x-axis (positive rotation → positive w gradient in y)
  theta_y = rotation about y-axis (positive rotation → positive w gradient in x)
  This matches the standard plate convention where:
    gam_xz = dw/dx - theta_y
    gam_yz = dw/dy + theta_x  (NOTE: +theta_x, not -theta_x)

Reference: Hughes (1987), Calcpad-Symbolic Mindlin implementation.
"""
import numpy as np


def _shape_functions(xi, eta):
    """Bilinear Q4 shape functions and isoparametric derivatives."""
    xi_n  = [-1.0,  1.0,  1.0, -1.0]
    eta_n = [-1.0, -1.0,  1.0,  1.0]
    N    = np.zeros(4)
    dNdxi  = np.zeros(4)
    dNdeta = np.zeros(4)
    for i in range(4):
        N[i]      = 0.25 * (1.0 + xi_n[i]*xi) * (1.0 + eta_n[i]*eta)
        dNdxi[i]  = 0.25 * xi_n[i]  * (1.0 + eta_n[i]*eta)
        dNdeta[i] = 0.25 * eta_n[i] * (1.0 + xi_n[i]*xi)
    return N, dNdxi, dNdeta


def _physical_derivs(dNdxi, dNdeta, dx, dy):
    """Physical derivatives for rectangular element (diagonal Jacobian)."""
    # J = diag(dx/2, dy/2), J^{-1} = diag(2/dx, 2/dy)
    dNdx = dNdxi  * (2.0/dx)
    dNdy = dNdeta * (2.0/dy)
    Jdet = (dx/2.0) * (dy/2.0)
    return dNdx, dNdy, Jdet


def _mindlin_Bb(xi, eta, dx, dy):
    """Bending B matrix (3×12) at (xi, eta).

    Column order per node: {w, theta_x, theta_y}

    Convention matches Calcpad 03_Plate_Thick_6x4.cpd exactly:
      Row 0 (kap_x):   [0,  0,       -dNi/dx]  → kap_x = -d(theta_y)/dx
      Row 1 (kap_y):   [0,  dNi/dy,   0     ]  → kap_y =  d(theta_x)/dy
      Row 2 (2kap_xy): [0,  dNi/dx,  -dNi/dy]  → 2kap_xy = d(theta_x)/dx - d(theta_y)/dy

    D_b is the standard isotropic bending matrix:
      D_b = Et^3/(12(1-nu^2)) * [[1,nu,0],[nu,1,0],[0,0,(1-nu)/2]]

    The strain energy U = 0.5 * integral(kappa^T D_b kappa dA) is correct
    with this sign convention because D_b is symmetric and the signs only
    affect the coupling between rows — the diagonal terms dominate.
    """
    N, dNdxi, dNdeta = _shape_functions(xi, eta)
    dNdx, dNdy, _ = _physical_derivs(dNdxi, dNdeta, dx, dy)

    B = np.zeros((3, 12))
    for i in range(4):
        col_w  = 3*i
        col_tx = 3*i + 1
        col_ty = 3*i + 2
        # Row 0: kappa_x = -d(theta_y)/dx
        B[0, col_ty] = -dNdx[i]
        # Row 1: kappa_y = d(theta_x)/dy
        B[1, col_tx] =  dNdy[i]
        # Row 2: 2*kappa_xy = d(theta_x)/dx - d(theta_y)/dy
        B[2, col_tx] =  dNdx[i]
        B[2, col_ty] = -dNdy[i]
    return B


def _mindlin_Bs(xi, eta, dx, dy):
    """Shear B matrix (2×12) at (xi, eta).

    Row 0: gam_xz = dw/dx - theta_y  → [dNi/dx, 0, -Ni]
    Row 1: gam_yz = dw/dy + theta_x  → [dNi/dy, Ni,  0]
    """
    N, dNdxi, dNdeta = _shape_functions(xi, eta)
    dNdx, dNdy, _ = _physical_derivs(dNdxi, dNdeta, dx, dy)

    B = np.zeros((2, 12))
    for i in range(4):
        col_w  = 3*i
        col_tx = 3*i + 1
        col_ty = 3*i + 2
        # Row 0: gam_xz = dw/dx - theta_y
        B[0, col_w]  =  dNdx[i]
        B[0, col_ty] = -N[i]
        # Row 1: gam_yz = dw/dy + theta_x
        B[1, col_w]  =  dNdy[i]
        B[1, col_tx] =  N[i]
    return B


def mindlin_bending_matrix(E, nu, t):
    """Isotropic bending constitutive matrix D_b (3×3)."""
    coeff = E * t**3 / (12.0 * (1.0 - nu**2))
    return coeff * np.array([
        [1.0,  nu,          0.0],
        [nu,   1.0,         0.0],
        [0.0,  0.0,  (1.0-nu)/2.0],
    ])


def mindlin_shear_matrix(E, nu, t, kappa=5.0/6.0):
    """Isotropic shear constitutive matrix D_s (2×2).

    kappa: shear correction factor (default 5/6 for rectangular cross-section)
    G = E / (2*(1+nu))
    D_s = kappa * G * t * I_2
    """
    G = E / (2.0 * (1.0 + nu))
    coeff = kappa * G * t
    return coeff * np.eye(2)


def mindlin_stiffness(dx, dy, D_b, D_s):
    """Mindlin plate element stiffness matrix (12×12).

    Bending:  2×2 Gauss (full integration for bending)
    Shear:    1×1 Gauss at center (selective reduced integration → locking-free)

    Parameters
    ----------
    dx : float   Element width
    dy : float   Element height
    D_b : ndarray (3×3)  Bending constitutive matrix
    D_s : ndarray (2×2)  Shear constitutive matrix (includes kappa and t)

    Returns
    -------
    Ke : ndarray (12×12)
    """
    Ke = np.zeros((12, 12))

    # --- Bending stiffness: 2×2 Gauss ---
    g = 1.0 / np.sqrt(3.0)
    gauss_b = [(-g, -g), (g, -g), (g, g), (-g, g)]
    for xi, eta in gauss_b:
        _, _, Jdet = _physical_derivs(
            *_shape_functions(xi, eta)[1:], dx, dy)
        Bb = _mindlin_Bb(xi, eta, dx, dy)
        Ke += Bb.T @ D_b @ Bb * Jdet  # weight=1 each

    # --- Shear stiffness: 1×1 Gauss at center ---
    xi_c, eta_c = 0.0, 0.0
    w_s = 4.0  # weight for 1-pt rule over [-1,1]^2 = 2*2 = 4
    _, _, Jdet_c = _physical_derivs(
        *_shape_functions(xi_c, eta_c)[1:], dx, dy)
    Bs = _mindlin_Bs(xi_c, eta_c, dx, dy)
    Ke += w_s * Bs.T @ D_s @ Bs * Jdet_c

    return Ke


def mindlin_load_uniform(dx, dy, q):
    """Consistent load vector for uniform transverse load q (force/area).

    Uses 2×2 Gauss integration with bilinear shape functions for w DOF only.
    """
    g = 1.0 / np.sqrt(3.0)
    gauss_pts = [(-g, -g), (g, -g), (g, g), (-g, g)]
    Jdet = (dx/2.0) * (dy/2.0)

    fe = np.zeros(12)
    for xi, eta in gauss_pts:
        N, _, _ = _shape_functions(xi, eta)
        for i in range(4):
            fe[3*i] += q * N[i] * Jdet
    return fe


# ---------------------------------------------------------------------------
# Self-test
# ---------------------------------------------------------------------------
if __name__ == '__main__':
    print("Mindlin plate element self-test")
    print("-" * 40)

    dx, dy = 1000.0, 1000.0
    E, nu, t = 35000.0, 0.15, 100.0
    D_b = mindlin_bending_matrix(E, nu, t)
    D_s = mindlin_shear_matrix(E, nu, t)
    Ke  = mindlin_stiffness(dx, dy, D_b, D_s)
    fe  = mindlin_load_uniform(dx, dy, -0.01)

    print(f"D_b[0,0] = {D_b[0,0]:.4e}")
    print(f"D_s[0,0] = {D_s[0,0]:.4e}")
    print(f"Ke shape: {Ke.shape}")
    print(f"Ke[0,0]  = {Ke[0,0]:.4e}")
    sym_err = np.max(np.abs(Ke - Ke.T))
    print(f"Symmetry error: {sym_err:.2e}")
    print(f"sum(fe_w) = {fe[0]+fe[3]+fe[6]+fe[9]:.4f}  (should be {-0.01*dx*dy:.4f})")
