"""DKQ Shell-Thin element (Discrete Kirchhoff Quadrilateral) - rectangular version.

DOFs per node: 3  {w, theta_x, theta_y}
  where:  beta_x = theta_y  (rotation about x-axis → bending in y-plane)
          beta_y = -theta_x (rotation about y-axis → bending in x-plane, NEGATED)

Element nodes (isoparametric):
  1: (-1,-1)  2: (+1,-1)  3: (+1,+1)  4: (-1,+1)
  (bottom-left, bottom-right, top-right, top-left)

B matrix is 3×12, assembled from the explicit rectangular DKQ formulas.
Reference: Batoz & Tahar (1982), Calcpad-Symbolic DKQ implementation.
"""
import numpy as np


def _dkq_Bmatrix(xi, eta, dx, dy):
    """Compute DKQ B matrix (3×12) at Gauss point (xi, eta).

    dx = half-width  (element width  = 2*dx  in physical coords)
    dy = half-height (element height = 2*dy  in physical coords)

    NOTE: dx and dy here are the FULL element dimensions (not half),
    consistent with the Calcpad formulation where J1=dx/2, J2=dy/2.
    """
    # Row 1: kappa_x = d(beta_x)/dx = d(theta_y)/dx
    #   col w:    3*xi*(1-eta)/dx^2   etc.
    #   col tx:   0                   (beta_x doesn't depend on theta_x)
    #   col ty:   (1-eta)*(3*xi-1)/(2*dx)  etc.
    Rx_w1 =  3.0*xi*(1.0-eta)   / dx**2
    Rx_t1 =  (1.0-eta)*(3.0*xi-1.0) / (2.0*dx)
    Rx_w2 = -3.0*xi*(1.0-eta)   / dx**2
    Rx_t2 =  (1.0-eta)*(1.0+3.0*xi) / (2.0*dx)
    Rx_w3 = -3.0*xi*(1.0+eta)   / dx**2
    Rx_t3 =  (1.0+eta)*(1.0+3.0*xi) / (2.0*dx)
    Rx_w4 =  3.0*xi*(1.0+eta)   / dx**2
    Rx_t4 =  (1.0+eta)*(3.0*xi-1.0) / (2.0*dx)

    # Row 2: kappa_y = d(beta_y)/dy = -d(theta_x)/dy  (theta_x = -beta_y)
    #   col w:    3*(1-xi)*eta/dy^2  etc.
    #   col tx:   NEGATED beta_y coefs  → -(1-xi)*(3*eta-1)/(2*dy)  etc.
    #   col ty:   0
    Ry_w1 =  3.0*(1.0-xi)*eta   / dy**2
    Ry_t1 =  (1.0-xi)*(3.0*eta-1.0) / (2.0*dy)   # beta_y coef (to be negated)
    Ry_w2 =  3.0*(1.0+xi)*eta   / dy**2
    Ry_t2 =  (1.0+xi)*(3.0*eta-1.0) / (2.0*dy)
    Ry_w3 = -3.0*(1.0+xi)*eta   / dy**2
    Ry_t3 =  (1.0+xi)*(1.0+3.0*eta) / (2.0*dy)
    Ry_w4 = -3.0*(1.0-xi)*eta   / dy**2
    Ry_t4 =  (1.0-xi)*(1.0+3.0*eta) / (2.0*dy)

    # Row 3: 2*kappa_xy = d(beta_x)/dy + d(beta_y)/dx
    #   T = 3*(2 - xi^2 - eta^2) / (2*dx*dy)
    #   col tx: NEGATED beta_y-y coefs
    #   col ty: beta_x-x coefs
    TT = 3.0*(2.0 - xi**2 - eta**2) / (2.0*dx*dy)
    Rxy_bx1 =  (1.0-xi)*(1.0+3.0*xi)  / (4.0*dy)
    Rxy_by1 =  (1.0-eta)*(1.0+3.0*eta) / (4.0*dx)
    Rxy_bx2 =  (1.0+xi)*(1.0-3.0*xi)  / (4.0*dy)
    Rxy_by2 = -(1.0-eta)*(1.0+3.0*eta) / (4.0*dx)
    Rxy_bx3 =  (1.0+xi)*(3.0*xi-1.0)  / (4.0*dy)
    Rxy_by3 =  (1.0+eta)*(3.0*eta-1.0) / (4.0*dx)
    Rxy_bx4 = -(1.0-xi)*(1.0+3.0*xi)  / (4.0*dy)
    Rxy_by4 =  (1.0+eta)*(1.0-3.0*eta) / (4.0*dx)

    # Assemble B (3×12), column order per node: {w, theta_x, theta_y}
    # Row 1 (kappa_x): col_tx = 0, col_ty = Rx_t
    # Row 2 (kappa_y): col_tx = -Ry_t (negated), col_ty = 0
    # Row 3 (2*kappa_xy): col_tx = -Rxy_by (negated), col_ty = Rxy_bx
    B = np.array([
        # Row 1: kappa_x
        [Rx_w1, 0.0,    Rx_t1,
         Rx_w2, 0.0,    Rx_t2,
         Rx_w3, 0.0,    Rx_t3,
         Rx_w4, 0.0,    Rx_t4],
        # Row 2: kappa_y
        [Ry_w1, -Ry_t1, 0.0,
         Ry_w2, -Ry_t2, 0.0,
         Ry_w3, -Ry_t3, 0.0,
         Ry_w4, -Ry_t4, 0.0],
        # Row 3: 2*kappa_xy
        [ TT,   -Rxy_by1, Rxy_bx1,
         -TT,   -Rxy_by2, Rxy_bx2,
          TT,   -Rxy_by3, Rxy_bx3,
         -TT,   -Rxy_by4, Rxy_bx4],
    ])
    return B


