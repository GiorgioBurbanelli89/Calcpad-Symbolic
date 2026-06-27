// =============================================================================
// Calcpad Lab — BLAS/LAPACK via interfaz C (CBLAS + LAPACKE): Intel MKL ↔ OpenBLAS
// =============================================================================
//   El motor numerico carga UNA libreria nativa, elegida por PREFERENCIA:
//       1) Intel oneMKL  (mkl_rt.dll / mkl_rt.2.dll)  ← mas rapido, LEGAL
//       2) OpenBLAS      (libopenblas.dll)            ← fallback (va en el bin)
//   Si no hay ninguna, cae al loop managed (correcto, mas lento).
//
//   POR QUE LA INTERFAZ C (cblas_*/LAPACKE_*) Y NO LA FORTRAN:
//   - Enteros de 32 bits (lapack_int) → inmune al crash ILP64 de la mkl.dll de
//     MATLAB (que lee n/lda/ipiv como qword de 64 bits; ver memoria
//     reference_mkl_vs_openblas_lab). El redistribuible oneMKL `mkl_rt` y
//     OpenBLAS exportan LAPACKE en LP64 (int 32-bit).
//   - Mismos nombres EXACTOS en ambas libs (cblas_dgemm, LAPACKE_dgesv…),
//     sin la ambiguedad de decoracion Fortran (DGEMM vs dgemm_).
//   - Soportan row-major nativo (CblasRowMajor=101) → sin el truco de transpuesta.
//
//   oneMKL real ya presente en la maquina del usuario: el redistribuible que
//   trae FreeCAD (C:\Program Files\FreeCAD 1.0\bin\mkl_rt.2.dll). NO se usa la
//   mkl.dll de MATLAB (licencia + ILP64). Mismo enfoque que Hekatan.Core/MklInterop.
// =============================================================================
using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Calcpad.Core
{
    /// <summary>Loader nativo compartido (CBLAS + LAPACKE). Prefiere Intel oneMKL,
    /// cae a OpenBLAS, y si no hay nada deja todo en managed.</summary>
    internal static unsafe class NativeBlas
    {
        // Constantes de la interfaz C
        public const int CblasRowMajor = 101, CblasColMajor = 102;
        public const int CblasNoTrans = 111, CblasTrans = 112;
        public const int LapackRowMajor = 101, LapackColMajor = 102;

        /// <summary>"Intel MKL", "OpenBLAS" o "managed".</summary>
        public static string Provider { get; private set; } = "managed";
        /// <summary>Ruta de la libreria cargada (diagnostico).</summary>
        public static string LoadedFrom { get; private set; } = "";
        public static IntPtr Handle { get; private set; } = IntPtr.Zero;

        // ---- Punteros a las rutinas C (cdecl) ----
        // cblas_dgemm(layout, transA, transB, M, N, K, alpha, A, lda, B, ldb, beta, C, ldc)
        public static delegate* unmanaged[Cdecl]<int, int, int, int, int, int, double, double*, int, double*, int, double, double*, int, void> Dgemm;
        // cblas_dgemv(layout, transA, M, N, alpha, A, lda, X, incX, beta, Y, incY)
        public static delegate* unmanaged[Cdecl]<int, int, int, int, double, double*, int, double*, int, double, double*, int, void> Dgemv;
        // cblas_daxpy(N, alpha, X, incX, Y, incY)
        public static delegate* unmanaged[Cdecl]<int, double, double*, int, double*, int, void> Daxpy;
        // cblas_ddot(N, X, incX, Y, incY) -> double
        public static delegate* unmanaged[Cdecl]<int, double*, int, double*, int, double> Ddot;
        // LAPACKE_dgesv(layout, n, nrhs, A, lda, ipiv, B, ldb) -> info
        public static delegate* unmanaged[Cdecl]<int, int, int, double*, int, int*, double*, int, int> Dgesv;
        // LAPACKE_dpbsv(layout, uplo, n, kd, nrhs, AB, ldab, B, ldb) -> info
        public static delegate* unmanaged[Cdecl]<int, sbyte, int, int, int, double*, int, double*, int, int> Dpbsv;
        // LAPACKE_dsygv(layout, itype, jobz, uplo, n, A, lda, B, ldb, w) -> info  (LAPACKE gestiona el workspace)
        public static delegate* unmanaged[Cdecl]<int, int, sbyte, sbyte, int, double*, int, double*, int, double*, int> Dsygv;
        // MKL PARDISO (sparse directo, el solver de OpenSees). Solo existe en mkl_rt.
        // pardiso(pt, maxfct, mnum, mtype, phase, n, a, ia, ja, perm, nrhs, iparm, msglvl, b, x, error)
        public static delegate* unmanaged[Cdecl]<void*, int*, int*, int*, int*, int*, double*, int*, int*, int*, int*, int*, int*, double*, double*, int*, void> Pardiso;
        // pardisoinit(pt, mtype, iparm)
        public static delegate* unmanaged[Cdecl]<void*, int*, int*, void> Pardisoinit;

        public static bool HasBlas => Dgemm != null && Dgemv != null && Daxpy != null && Ddot != null;
        public static bool HasLapack => Dgesv != null && Dpbsv != null && Dsygv != null;
        public static bool HasPardiso => Pardiso != null && Pardisoinit != null;

        static NativeBlas()
        {
            var asm = typeof(NativeBlas).Assembly;

            // Override opcional: HEKATAN_BLAS = mkl | openblas | managed | off
            var force = (Environment.GetEnvironmentVariable("HEKATAN_BLAS") ?? "").Trim().ToLowerInvariant();
            if (force is "managed" or "off" or "none") return;     // todo managed
            bool allowMkl = force is not ("openblas" or "blas");   // openblas → saltar MKL

            // 1) Intel oneMKL real (mkl_rt*.dll) en directorios candidatos.
            //    mkl_rt carga sus satelites (mkl_avx2/mkl_core/libiomp5md…) desde
            //    su propia carpeta → prependemos esa carpeta al PATH.
            string[] mklDirs =
            {
                Environment.GetEnvironmentVariable("HEKATAN_MKL_DIR"),   // override explicito
                Path.Combine(AppContext.BaseDirectory, "mkl"),           // bundle oneMKL (bin\mkl\) ← preferido
                AppContext.BaseDirectory,                                 // junto al ejecutable
                @"C:\Program Files\FreeCAD 1.0\bin",                     // fallback: oneMKL de FreeCAD
            };
            foreach (var dir in allowMkl ? mklDirs : Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                string[] hits;
                try { hits = Directory.GetFiles(dir, "mkl_rt*.dll"); }
                catch { continue; }
                if (hits.Length == 0) continue;
                try
                {
                    // Anexar (no anteponer) el dir de oneMKL al PATH: sus satelites
                    // (mkl_avx2.2/mkl_core.2/libiomp5md…) tienen nombres unicos → se
                    // hallan igual, pero las DLLs propias de la app ganan en colision.
                    Environment.SetEnvironmentVariable("PATH",
                        Environment.GetEnvironmentVariable("PATH") + Path.PathSeparator + dir);
                    if (NativeLibrary.TryLoad(hits[0], out var h) && Bind(h))
                    { Handle = h; Provider = "Intel MKL"; LoadedFrom = hits[0]; return; }
                }
                catch { /* probar siguiente */ }
            }

            // 2) OpenBLAS (en el bin de la app)
            const DllImportSearchPath search =
                DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories |
                DllImportSearchPath.ApplicationDirectory;
            foreach (var name in new[] { "libopenblas", "openblas" })
            {
                try
                {
                    if (NativeLibrary.TryLoad(name, asm, search, out var h) && Bind(h))
                    { Handle = h; Provider = "OpenBLAS"; LoadedFrom = name; return; }
                }
                catch { /* probar siguiente */ }
            }
            // 3) nada → managed
        }

        private static bool Bind(IntPtr h)
        {
            if (!NativeLibrary.TryGetExport(h, "cblas_dgemm", out var dgemm)) return false;
            Dgemm = (delegate* unmanaged[Cdecl]<int, int, int, int, int, int, double, double*, int, double*, int, double, double*, int, void>)dgemm;
            if (NativeLibrary.TryGetExport(h, "cblas_dgemv", out var p)) Dgemv = (delegate* unmanaged[Cdecl]<int, int, int, int, double, double*, int, double*, int, double, double*, int, void>)p;
            if (NativeLibrary.TryGetExport(h, "cblas_daxpy", out p)) Daxpy = (delegate* unmanaged[Cdecl]<int, double, double*, int, double*, int, void>)p;
            if (NativeLibrary.TryGetExport(h, "cblas_ddot", out p)) Ddot = (delegate* unmanaged[Cdecl]<int, double*, int, double*, int, double>)p;
            if (NativeLibrary.TryGetExport(h, "LAPACKE_dgesv", out p)) Dgesv = (delegate* unmanaged[Cdecl]<int, int, int, double*, int, int*, double*, int, int>)p;
            if (NativeLibrary.TryGetExport(h, "LAPACKE_dpbsv", out p)) Dpbsv = (delegate* unmanaged[Cdecl]<int, sbyte, int, int, int, double*, int, double*, int, int>)p;
            if (NativeLibrary.TryGetExport(h, "LAPACKE_dsygv", out p)) Dsygv = (delegate* unmanaged[Cdecl]<int, int, sbyte, sbyte, int, double*, int, double*, int, double*, int>)p;
            // PARDISO (solo MKL; OpenBLAS no lo exporta → quedan null)
            if (NativeLibrary.TryGetExport(h, "pardiso", out p)) Pardiso = (delegate* unmanaged[Cdecl]<void*, int*, int*, int*, int*, int*, double*, int*, int*, int*, int*, int*, int*, double*, double*, int*, void>)p;
            if (NativeLibrary.TryGetExport(h, "pardisoinit", out p)) Pardisoinit = (delegate* unmanaged[Cdecl]<void*, int*, int*, void>)p;
            return true;
        }
    }

    public static unsafe class BlasInterop
    {
        /// <summary>Threshold por debajo del cual usamos loop naive (overhead BLAS > compute).</summary>
        public const int BlasThreshold = 32;

        /// <summary>Proveedor BLAS activo: "Intel MKL", "OpenBLAS" o "managed".</summary>
        public static string Provider => NativeBlas.Provider;
        /// <summary>Ruta de la libreria nativa cargada (diagnostico).</summary>
        public static string LoadedFrom => NativeBlas.LoadedFrom;

        /// <summary>True si hay una libreria BLAS nativa cargada y con simbolos resueltos.</summary>
        public static readonly bool Available;

        static BlasInterop()
        {
            try
            {
                if (!NativeBlas.HasBlas) { Available = false; return; }
                const int N = 64;
                var a = new double[N * N];
                var b = new double[N * N];
                var c = new double[N * N];
                for (int i = 0; i < N; i++) { a[i * N + i] = 2.0; b[i * N + i] = 3.0; }
                MatMulNative(N, N, N, a, b, c);
                Available = Math.Abs(c[0] - 6.0) < 1e-9 && Math.Abs(c[N * N - 1] - 6.0) < 1e-9;
            }
            catch { Available = false; }
        }

        /// <summary>True si PARDISO (sparse directo de MKL, el solver de OpenSees) está disponible.</summary>
        public static bool PardisoAvailable => NativeBlas.HasPardiso;

        /// <summary>Resuelve A·x=b con A sparse CSR 0-based (matriz completa, real no-simétrica)
        /// vía MKL PARDISO. rowPtr[n+1], colIdx[nnz], vals[nnz]. Requiere Intel MKL.</summary>
        public static double[] SolveSparsePardiso(int n, int[] rowPtr, int[] colIdx, double[] vals, double[] b)
        {
            if (!NativeBlas.HasPardiso)
                throw new InvalidOperationException("PARDISO no disponible (requiere Intel MKL real, no OpenBLAS).");
            var pt = new long[64];
            var iparm = new int[64];
            var perm = new int[n];
            var x = new double[n];
            int error = 0;
            fixed (long* ppt = pt)
            fixed (int* pip = iparm, pia = rowPtr, pja = colIdx, ppe = perm)
            fixed (double* pa = vals, pb = b, px = x)
            {
                int mt = 11;   // real, no-simétrica (matriz completa)
                NativeBlas.Pardisoinit((void*)ppt, &mt, pip);
                iparm[34] = 1; // indexado 0-based (C) → usar el CSR del Lab directo
                int mf = 1, mn = 1, ml = 0, nr = 1, N = n;
                int phase = 13;                 // análisis + factorización + solve
                NativeBlas.Pardiso((void*)ppt, &mf, &mn, &mt, &phase, &N, pa, pia, pja, ppe, &nr, pip, &ml, pb, px, &error);
                int err1 = error;
                int rel = -1, e2 = 0;           // liberar memoria interna
                NativeBlas.Pardiso((void*)ppt, &mf, &mn, &mt, &rel, &N, pa, pia, pja, ppe, &nr, pip, &ml, pb, px, &e2);
                if (err1 != 0) throw new InvalidOperationException($"PARDISO error {err1}");
            }
            return x;
        }

        /// <summary>y[m] = A[m×n] * x[n] via cblas_dgemv (row-major).</summary>
        public static void MatVec(int m, int n, double[] A, double[] x, double[] y)
        {
            if (!Available || NativeBlas.Dgemv == null || m < BlasThreshold)
            {
                for (int i = 0; i < m; i++)
                {
                    double s = 0;
                    int rowA = i * n;
                    for (int j = 0; j < n; j++) s += A[rowA + j] * x[j];
                    y[i] = s;
                }
                return;
            }
            fixed (double* pA = A, pX = x, pY = y)
                NativeBlas.Dgemv(NativeBlas.CblasRowMajor, NativeBlas.CblasNoTrans, m, n, 1.0, pA, n, pX, 1, 0.0, pY, 1);
        }

        /// <summary>y[n] += alpha * x[n] via cblas_daxpy.</summary>
        public static void Axpy(int n, double alpha, double[] x, double[] y)
        {
            if (!Available || NativeBlas.Daxpy == null || n < BlasThreshold)
            {
                for (int i = 0; i < n; i++) y[i] += alpha * x[i];
                return;
            }
            fixed (double* pX = x, pY = y)
                NativeBlas.Daxpy(n, alpha, pX, 1, pY, 1);
        }

        /// <summary>dot product nativo via cblas_ddot.</summary>
        public static double Dot(int n, double[] x, double[] y)
        {
            if (!Available || NativeBlas.Ddot == null || n < BlasThreshold)
            {
                double s = 0;
                for (int i = 0; i < n; i++) s += x[i] * y[i];
                return s;
            }
            fixed (double* pX = x, pY = y)
                return NativeBlas.Ddot(n, pX, 1, pY, 1);
        }

        /// <summary>C[m×n] = A[m×k] * B[k×n] (row-major) via cblas_dgemm.
        /// Cae al loop naive para tamaños chicos o sin BLAS.</summary>
        public static void MatMul(int m, int k, int n, double[] A, double[] B, double[] C)
        {
            int maxDim = m > n ? (m > k ? m : k) : (n > k ? n : k);
            if (!Available || maxDim < BlasThreshold)
            {
                MatMulNaive(m, k, n, A, B, C);
                return;
            }
            MatMulNative(m, k, n, A, B, C);
        }

        private static void MatMulNative(int m, int k, int n, double[] A, double[] B, double[] C)
        {
            // Row-major directo: C[m×n] = A[m×k]·B[k×n], lda=k, ldb=n, ldc=n.
            fixed (double* pA = A, pB = B, pC = C)
                NativeBlas.Dgemm(NativeBlas.CblasRowMajor, NativeBlas.CblasNoTrans, NativeBlas.CblasNoTrans,
                                 m, n, k, 1.0, pA, k, pB, n, 0.0, pC, n);
        }

        // Fallback SIN BLAS: ikj vectorizado con System.Numerics.Vector<double>.
        // El JIT lo compila a SIMD (AVX2 = 4 doubles, AVX-512 = 8) → el "9× mas lento
        // sin MKL" se reduce a ~2-3×, sin ninguna libreria nativa. Orden ikj = la fila
        // de B se recorre lineal (cache-friendly) y cada paso es un AXPY: C_i += a_ip · B_p.
        private static void MatMulNaive(int m, int k, int n, double[] A, double[] B, double[] C)
        {
            Array.Clear(C, 0, m * n);
            int W = Vector<double>.Count, nv = n - (n % W);
            for (int i = 0; i < m; i++)
            {
                int rowA = i * k, rowC = i * n;
                for (int p = 0; p < k; p++)
                {
                    double aip = A[rowA + p];
                    if (aip == 0.0) continue;          // salta ceros (matrices ralas)
                    int rowB = p * n, j = 0;
                    var va = new Vector<double>(aip);
                    for (; j < nv; j += W)             // tramo SIMD
                    {
                        var vb = new Vector<double>(B, rowB + j);
                        var vc = new Vector<double>(C, rowC + j);
                        (vc + va * vb).CopyTo(C, rowC + j);
                    }
                    for (; j < n; j++)                 // cola escalar
                        C[rowC + j] += aip * B[rowB + j];
                }
            }
        }
    }

    /// <summary>LAPACK via LAPACKE (misma libreria que BlasInterop: Intel MKL u OpenBLAS).
    /// Enteros de 32 bits (LP64) → sin el crash ILP64. Para las rutinas con
    /// almacenamiento delicado (banda, simetrica) usamos LAPACK_COL_MAJOR y
    /// replicamos el empaquetado Fortran ya validado; LAPACKE col-major reenvia
    /// a la rutina nativa sin transponer.</summary>
    public static unsafe class LapackInterop
    {
        /// <summary>Threshold: el solve denso solo conviene para n ≥ 64.</summary>
        public const int LapackThreshold = 64;

        /// <summary>Proveedor LAPACK activo: "Intel MKL", "OpenBLAS" o "managed".</summary>
        public static string Provider => NativeBlas.Provider;

        public static readonly bool Available;

        static LapackInterop()
        {
            try
            {
                if (!NativeBlas.HasLapack) { Available = false; return; }
                // Probe: solve [1,2;3,4]·x=[5;11] → x=[1;2] (row-major directo)
                var A = new[] { 1.0, 2.0, 3.0, 4.0 };
                var b = new[] { 5.0, 11.0 };
                var ipiv = new int[2];
                int info;
                fixed (double* pA = A, pB = b)
                fixed (int* pIpiv = ipiv)
                    info = NativeBlas.Dgesv(NativeBlas.LapackRowMajor, 2, 1, pA, 2, pIpiv, pB, 1);
                Available = info == 0 && Math.Abs(b[0] - 1.0) < 1e-9 && Math.Abs(b[1] - 2.0) < 1e-9;
            }
            catch { Available = false; }
        }

        /// <summary>Resuelve A·x = b con A simétrica positive definite banded (bw=kd).
        /// A row-major n×n. Retorna x. (AB en banded col-major + LAPACK_COL_MAJOR.)</summary>
        public static double[] SolveSymBanded(int n, int kd, double[] A_row, double[] b)
        {
            if (!Available) throw new InvalidOperationException("LAPACK no disponible");
            int ldab = kd + 1;
            var AB = new double[ldab * n];
            for (int j = 0; j < n; j++)
            {
                int iMin = System.Math.Max(0, j - kd);
                for (int i = iMin; i <= j; i++)
                    AB[j * ldab + (kd + i - j)] = A_row[i * n + j];
            }
            var x = new double[n];
            System.Array.Copy(b, x, n);
            int info;
            fixed (double* pAB = AB, pX = x)
                info = NativeBlas.Dpbsv(NativeBlas.LapackColMajor, (sbyte)'U', n, kd, 1, pAB, ldab, pX, n);
            if (info != 0)
                throw new InvalidOperationException($"dpbsv info={info} (not SPD or arg error)");
            return x;
        }

        /// <summary>Resuelve A·x = b para A cuadrada n×n y b vector n×1.
        /// A es row-major → LAPACKE_dgesv en row-major directo. Retorna x.</summary>
        public static double[] Solve(int n, double[] A_row, double[] b)
        {
            if (!Available) throw new InvalidOperationException("LAPACK no disponible");
            var a = (double[])A_row.Clone();   // dgesv sobreescribe A con la LU
            var x = new double[n];
            Array.Copy(b, x, n);
            var ipiv = new int[n];
            int info;
            fixed (double* pA = a, pX = x)
            fixed (int* pIpiv = ipiv)
                info = NativeBlas.Dgesv(NativeBlas.LapackRowMajor, n, 1, pA, n, pIpiv, pX, 1);
            if (info != 0)
                throw new InvalidOperationException($"dgesv info={info} (singular or argument error)");
            return x;
        }

        /// <summary>Valores propios generalizados simétricos A·φ = λ·B·φ (itype=1), A simétrica
        /// y B simétrica positive definite. A,B row-major n×n. Retorna (eigenvalues ASC,
        /// eigenvectors row-major V[i*n+k]=componente i del modo k).</summary>
        public static (double[] vals, double[] vecs) SymGenEig(int n, double[] A_row, double[] B_row)
        {
            if (!Available) throw new InvalidOperationException("LAPACK no disponible");
            // Simétricas → row-major≡col-major. Usamos COL_MAJOR para que, a la salida,
            // la columna k de A sea el eigenvector k (A[k*n+i]); LAPACKE gestiona el workspace.
            var A = (double[])A_row.Clone();
            var B = (double[])B_row.Clone();
            var w = new double[n];
            int info;
            fixed (double* pA = A, pB = B, pW = w)
                info = NativeBlas.Dsygv(NativeBlas.LapackColMajor, 1, (sbyte)'V', (sbyte)'U', n, pA, n, pB, n, pW);
            if (info != 0) throw new InvalidOperationException($"dsygv info={info} (B no SPD o arg error)");
            var V = new double[n * n];
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    V[i * n + k] = A[k * n + i];
            return (w, V);
        }
    }
}
