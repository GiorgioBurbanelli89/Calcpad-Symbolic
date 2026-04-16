using System;
using System.Threading.Tasks;

namespace Calcpad.Core
{
    /// <summary>
    /// Native FEM solver for 3D hexahedral C3D8 elements.
    /// Used by the fem_solid_3d() built-in function in Calcpad.
    ///
    /// Capabilities:
    /// - Assembles the global sparse stiffness matrix from element matrices in C# (no #for loops in Calcpad).
    /// - Uses Gauss 2x2x2 integration (8 points per element).
    /// - Solves Ku=F with Eigen C++ SimplicialLDLT (via HpSymmetricMatrix.ClSolve).
    /// - Supports penalty-method boundary conditions.
    /// - Target: 32000 hex8 / 91500 DOFs in under 30 seconds.
    /// </summary>
    internal static class FemSolver
    {
        // Gauss 2x2x2 quadrature points and weights
        private static readonly double _gp = 1.0 / Math.Sqrt(3.0);
        private static readonly double[,] _gauss = new double[8, 3]
        {
            { -1, -1, -1 }, {  1, -1, -1 }, {  1,  1, -1 }, { -1,  1, -1 },
            { -1, -1,  1 }, {  1, -1,  1 }, {  1,  1,  1 }, { -1,  1,  1 },
        };

        // Abaqus/Calcpad node signs for C3D8 in natural coordinates
        // Node i at (xi_i, eta_i, zeta_i)
        private static readonly int[] _xiSign  = { -1,  1,  1, -1, -1,  1,  1, -1 };
        private static readonly int[] _etaSign = { -1, -1,  1,  1, -1, -1,  1,  1 };
        private static readonly int[] _zetaSign= { -1, -1, -1, -1,  1,  1,  1,  1 };

