using System;
using System.Runtime.InteropServices;

namespace Calcpad.Core
{
    /// <summary>
    /// P/Invoke bindings for eigen_solver.dll
    /// Native C++ sparse solver using Eigen 3.4 (SimplicialLDLT, SparseLU)
    /// </summary>
    internal static class EigenInterop
    {
        private const string DllName = "eigen_solver";

        /// <summary>
        /// Solve symmetric system K*u = f using Eigen SimplicialLDLT.
        /// Input: skyline format (CalcpadCE HpSymmetricMatrix storage).
        /// Returns: 0 = LDLT success, 1 = LU fallback, -1/-2 = error
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int skyline_cholesky_solve(
            int n,
            [In] int[] rowSizes,
            [In] double[] values,
            [In] double[] rhs,
            [Out] double[] solution);

        /// <summary>
        /// Solve dense system A*x = b.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int dense_solve(
            int n,
            [In] double[] A_data,
            [In] double[] b_data,
            [Out] double[] x_data);

        /// <summary>
        /// Compute eigenvalues of dense symmetric matrix.
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int dense_eigenvalues(
            int n,
            [In] double[] A_data,
            [Out] double[] eigenvalues,
            [Out] double[] eigenvectors);

        /// <summary>
        /// Generalized eigenvalue problem K*phi = lambda*M*phi (skyline format).
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sparse_gen_eigen(
            int n,
            [In] int[] K_rowSizes, [In] double[] K_values,
            [In] int[] M_rowSizes, [In] double[] M_values,
            int numModes,
            [Out] double[] eigenvalues,
            [Out] double[] eigenvectors);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr eigen_solver_version();

        /// <summary>
        /// Get version string
        /// </summary>
        internal static string GetVersion()
        {
            var ptr = eigen_solver_version();
            return Marshal.PtrToStringAnsi(ptr) ?? "unknown";
        }

        /// <summary>
        /// Check if the native DLL is available
        /// </summary>
        internal static bool IsAvailable()
        {
            try
            {
                var ptr = eigen_solver_version();
                return ptr != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Extract skyline data from HpSymmetricMatrix for native solver.
        /// </summary>
        /// <summary>
        /// Extract skyline data from HpSymmetricMatrix for native solver.
        /// HpSymmetricMatrix stores upper triangle: row i has (n-i) values
        /// from column i to column n-1.
        /// </summary>
        internal static void ExtractSkyline(HpSymmetricMatrix matrix, out int[] rowSizes, out double[] values)
        {
            int n = matrix.RowCount;
            var rows = matrix.HpRows;
            rowSizes = new int[n];
            int totalValues = 0;

            for (int i = 0; i < n; i++)
            {
                int sz = n - i; // upper triangular: each row stores from diagonal to end
                rowSizes[i] = sz;
                totalValues += sz;
            }

            values = new double[totalValues];
            int offset = 0;
            for (int i = 0; i < n; i++)
            {
                int sz = rowSizes[i];
                for (int k = 0; k < sz; k++)
                {
                    values[offset + k] = matrix.GetValue(i, i + k);
                }
                offset += sz;
            }
        }
    }
}
