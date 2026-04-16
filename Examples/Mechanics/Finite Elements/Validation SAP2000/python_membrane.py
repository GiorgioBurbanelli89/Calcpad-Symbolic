"""Membrane Q4 element - bilinear plane-stress quadrilateral.

DOFs per node: 2  {u, v}
Element nodes (isoparametric):
  1: (-1,-1)  2: (+1,-1)  3: (+1,+1)  4: (-1,+1)

Gauss integration: 2×2 (full integration — appropriate for membrane)

Reference: standard isoparametric Q4, Calcpad-Symbolic membrane implementation.
"""
import numpy as np


def _q4_shape_derivs(xi, eta, dx, dy):
    """Shape function physical derivatives for rectangular Q4.

    For a rectangle with width dx, height dy (physical dimensions),
    the Jacobian is diagonal: J = diag(dx/2, dy/2), det(J) = dx*dy/4.

    dN/dx_i = (2/dx) * dN/dxi_i
    dN/dy_i = (2/dy) * dN/deta_i

    Node numbering:
      1: (-1,-1)   2: (+1,-1)   3: (+1,+1)   4: (-1,+1)
    """
    xi_n  = [-1.0,  1.0,  1.0, -1.0]
    eta_n = [-1.0, -1.0,  1.0,  1.0]

    dNdx = np.zeros(4)
    dNdy = np.zeros(4)
    for i in range(4):
        dNdxi  = 0.25 * xi_n[i]  * (1.0 + eta_n[i]*eta)
        dNdeta = 0.25 * eta_n[i] * (1.0 + xi_n[i]*xi)
        dNdx[i] = dNdxi  * (2.0/dx)
        dNdy[i] = dNdeta * (2.0/dy)

    return dNdx, dNdy


def _q4_Bmatrix(xi, eta, dx, dy):
    """Membrane B matrix (3×8) at (xi, eta).

    Column order per node: {u, v}
    Row 0: eps_x = du/dx
    Row 1: eps_y = dv/dy
    Row 2: gam_xy = du/dy + dv/dx
    """
    dNdx, dNdy = _q4_shape_derivs(xi, eta, dx, dy)

    B = np.zeros((3, 8))
    for i in range(4):
        col_u = 2*i
        col_v = 2*i + 1
        B[0, col_u] = dNdx[i]          # eps_x = du/dx
        B[1, col_v] = dNdy[i]          # eps_y = dv/dy
        B[2, col_u] = dNdy[i]          # gam_xy (u part)
        B[2, col_v] = dNdx[i]          # gam_xy (v part)
    return B


def membrane_plane_stress(E, nu, t):
    """Plane-stress constitutive matrix D_m (3×3) × thickness t."""
    coeff = E * t / (1.0 - nu**2)
    return coeff * np.array([
        [1.0,  nu,          0.0],
        [nu,   1.0,         0.0],
        [0.0,  0.0,  (1.0-nu)/2.0],
    ])


def membrane_stiffness(dx, dy, D_m):
    """Membrane Q4 element stiffness matrix (8×8).

    Parameters
    ----------
    dx : float   Element width
    dy : float   Element height
    D_m : ndarray (3×3)  Plane-stress D matrix (already includes thickness)

    Returns
    -------
    Ke : ndarray (8×8)
    """
    g = 1.0 / np.sqrt(3.0)
    gauss_pts = [(-g, -g), (g, -g), (g, g), (-g, g)]
    w = 1.0
    Jdet = (dx/2.0) * (dy/2.0)

    Ke = np.zeros((8, 8))
    for xi, eta in gauss_pts:
        B = _q4_Bmatrix(xi, eta, dx, dy)
        Ke += w * w * B.T @ D_m @ B * Jdet
    return Ke


def membrane_load_lateral(dx, dy, p, direction='x'):
    """Lumped nodal load for uniform distributed load on element edge.

    p : force per unit length (e.g. N/mm)
    direction : 'x' or 'y'
    Applies to TOP edge (nodes 3,4) for lateral (body force approach).
    Here we implement a simpler tributary-area approach for body forces.
    """
    fe = np.zeros(8)
    # Tributary area per node = dx*dy/4, applied as body force direction
    load_per_node = p * dx * dy / 4.0
    for i in range(4):
        if direction == 'x':
            fe[2*i] = load_per_node
        else:
            fe[2*i+1] = load_per_node
    return fe


def membrane_load_edge_x(dy, p_total, nodes=(2, 3)):
    """Distributed load p (force/length) on vertical edge of element.

    nodes: tuple of two local node indices (0-based) on that edge.
    Returns fe (8,) — only x-DOFs affected.
    p_total: total force on edge = p_intensity * dy
    """
    fe = np.zeros(8)
    # Consistent: equal halves to both end nodes of the edge
    for n in nodes:
        fe[2*n] = p_total / 2.0
    return fe


# ---------------------------------------------------------------------------
# Self-test
# ---------------------------------------------------------------------------
if __name__ == '__main__':
    print("Membrane Q4 element self-test")
    print("-" * 40)

    dx, dy = 625.0, 500.0
    E, nu, t = 25000.0, 0.2, 200.0
    D_m = membrane_plane_stress(E, nu, t)
    Ke  = membrane_stiffness(dx, dy, D_m)

    print(f"D_m[0,0] = {D_m[0,0]:.4e}")
    print(f"Ke shape: {Ke.shape}")
    print(f"Ke[0,0]  = {Ke[0,0]:.4e}")
    sym_err = np.max(np.abs(Ke - Ke.T))
    print(f"Symmetry error: {sym_err:.2e}")

    # Cantilever beam: PL^3/(3EI) = 100000 * 3000^3 / (3 * 25000 * 200 * 3000^3 / 12)
    # = 100000 * 12 / (3 * 25000 * 200) = 1200000 / 15000000 = 0.08 mm
    L, H = 5000.0, 3000.0
    EI = E * t * H**3 / 12.0
    u_theory = 100000.0 * L**3 / (3.0 * EI)
    print(f"\nCantilever E-B theory: u_tip = {u_theory:.4f} mm")