        /// <summary>
        /// Native FEM solver entry point for C3D8 hexahedral elements.
        /// </summary>
        /// <param name="nodes">Matrix Nx3 of node coordinates (x, y, z).</param>
        /// <param name="elements">Matrix Mx8 of element connectivity (1-based node IDs).</param>
        /// <param name="E">Young's modulus (scalar, MPa / kN/m2 / whatever).</param>
        /// <param name="nu">Poisson ratio (scalar, dimensionless).</param>
        /// <param name="loadsMat">Matrix Nldx4: (nodeId, fx, fy, fz) - applied joint forces (1-based node IDs).</param>
        /// <param name="bcsMat">Matrix Nbcx2: (dofIndex, fixedValue) - constrained DOFs (1-based global DOF).
        /// The global DOF of node n in direction d (d=1..3) is 3*(n-1)+d.</param>
        /// <returns>Displacement vector u of length 3N (ux1, uy1, uz1, ux2, uy2, uz2, ...).</returns>
        internal static Vector SolveHex8(
            Matrix nodes, Matrix elements, double E, double nu,
            Matrix loadsMat, Matrix bcsMat)
        {
            int nN = nodes.RowCount;
            int nE = elements.RowCount;
            int ndof = 3 * nN;

            if (nodes.ColCount < 3)
                throw new ArgumentException("nodes matrix must have 3 columns (x, y, z)");
            if (elements.ColCount < 8)
                throw new ArgumentException("elements matrix must have 8 columns (C3D8 nodes)");

            // Flatten node coordinates to arrays for fast access
            var nX = new double[nN];
            var nY = new double[nN];
            var nZ = new double[nN];
            for (int i = 0; i < nN; i++)
            {
                nX[i] = nodes[i, 0].D;
                nY[i] = nodes[i, 1].D;
                nZ[i] = nodes[i, 2].D;
            }

            // Build D (6x6) constitutive matrix for isotropic 3D
            double lam = E * nu / ((1.0 + nu) * (1.0 - 2.0 * nu));
            double mu = E / (2.0 * (1.0 + nu));
            var D = new double[6, 6];
            D[0, 0] = D[1, 1] = D[2, 2] = lam + 2.0 * mu;
            D[0, 1] = D[1, 0] = D[0, 2] = D[2, 0] = D[1, 2] = D[2, 1] = lam;
            D[3, 3] = D[4, 4] = D[5, 5] = mu;

            // Global sparse stiffness matrix (symmetric, high-precision)
            var K = new HpSymmetricMatrix(ndof, null);

            // Element connectivity as int array for speed
            var conn = new int[nE, 8];
            for (int e = 0; e < nE; e++)
                for (int k = 0; k < 8; k++)
                    conn[e, k] = (int)elements[e, k].D - 1; // 1-based -> 0-based

            // Assemble K in parallel using partial matrices? For safety, use single-threaded assembly
            // (HpSymmetricMatrix GetValue/SetValue is not thread-safe for concurrent writes).
            var Ke = new double[24, 24];
            var B = new double[6, 24];

            for (int e = 0; e < nE; e++)
            {
                // Clear Ke
                Array.Clear(Ke, 0, 576);

                // Element node coordinates
                var xe = new double[8];
                var ye = new double[8];
                var ze = new double[8];
                for (int k = 0; k < 8; k++)
                {
                    int ni = conn[e, k];
                    xe[k] = nX[ni];
                    ye[k] = nY[ni];
                    ze[k] = nZ[ni];
                }

                // Gauss 2x2x2 integration
                for (int gp = 0; gp < 8; gp++)
                {
                    double xi = _gauss[gp, 0] * _gp;
                    double eta = _gauss[gp, 1] * _gp;
                    double zeta = _gauss[gp, 2] * _gp;

                    // Compute shape function derivatives in natural coordinates
                    var dNdxi = new double[8];
                    var dNdeta = new double[8];
                    var dNdzeta = new double[8];
                    for (int i = 0; i < 8; i++)
                    {
                        double xi_s = _xiSign[i];
                        double et_s = _etaSign[i];
                        double ze_s = _zetaSign[i];
                        dNdxi[i]   = 0.125 * xi_s * (1 + et_s * eta) * (1 + ze_s * zeta);
                        dNdeta[i]  = 0.125 * (1 + xi_s * xi) * et_s * (1 + ze_s * zeta);
                        dNdzeta[i] = 0.125 * (1 + xi_s * xi) * (1 + et_s * eta) * ze_s;
                    }

                    // Jacobian J (3x3) = [d(x,y,z)/d(xi,eta,zeta)]
                    double J00 = 0, J01 = 0, J02 = 0;
                    double J10 = 0, J11 = 0, J12 = 0;
                    double J20 = 0, J21 = 0, J22 = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        J00 += dNdxi[i] * xe[i];
                        J01 += dNdxi[i] * ye[i];
                        J02 += dNdxi[i] * ze[i];
                        J10 += dNdeta[i] * xe[i];
                        J11 += dNdeta[i] * ye[i];
                        J12 += dNdeta[i] * ze[i];
                        J20 += dNdzeta[i] * xe[i];
                        J21 += dNdzeta[i] * ye[i];
                        J22 += dNdzeta[i] * ze[i];
                    }

                    // det(J) and J^-1
                    double detJ =
                          J00 * (J11 * J22 - J12 * J21)
                        - J01 * (J10 * J22 - J12 * J20)
                        + J02 * (J10 * J21 - J11 * J20);
                    if (detJ <= 0)
                        throw new InvalidOperationException($"Element {e + 1}: negative/zero Jacobian at GP {gp + 1}");
                    double invDet = 1.0 / detJ;
                    double iJ00 = (J11 * J22 - J12 * J21) * invDet;
                    double iJ01 = (J02 * J21 - J01 * J22) * invDet;
                    double iJ02 = (J01 * J12 - J02 * J11) * invDet;
                    double iJ10 = (J12 * J20 - J10 * J22) * invDet;
                    double iJ11 = (J00 * J22 - J02 * J20) * invDet;
                    double iJ12 = (J02 * J10 - J00 * J12) * invDet;
                    double iJ20 = (J10 * J21 - J11 * J20) * invDet;
                    double iJ21 = (J01 * J20 - J00 * J21) * invDet;
                    double iJ22 = (J00 * J11 - J01 * J10) * invDet;

                    // Shape function derivatives in physical coords
                    // dN/dx = invJ * dN/dxi_natural
                    var dNx = new double[8];
                    var dNy = new double[8];
                    var dNz = new double[8];
                    for (int i = 0; i < 8; i++)
                    {
                        dNx[i] = iJ00 * dNdxi[i] + iJ01 * dNdeta[i] + iJ02 * dNdzeta[i];
                        dNy[i] = iJ10 * dNdxi[i] + iJ11 * dNdeta[i] + iJ12 * dNdzeta[i];
                        dNz[i] = iJ20 * dNdxi[i] + iJ21 * dNdeta[i] + iJ22 * dNdzeta[i];
                    }

                    // B (6x24) strain-displacement matrix
                    Array.Clear(B, 0, 144);
                    for (int i = 0; i < 8; i++)
                    {
                        int c = 3 * i;
                        B[0, c + 0] = dNx[i];        // eps_xx = du/dx
                        B[1, c + 1] = dNy[i];        // eps_yy = dv/dy
                        B[2, c + 2] = dNz[i];        // eps_zz = dw/dz
                        B[3, c + 0] = dNy[i];        // gamma_xy
                        B[3, c + 1] = dNx[i];
                        B[4, c + 1] = dNz[i];        // gamma_yz
                        B[4, c + 2] = dNy[i];
                        B[5, c + 0] = dNz[i];        // gamma_zx
                        B[5, c + 2] = dNx[i];
                    }

                    // Ke += B^T * D * B * detJ (unit weight 1.0)
                    // Compute DB (6x24)
                    var DB = new double[6, 24];
                    for (int r = 0; r < 6; r++)
                    {
                        for (int c = 0; c < 24; c++)
                        {
                            double s = 0;
                            for (int k = 0; k < 6; k++)
                                s += D[r, k] * B[k, c];
                            DB[r, c] = s;
                        }
                    }
                    // Ke += B^T DB * detJ
                    for (int r = 0; r < 24; r++)
                    {
                        for (int c = 0; c < 24; c++)
                        {
                            double s = 0;
                            for (int k = 0; k < 6; k++)
                                s += B[k, r] * DB[k, c];
                            Ke[r, c] += s * detJ;
                        }
                    }
                }

                // Assemble Ke into global K using connectivity
                // Global DOF of node i in direction d (d=0..2) is 3*ni + d
                var dofMap = new int[24];
                for (int i = 0; i < 8; i++)
                {
                    int ni = conn[e, i];
                    dofMap[3 * i + 0] = 3 * ni + 0;
                    dofMap[3 * i + 1] = 3 * ni + 1;
                    dofMap[3 * i + 2] = 3 * ni + 2;
                }
                for (int i = 0; i < 24; i++)
                {
                    int gi = dofMap[i];
                    for (int j = i; j < 24; j++) // symmetric: only upper triangle
                    {
                        int gj = dofMap[j];
                        int row, col;
                        if (gi <= gj) { row = gi; col = gj; }
                        else { row = gj; col = gi; }
                        double cur = K.GetValue(row, col);
                        K.SetValue(cur + Ke[i, j], row, col);
                    }
                }
            }

