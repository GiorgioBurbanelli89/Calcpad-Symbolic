#include <cstdio>
#include <cstdlib>

#ifdef _WIN32
#include <windows.h>
#else
#include <dlfcn.h>
#endif

typedef int (*SkylineSolveFn)(int, const int*, const double*, const double*, double*);
typedef const char* (*VersionFn)();

int main() {
    // Load DLL
#ifdef _WIN32
    HMODULE lib = LoadLibraryA("eigen_solver.dll");
    if (!lib) { printf("Failed to load DLL\n"); return 1; }
    auto solve = (SkylineSolveFn)GetProcAddress(lib, "skyline_cholesky_solve");
    auto version = (VersionFn)GetProcAddress(lib, "eigen_solver_version");
#endif

    printf("Version: %s\n", version());

    // Test: 3x3 symmetric SPD
    // K = [4, 1, 0; 1, 3, 1; 0, 1, 2]
    // Skyline: row0=[4,1,0], row1=[3,1], row2=[2]
    int n = 3;
    int rowSizes[] = {3, 2, 1};  // row0: 3 vals, row1: 2 vals, row2: 1 val
    double values[] = {4.0, 1.0, 0.0,  3.0, 1.0,  2.0};
    double rhs[] = {1.0, 2.0, 3.0};
    double sol[3] = {0};

    int result = solve(n, rowSizes, values, rhs, sol);
    printf("Result code: %d\n", result);
    printf("Solution: [%f, %f, %f]\n", sol[0], sol[1], sol[2]);

    // Verify: K*sol should = rhs
    double v0 = 4*sol[0] + 1*sol[1] + 0*sol[2];
    double v1 = 1*sol[0] + 3*sol[1] + 1*sol[2];
    double v2 = 0*sol[0] + 1*sol[1] + 2*sol[2];
    printf("K*x = [%f, %f, %f] (should be [1, 2, 3])\n", v0, v1, v2);

    return 0;
}
