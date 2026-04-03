/**
 * eigen_solver.cpp — Native sparse solver DLL for CalcpadCE
 * Uses Eigen 3.4 SimplicialLDLT for symmetric positive-definite systems
 * and SparseLU for general systems.
 *
 * Build (Windows, MinGW):
 *   g++ -O2 -shared -o eigen_solver.dll eigen_solver.cpp -I<eigen_path> -static-libgcc -static-libstdc++
 *
 * Build (Linux):
 *   g++ -O2 -shared -fPIC -o libeigen_solver.so eigen_solver.cpp -I<eigen_path>
 */

#include <Eigen/Sparse>
#include <Eigen/Dense>
#include <cstring>
#include <cmath>

#ifdef _WIN32
  #define EXPORT extern "C" __declspec(dllexport)
#else
  #define EXPORT extern "C" __attribute__((visibility("default")))
#endif

using SpMat = Eigen::SparseMatrix<double>;
using Vec = Eigen::VectorXd;
using Mat = Eigen::MatrixXd;
using Triplet = Eigen::Triplet<double>;

/**
 * Solve symmetric system K*u = f using SimplicialLDLT (Cholesky-like).
 *
 * Input format: upper-triangular skyline (CalcpadCE HpSymmetricMatrix format)
 *   n         = matrix dimension
 *   rowSizes  = array[n] with number of stored values per row
 *               Row i stores values from column i to column (i + rowSizes[i] - 1)
 *   values    = packed array of all row values (row 0 first, then row 1, etc.)
 *   rhs       = right-hand side vector[n]
 *   solution  = output vector[n]
 *
 * Returns: 0 = success, -1 = decomposition failed, -2 = singular
 */
EXPORT int skyline_cholesky_solve(
    int n,
    const int* rowSizes,
    const double* values,
    const double* rhs,
    double* solution)
{
    // Convert skyline to Eigen sparse triplets
    std::vector<Triplet> triplets;
    triplets.reserve(n * 10); // estimate

    int offset = 0;
    for (int i = 0; i < n; ++i) {
        int size = rowSizes[i];
        for (int k = 0; k < size; ++k) {
            int col = i + k;
            if (col >= n) break;
            double val = values[offset + k];
            if (std::abs(val) > 1e-30) {
                triplets.emplace_back(i, col, val);
                if (i != col) {
                    triplets.emplace_back(col, i, val); // symmetric
                }
            }
        }
        offset += size;
    }

    SpMat K(n, n);
    K.setFromTriplets(triplets.begin(), triplets.end());
    K.makeCompressed();

    Vec f = Eigen::Map<const Vec>(rhs, n);

    // Try Cholesky first (fastest for SPD)
    Eigen::SimplicialLDLT<SpMat> solver;
    solver.compute(K);

    if (solver.info() != Eigen::Success) {
        // Fallback to SparseLU for indefinite systems
        Eigen::SparseLU<SpMat> lu;
        lu.compute(K);
        if (lu.info() != Eigen::Success) return -1;

        Vec u = lu.solve(f);
        if (lu.info() != Eigen::Success) return -2;

        std::memcpy(solution, u.data(), n * sizeof(double));
        return 1; // solved with LU
    }

    Vec u = solver.solve(f);
    if (solver.info() != Eigen::Success) return -2;

    std::memcpy(solution, u.data(), n * sizeof(double));
    return 0; // solved with LDLT
}

/**
 * Solve general dense system A*x = b.
 */
EXPORT int dense_solve(
    int n,
    const double* A_data,  // row-major n×n
    const double* b_data,  // vector[n]
    double* x_data)        // output vector[n]
{
    Mat A = Eigen::Map<const Eigen::Matrix<double, Eigen::Dynamic, Eigen::Dynamic, Eigen::RowMajor>>(A_data, n, n);
    Vec b = Eigen::Map<const Vec>(b_data, n);

    Eigen::PartialPivLU<Mat> lu(A);
    Vec x = lu.solve(b);

    std::memcpy(x_data, x.data(), n * sizeof(double));
    return 0;
}

/**
 * Compute eigenvalues of symmetric matrix (dense).
 * Returns eigenvalues in ascending order.
 */
EXPORT int dense_eigenvalues(
    int n,
    const double* A_data,  // row-major n×n symmetric
    double* eigenvalues,   // output[n]
    double* eigenvectors)  // output[n*n] row-major (can be NULL)
{
    Mat A = Eigen::Map<const Eigen::Matrix<double, Eigen::Dynamic, Eigen::Dynamic, Eigen::RowMajor>>(A_data, n, n);

    Eigen::SelfAdjointEigenSolver<Mat> es(A);
    if (es.info() != Eigen::Success) return -1;

    std::memcpy(eigenvalues, es.eigenvalues().data(), n * sizeof(double));

    if (eigenvectors) {
        // Transpose to row-major
        Mat V = es.eigenvectors().transpose();
        std::memcpy(eigenvectors, V.data(), n * n * sizeof(double));
    }

    return 0;
}

/**
 * Sparse generalized eigenvalue problem: K*phi = lambda*M*phi
 * Uses dense conversion (for moderate sizes) + SelfAdjointEigenSolver
 * Returns first numModes eigenvalues/vectors.
 */
EXPORT int sparse_gen_eigen(
    int n,
    const int* K_rowSizes, const double* K_values,
    const int* M_rowSizes, const double* M_values,
    int numModes,
    double* eigenvalues,    // output[numModes]
    double* eigenvectors)   // output[numModes*n] row-major (can be NULL)
{
    // Convert skyline to dense (for moderate n)
    auto skyline_to_dense = [&](const int* rowSizes, const double* values) -> Mat {
        Mat A = Mat::Zero(n, n);
        int offset = 0;
        for (int i = 0; i < n; ++i) {
            int size = rowSizes[i];
            for (int k = 0; k < size; ++k) {
                int col = i + k;
                if (col >= n) break;
                A(i, col) = values[offset + k];
                A(col, i) = values[offset + k];
            }
            offset += size;
        }
        return A;
    };

    Mat K = skyline_to_dense(K_rowSizes, K_values);
    Mat M = skyline_to_dense(M_rowSizes, M_values);

    // Solve M^{-1}K or use GeneralizedSelfAdjointEigenSolver
    Eigen::GeneralizedSelfAdjointEigenSolver<Mat> ges(K, M);
    if (ges.info() != Eigen::Success) return -1;

    int nm = std::min(numModes, n);
    for (int i = 0; i < nm; ++i) {
        eigenvalues[i] = ges.eigenvalues()(i);
    }

    if (eigenvectors) {
        for (int i = 0; i < nm; ++i) {
            for (int j = 0; j < n; ++j) {
                eigenvectors[i * n + j] = ges.eigenvectors()(j, i);
            }
        }
    }

    return 0;
}

/**
 * Version info
 */
EXPORT const char* eigen_solver_version() {
    return "EigenSolver 1.0 (Eigen 3.4)";
}