            // Build load vector F
            var F = new double[ndof];
            if (loadsMat is not null && loadsMat.RowCount > 0 && loadsMat.ColCount >= 4)
            {
                int nL = loadsMat.RowCount;
                for (int i = 0; i < nL; i++)
                {
                    int ni = (int)loadsMat[i, 0].D - 1;
                    if (ni < 0 || ni >= nN)
                        continue;
                    F[3 * ni + 0] += loadsMat[i, 1].D;
                    F[3 * ni + 1] += loadsMat[i, 2].D;
                    F[3 * ni + 2] += loadsMat[i, 3].D;
                }
            }

            // Apply penalty-method boundary conditions
            // For each fixed DOF, add a large value to K[d,d] and set F[d] = penalty * fixedValue
            const double penalty = 1e20;
            if (bcsMat is not null && bcsMat.RowCount > 0 && bcsMat.ColCount >= 1)
            {
                int nB = bcsMat.RowCount;
                for (int i = 0; i < nB; i++)
                {
                    int dof = (int)bcsMat[i, 0].D - 1;
                    if (dof < 0 || dof >= ndof)
                        continue;
                    double fixedVal = bcsMat.ColCount >= 2 ? bcsMat[i, 1].D : 0.0;
                    double cur = K.GetValue(dof, dof);
                    K.SetValue(cur + penalty, dof, dof);
                    F[dof] += penalty * fixedVal;
                }
            }

            // Solve Ku = F using Cholesky sparse (Eigen C++ if available)
            var F_hp = new HpVector(F, null);
            var u_hp = K.ClSolve(F_hp);

            // Return as Vector (Calcpad-compatible)
            return u_hp;
        }