def dkq_stiffness(dx, dy, D_b):
    """Compute DKQ element stiffness matrix (12×12).

    Parameters
    ----------
    dx : float   Element width  (physical, e.g. mm)
    dy : float   Element height (physical, e.g. mm)
    D_b : ndarray (3×3)  Bending constitutive matrix  [D11 D12 0; ...]

    Returns
    -------
    Ke : ndarray (12×12)  Symmetric element stiffness
    """
    g = 1.0 / np.sqrt(3.0)
    gauss_pts = [(-g, -g), (g, -g), (g, g), (-g, g)]
    w = 1.0  # all weights = 1 for 2×2 Gauss
    Jdet = (dx/2.0) * (dy/2.0)   # Jacobian determinant (constant for rectangle)

    Ke = np.zeros((12, 12))
    for xi, eta in gauss_pts:
        B = _dkq_Bmatrix(xi, eta, dx, dy)
        Ke += w * w * B.T @ D_b @ B * Jdet
    return Ke


def dkq_load_uniform(dx, dy, q):
    """Consistent load vector for uniform transverse load q (force/area).

    Uses same 2×2 Gauss integration with bilinear shape functions for w.
    N = 0.25*(1+xi_i*xi)*(1+eta_i*eta)

    Returns fe (12,) — only the w DOFs get load (indices 0,3,6,9).
    """
    g = 1.0 / np.sqrt(3.0)
    gauss_pts = [(-g, -g), (g, -g), (g, g), (-g, g)]
    xi_nodes  = [-1.0,  1.0,  1.0, -1.0]
    eta_nodes = [-1.0, -1.0,  1.0,  1.0]
    Jdet = (dx/2.0) * (dy/2.0)

    fe = np.zeros(12)
    for xi, eta in gauss_pts:
        for n in range(4):
            N_n = 0.25 * (1.0 + xi_nodes[n]*xi) * (1.0 + eta_nodes[n]*eta)
            fe[3*n] += q * N_n * Jdet  # only w DOF
    return fe


def dkq_bending_matrix(E, nu, t):
    """Isotropic bending constitutive matrix D_b (3×3)."""
    coeff = E * t**3 / (12.0 * (1.0 - nu**2))
    return coeff * np.array([
        [1.0,  nu,          0.0],
        [nu,   1.0,         0.0],
        [0.0,  0.0,  (1.0-nu)/2.0],
    ])


# ---------------------------------------------------------------------------
# Self-test
# ---------------------------------------------------------------------------
if __name__ == '__main__':
    print("DKQ element self-test")
    print("-" * 40)

    # Unit rectangle
    dx, dy = 50.0, 50.0
    E, nu, t = 200000.0, 0.3, 50.0
    D_b = dkq_bending_matrix(E, nu, t)
    Ke  = dkq_stiffness(dx, dy, D_b)
    fe  = dkq_load_uniform(dx, dy, -1.0)

    print(f"D_b[0,0] = {D_b[0,0]:.4e}  (expected ~1.154e9 N·mm for E=200000,t=50,nu=0.3)")
    print(f"Ke shape: {Ke.shape}")
    print(f"Ke[0,0]  = {Ke[0,0]:.4e}")
    print(f"sum(fe)  = {sum(fe):.4f}  (should be q*dx*dy = {-1*dx*dy:.1f})")
    print(f"fe       = {fe}")
    sym_err = np.max(np.abs(Ke - Ke.T))
    print(f"Symmetry error: {sym_err:.2e}")