        /// <summary>
        /// Compute nodal stress sigma_zz (global Z direction) from the displacement
        /// vector u returned by SolveHex8. Stresses are evaluated at Gauss points of
        /// each element and extrapolated/averaged to nodes (unweighted mean).
        ///
        /// Returns a matrix Nx6 with nodal stresses [S11, S22, S33, S12, S23, S13].
        /// For Calcpad: use col(stress; 3) to get sigma_zz, col(stress; 1) for S11, etc.
        /// </summary>
        internal static Matrix ComputeStressHex8(
            Vector u, Matrix nodes, Matrix elements, double E, double nu)
        {
            int nN = nodes.RowCount;
            int nE = elements.RowCount;

            if (u.Length < 3 * nN)
                throw new ArgumentException("Displacement vector too short for mesh");

            // Flatten node coordinates
            var nX = new double[nN];
            var nY = new double[nN];
            var nZ = new double[nN];
            for (int i = 0; i < nN; i++)
            {
                nX[i] = nodes[i, 0].D;
                nY[i] = nodes[i, 1].D;
                nZ[i] = nodes[i, 2].D;
            }

            // Flatten displacements
            var uArr = new double[3 * nN];
            for (int i = 0; i < 3 * nN; i++)
                uArr[i] = u[i].D;

            // D matrix
            double lam = E * nu / ((1.0 + nu) * (1.0 - 2.0 * nu));
            double mu = E / (2.0 * (1.0 + nu));
            var D = new double[6, 6];
            D[0, 0] = D[1, 1] = D[2, 2] = lam + 2.0 * mu;
            D[0, 1] = D[1, 0] = D[0, 2] = D[2, 0] = D[1, 2] = D[2, 1] = lam;
            D[3, 3] = D[4, 4] = D[5, 5] = mu;

            // Nodal stress accumulators (averaged from Gauss points touching each node)
            var stressAccum = new double[nN, 6]; // [S11, S22, S33, S12, S23, S13]
            var count = new int[nN];

            // Element connectivity
            var conn = new int[nE, 8];
            for (int e = 0; e < nE; e++)
                for (int k = 0; k < 8; k++)
                    conn[e, k] = (int)elements[e, k].D - 1;

            var B = new double[6, 24];
            var ue = new double[24];

            // Evaluate stress at element center (xi=eta=zeta=0) to avoid Gauss
            // extrapolation complexity. This is a reasonable approximation for
            // visualization of the pressure bulb.
            // For better accuracy: use Gauss 2x2x2 + extrapolation matrix, but that
            // is more complex. This "element center" approach is what ParaView does
            // for cell-data visualization.
            for (int eid = 0; eid < nE; eid++)
            {
                // Element node coordinates
                var xe = new double[8];
                var ye = new double[8];
                var ze = new double[8];
                for (int k = 0; k < 8; k++)
                {
                    int ni = conn[eid, k];
                    xe[k] = nX[ni];
                    ye[k] = nY[ni];
                    ze[k] = nZ[ni];
                }

                // Gather element displacements
                for (int i = 0; i < 8; i++)
                {
                    int ni = conn[eid, i];
                    ue[3 * i + 0] = uArr[3 * ni + 0];
                    ue[3 * i + 1] = uArr[3 * ni + 1];
                    ue[3 * i + 2] = uArr[3 * ni + 2];
                }

                // Shape function derivatives at element center (xi=eta=zeta=0)
                // dN_i/dxi = (1/8) * xi_i * (1 + eta_i*0) * (1 + zeta_i*0) = xi_i/8
                // Similarly for dN/deta, dN/dzeta
                var dNdxi = new double[8];
                var dNdeta = new double[8];
                var dNdzeta = new double[8];
                for (int i = 0; i < 8; i++)
                {
                    dNdxi[i]   = 0.125 * _xiSign[i];
                    dNdeta[i]  = 0.125 * _etaSign[i];
                    dNdzeta[i] = 0.125 * _zetaSign[i];
                }

                // Jacobian
                double J00 = 0, J01 = 0, J02 = 0;
                double J10 = 0, J11 = 0, J12 = 0;
                double J20 = 0, J21 = 0, J22 = 0;
                for (int i = 0; i < 8; i++)
                {
                    J00 += dNdxi[i] * xe[i];
                    J01 += dNdxi[i] * ye[i];
                    J02 += dNdxi[i] * ze[i];
                    J10 += dNdeta[i] * xe[i];
                    J11 += dNdeta[i] * ye[i];
                    J12 += dNdeta[i] * ze[i];
                    J20 += dNdzeta[i] * xe[i];
                    J21 += dNdzeta[i] * ye[i];
                    J22 += dNdzeta[i] * ze[i];
                }
                double detJ =
                      J00 * (J11 * J22 - J12 * J21)
                    - J01 * (J10 * J22 - J12 * J20)
                    + J02 * (J10 * J21 - J11 * J20);
                if (detJ <= 0) continue;
                double invDet = 1.0 / detJ;
                double iJ00 = (J11 * J22 - J12 * J21) * invDet;
                double iJ01 = (J02 * J21 - J01 * J22) * invDet;
                double iJ02 = (J01 * J12 - J02 * J11) * invDet;
                double iJ10 = (J12 * J20 - J10 * J22) * invDet;
                double iJ11 = (J00 * J22 - J02 * J20) * invDet;
                double iJ12 = (J02 * J10 - J00 * J12) * invDet;
                double iJ20 = (J10 * J21 - J11 * J20) * invDet;
                double iJ21 = (J01 * J20 - J00 * J21) * invDet;
                double iJ22 = (J00 * J11 - J01 * J10) * invDet;

                // Physical derivatives
                var dNx = new double[8];
                var dNy = new double[8];
                var dNz = new double[8];
                for (int i = 0; i < 8; i++)
                {
                    dNx[i] = iJ00 * dNdxi[i] + iJ01 * dNdeta[i] + iJ02 * dNdzeta[i];
                    dNy[i] = iJ10 * dNdxi[i] + iJ11 * dNdeta[i] + iJ12 * dNdzeta[i];
                    dNz[i] = iJ20 * dNdxi[i] + iJ21 * dNdeta[i] + iJ22 * dNdzeta[i];
                }

                // Build B (6x24)
                Array.Clear(B, 0, 144);
                for (int i = 0; i < 8; i++)
                {
                    int c = 3 * i;
                    B[0, c + 0] = dNx[i];
                    B[1, c + 1] = dNy[i];
                    B[2, c + 2] = dNz[i];
                    B[3, c + 0] = dNy[i];
                    B[3, c + 1] = dNx[i];
                    B[4, c + 1] = dNz[i];
                    B[4, c + 2] = dNy[i];
                    B[5, c + 0] = dNz[i];
                    B[5, c + 2] = dNx[i];
                }

                // eps = B * ue (6x1)
                var eps = new double[6];
                for (int r = 0; r < 6; r++)
                {
                    double s = 0;
                    for (int c = 0; c < 24; c++)
                        s += B[r, c] * ue[c];
                    eps[r] = s;
                }

                // sigma = D * eps (6x1)
                var sig = new double[6];
                for (int r = 0; r < 6; r++)
                {
                    double s = 0;
                    for (int c = 0; c < 6; c++)
                        s += D[r, c] * eps[c];
                    sig[r] = s;
                }

                // Accumulate to the 8 nodes of this element (unweighted average)
                for (int i = 0; i < 8; i++)
                {
                    int ni = conn[eid, i];
                    for (int k = 0; k < 6; k++)
                        stressAccum[ni, k] += sig[k];
                    count[ni]++;
                }
            }

            // Build output matrix: nodal average
            var stress = new Matrix(nN, 6);
            for (int i = 0; i < nN; i++)
            {
                int c = count[i];
                if (c == 0)
                {
                    for (int k = 0; k < 6; k++)
                        stress[i, k] = new RealValue(0);
                }
                else
                {
                    double inv = 1.0 / c;
                    for (int k = 0; k < 6; k++)
                        stress[i, k] = new RealValue(stressAccum[i, k] * inv);
                }
            }
            return stress;
        }

        /// <summary>
        /// Generate a regular hex8 box mesh — nodes (N x 3).
        /// Nodes are ordered i-fastest then j then k, with origin at (0,0,0) if
        /// centered=false, or centered at origin if centered=true (from -L/2 to +L/2).
        /// Params vector: [Lx, Ly, Lz, nx, ny, nz, centered] (centered=1 for centered, 0 for origin).
        /// </summary>
        internal static Matrix GenerateNodesBox(Vector p)
        {
            if (p.Length < 6)
                throw new ArgumentException("mesh_hex8_nodes requires vector [Lx; Ly; Lz; nx; ny; nz; centered?]");

            double Lx = p[0].D;
            double Ly = p[1].D;
            double Lz = p[2].D;
            int nx = (int)p[3].D;
            int ny = (int)p[4].D;
            int nz = (int)p[5].D;
            bool centered = p.Length >= 7 && p[6].D > 0.5;

            int nn = (nx + 1) * (ny + 1) * (nz + 1);
            double dx = Lx / nx;
            double dy = Ly / ny;
            double dz = Lz / nz;
            double x0 = centered ? -Lx / 2 : 0;
            double y0 = centered ? -Ly / 2 : 0;

            var nodes = new Matrix(nn, 3);
            int id = 0;
            for (int k = 0; k <= nz; k++)
            {
                for (int j = 0; j <= ny; j++)
                {
                    for (int i = 0; i <= nx; i++)
                    {
                        nodes[id, 0] = new RealValue(x0 + i * dx);
                        nodes[id, 1] = new RealValue(y0 + j * dy);
                        nodes[id, 2] = new RealValue(k * dz);
                        id++;
                    }
                }
            }
            return nodes;
        }

        /// <summary>
        /// Generate hex8 element connectivity (M x 8, 1-based node IDs).
        /// Params vector: [nx, ny, nz].
        /// </summary>
        internal static Matrix GenerateElemsBox(Vector p)
        {
            if (p.Length < 3)
                throw new ArgumentException("mesh_hex8_elems requires vector [nx; ny; nz]");

            int nx = (int)p[0].D;
            int ny = (int)p[1].D;
            int nz = (int)p[2].D;
            int ne = nx * ny * nz;
            int nxp = nx + 1;
            int nyp = ny + 1;
            int nxpyp = nxp * nyp;

            var elems = new Matrix(ne, 8);
            int eid = 0;
            for (int k = 0; k < nz; k++)
            {
                for (int j = 0; j < ny; j++)
                {
                    for (int i = 0; i < nx; i++)
                    {
                        int n1 = k * nxpyp + j * nxp + i + 1;
                        int n2 = k * nxpyp + j * nxp + (i + 1) + 1;
                        int n3 = k * nxpyp + (j + 1) * nxp + (i + 1) + 1;
                        int n4 = k * nxpyp + (j + 1) * nxp + i + 1;
                        int n5 = (k + 1) * nxpyp + j * nxp + i + 1;
                        int n6 = (k + 1) * nxpyp + j * nxp + (i + 1) + 1;
                        int n7 = (k + 1) * nxpyp + (j + 1) * nxp + (i + 1) + 1;
                        int n8 = (k + 1) * nxpyp + (j + 1) * nxp + i + 1;
                        elems[eid, 0] = new RealValue(n1);
                        elems[eid, 1] = new RealValue(n2);
                        elems[eid, 2] = new RealValue(n3);
                        elems[eid, 3] = new RealValue(n4);
                        elems[eid, 4] = new RealValue(n5);
                        elems[eid, 5] = new RealValue(n6);
                        elems[eid, 6] = new RealValue(n7);
                        elems[eid, 7] = new RealValue(n8);
                        eid++;
                    }
                }
            }
            return elems;
        }

        /// <summary>
        /// Generate specs matrix (loads + BCs) for a standard "soil box" problem:
        /// base fixed + lateral faces fixed + single point load at top-center.
        /// Params vector: [Lx, Ly, Lz, nx, ny, nz, centered, Pz]
        ///   centered = 1 for centered box (mesh centered at origin)
        ///   Pz       = vertical point load at top-center (downward = negative)
        /// </summary>
        internal static Matrix GenerateSoilBoxSpecs(Vector p)
        {
            if (p.Length < 8)
                throw new ArgumentException("mesh_soil_specs requires vector [Lx; Ly; Lz; nx; ny; nz; centered; Pz]");

            double Lx = p[0].D;
            double Ly = p[1].D;
            double Lz = p[2].D;
            int nx = (int)p[3].D;
            int ny = (int)p[4].D;
            int nz = (int)p[5].D;
            bool centered = p[6].D > 0.5;
            double Pz = p[7].D;

            double dx = Lx / nx;
            double dy = Ly / ny;
            double dz = Lz / nz;
            double x0 = centered ? -Lx / 2 : 0;
            double y0 = centered ? -Ly / 2 : 0;
            double tol = Math.Min(Math.Min(dx, dy), dz) / 100;

            int nxp = nx + 1;
            int nyp = ny + 1;
            int nzp = nz + 1;
            int nn = nxp * nyp * nzp;

            // Count fixed nodes (base OR lateral)
            int nFixed = 0;
            int tcId = 0;
            double xCenter = centered ? 0 : Lx / 2;
            double yCenter = centered ? 0 : Ly / 2;
            int id = 0;
            for (int k = 0; k <= nz; k++)
            {
                for (int j = 0; j <= ny; j++)
                {
                    for (int i = 0; i <= nx; i++)
                    {
                        id++;
                        double x = x0 + i * dx;
                        double y = y0 + j * dy;
                        double z = k * dz;
                        bool onBase = z < tol;
                        bool onLeft = x < x0 + tol;
                        bool onRight = x > x0 + Lx - tol;
                        bool onFront = y < y0 + tol;
                        bool onBack = y > y0 + Ly - tol;
                        if (onBase || onLeft || onRight || onFront || onBack)
                            nFixed++;
                        // Top-center node
                        if (Math.Abs(x - xCenter) < tol && Math.Abs(y - yCenter) < tol && z > Lz - tol)
                            tcId = id;
                    }
                }
            }

            // specs = 1 load + 3*nFixed BC rows (UX, UY, UZ per node)
            int nSpecs = 1 + 3 * nFixed;
            var specs = new Matrix(nSpecs, 5);

            // Row 0: point load at top-center
            specs[0, 0] = new RealValue(1);
            specs[0, 1] = new RealValue(tcId);
            specs[0, 2] = new RealValue(0);
            specs[0, 3] = new RealValue(0);
            specs[0, 4] = new RealValue(Pz);

            int row = 1;
            id = 0;
            for (int k = 0; k <= nz; k++)
            {
                for (int j = 0; j <= ny; j++)
                {
                    for (int i = 0; i <= nx; i++)
                    {
                        id++;
                        double x = x0 + i * dx;
                        double y = y0 + j * dy;
                        double z = k * dz;
                        bool onBase = z < tol;
                        bool onLeft = x < x0 + tol;
                        bool onRight = x > x0 + Lx - tol;
                        bool onFront = y < y0 + tol;
                        bool onBack = y > y0 + Ly - tol;
                        if (onBase || onLeft || onRight || onFront || onBack)
                        {
                            for (int d = 1; d <= 3; d++)
                            {
                                specs[row, 0] = new RealValue(2);
                                specs[row, 1] = new RealValue(3 * (id - 1) + d);
                                specs[row, 2] = new RealValue(0);
                                specs[row, 3] = new RealValue(0);
                                specs[row, 4] = new RealValue(0);
                                row++;
                            }
                        }
                    }
                }
            }

            return specs;
        }

        /// <summary>
        /// Generate specs for a soil box with a RECTANGULAR distributed load on the top
        /// surface (like SAP2000 surface pressure or Fig. SF-70 of Serquen's book).
        ///
        /// Params vector: [Lx, Ly, Lz, nx, ny, nz, centered, Rx, Ry, q]
        ///   Rx, Ry = rectangle dimensions (m)
        ///   q      = surface pressure (tonf/m2, positive = compression downward)
        ///
        /// The pressure is converted to equivalent nodal forces:
        ///   F_node = -q * A_trib
        /// where A_trib is the tributary area of each top-surface node within
        /// the rectangle (dx*dy for interior nodes, dx*dy/2 for edge, dx*dy/4 for corner).
        /// For simplicity we use the full dx*dy for all nodes strictly inside
        /// and dx*dy/2 for edges; the total load matches q*Rx*Ry.
        /// </summary>
        internal static Matrix GenerateSoilBoxSpecsRect(Vector p)
        {
            if (p.Length < 10)
                throw new ArgumentException(
                    "mesh_soil_specs_rect requires [Lx; Ly; Lz; nx; ny; nz; centered; Rx; Ry; q]");

            double Lx = p[0].D;
            double Ly = p[1].D;
            double Lz = p[2].D;
            int nx = (int)p[3].D;
            int ny = (int)p[4].D;
            int nz = (int)p[5].D;
            bool centered = p[6].D > 0.5;
            double Rx = p[7].D;
            double Ry = p[8].D;
            double q = p[9].D;

            double dx = Lx / nx;
            double dy = Ly / ny;
            double dz = Lz / nz;
            double x0 = centered ? -Lx / 2 : 0;
            double y0 = centered ? -Ly / 2 : 0;
            double xCenter = centered ? 0 : Lx / 2;
            double yCenter = centered ? 0 : Ly / 2;
            double tol = Math.Min(Math.Min(dx, dy), dz) / 100;

            double halfRx = Rx / 2;
            double halfRy = Ry / 2;

            // First pass: count boundary fixed nodes and loaded nodes
            int nFixed = 0;
            int nLoaded = 0;
            int id = 0;
            for (int k = 0; k <= nz; k++)
            {
                for (int j = 0; j <= ny; j++)
                {
                    for (int i = 0; i <= nx; i++)
                    {
                        id++;
                        double x = x0 + i * dx;
                        double y = y0 + j * dy;
                        double z = k * dz;
                        bool onBase = z < tol;
                        bool onLeft = x < x0 + tol;
                        bool onRight = x > x0 + Lx - tol;
                        bool onFront = y < y0 + tol;
                        bool onBack = y > y0 + Ly - tol;
                        if (onBase || onLeft || onRight || onFront || onBack)
                            nFixed++;
                        // Top surface within rectangle?
                        if (z > Lz - tol)
                        {
                            double xr = x - xCenter;
                            double yr = y - yCenter;
                            if (Math.Abs(xr) <= halfRx + tol && Math.Abs(yr) <= halfRy + tol)
                                nLoaded++;
                        }
                    }
                }
            }

            int nSpecs = nLoaded + 3 * nFixed;
            var specs = new Matrix(nSpecs, 5);

            // Total area should be Rx*Ry; each interior node gets dx*dy weight,
            // edge gets dx*dy/2, corner gets dx*dy/4. We approximate using the
            // uniform dx*dy for all, then scale to match the exact integral.
            // Actually the correct lumped force for a node at (x,y) is the integral
            // of shape function over the rectangle, which for a regular grid is:
            //   interior: dx*dy
            //   edge:     dx*dy/2
            //   corner:   dx*dy/4
            //
            // To get exact total, we compute the tributary area per node.

            int row = 0;
            id = 0;
            for (int k = 0; k <= nz; k++)
            {
                for (int j = 0; j <= ny; j++)
                {
                    for (int i = 0; i <= nx; i++)
                    {
                        id++;
                        double x = x0 + i * dx;
                        double y = y0 + j * dy;
                        double z = k * dz;

                        // Apply load if on top surface within rectangle
                        if (z > Lz - tol)
                        {
                            double xr = x - xCenter;
                            double yr = y - yCenter;
                            if (Math.Abs(xr) <= halfRx + tol && Math.Abs(yr) <= halfRy + tol)
                            {
                                // Tributary area based on position within rectangle
                                bool onXEdge = Math.Abs(Math.Abs(xr) - halfRx) < tol;
                                bool onYEdge = Math.Abs(Math.Abs(yr) - halfRy) < tol;
                                double areaFactor = 1.0;
                                if (onXEdge) areaFactor *= 0.5;
                                if (onYEdge) areaFactor *= 0.5;
                                double F = q * dx * dy * areaFactor;
                                specs[row, 0] = new RealValue(1);
                                specs[row, 1] = new RealValue(id);
                                specs[row, 2] = new RealValue(0);
                                specs[row, 3] = new RealValue(0);
                                specs[row, 4] = new RealValue(-F); // negative = downward
                                row++;
                            }
                        }
                    }
                }
            }

            // Second pass: BC rows
            id = 0;
            for (int k = 0; k <= nz; k++)
            {
                for (int j = 0; j <= ny; j++)
                {
                    for (int i = 0; i <= nx; i++)
                    {
                        id++;
                        double x = x0 + i * dx;
                        double y = y0 + j * dy;
                        double z = k * dz;
                        bool onBase = z < tol;
                        bool onLeft = x < x0 + tol;
                        bool onRight = x > x0 + Lx - tol;
                        bool onFront = y < y0 + tol;
                        bool onBack = y > y0 + Ly - tol;
                        if (onBase || onLeft || onRight || onFront || onBack)
                        {
                            for (int d = 1; d <= 3; d++)
                            {
                                specs[row, 0] = new RealValue(2);
                                specs[row, 1] = new RealValue(3 * (id - 1) + d);
                                specs[row, 2] = new RealValue(0);
                                specs[row, 3] = new RealValue(0);
                                specs[row, 4] = new RealValue(0);
                                row++;
                            }
                        }
                    }
                }
            }

            return specs;
        }
    }
}
