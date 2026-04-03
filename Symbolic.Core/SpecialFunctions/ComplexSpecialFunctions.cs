using System;

namespace Calcpad.Core
{
    internal static partial class ComplexSpecialFunctions
    {        
        // Precision for numerical integration with Tanh-Sinh method
        private const double Precision = 1e-15;
        // Constants
        private static readonly double LogSqrt2Pi = 0.5 * Math.Log(2 * Math.PI);
        private static readonly double SqrtPiOver2 = 1 / Math.Sqrt(2);
        // Constant for Erf calculation
        private static readonly double c = 2.0 / Math.Sqrt(Math.PI);
        // Constants for Fresnel integrals
        private static readonly double InvSqrt2 = Math.Sqrt(0.5);
        private static readonly Complex CoefS = SqrtPiOver2 * new Complex(1, 1) / 4;  // √(π/2) * (1+i)/4
        private static readonly Complex CoefC = SqrtPiOver2 * new Complex(1, -1) / 4; // √(π/2) * (1-i)/4

        // Logarithm of Gamma function - Complex version
        internal static Complex GammaLn(Complex z)
        {
            if (z.Re < 0.5)
                return Math.Log(Math.PI) - Complex.Log(Complex.Sin(Math.PI * z)) - GammaLn(1 - z);

            z -= 1;
            var coef = Lanczos.Coefficients;
            Complex x = coef[0];
            for (int i = 1; i < 15; i++)
                x += coef[i] / (z + i);

            Complex t = z + Lanczos.G + 0.5;
            return LogSqrt2Pi + (z + 0.5) * Complex.Log(t) - t + Complex.Log(x);
        }

        // Gamma function - Complex version
        internal static Complex Gamma(Complex z) => Complex.Exp(GammaLn(z));


        // Incomplete gamma function - Complex version

        // Incomplete beta function - Complex version

        // Beta function - Complex version
        internal static Complex Beta(Complex x, Complex y) => Complex.Exp(GammaLn(x) + GammaLn(y) - GammaLn(x + y));


        // Error function - Complex version
        internal static Complex Erf(Complex z)
        {
            if (z.Equals(Complex.Zero))
                return Complex.Zero;

            var a = z.Re;
            var b = z.Im;
            var x = a * a - b * b;
            var y = 2 * a * b;
            var intCos = Solver.TanhSinh(s => Math.Exp(-x * s * s) * Math.Cos(y * s * s), 0, 1, Precision);
            var intSin = Solver.TanhSinh(s => Math.Exp(-x * s * s) * Math.Sin(y * s * s), 0, 1, Precision);
            return new(
                c * (a * intCos + b * intSin),
                c * (b * intCos - a * intSin)
            );
        }

        // Complementary error function - complex version
        internal static Complex Erfc(Complex z) => Complex.One - Erf(z);

        // Fresnel integrals

        internal static Complex FresnelC(Complex z)
        {
            var arg1 = new Complex(InvSqrt2, InvSqrt2) * z;
            var arg2 = new Complex(InvSqrt2, -InvSqrt2) * z;
            var erf1 = Erf(arg1);
            var erf2 = Erf(arg2);
            return CoefC * (erf1 + Complex.One * erf2);
        }

        internal static Complex FresnelS(Complex z)
        {
            var arg1 = new Complex(InvSqrt2, InvSqrt2) * z;
            var arg2 = new Complex(InvSqrt2, -InvSqrt2) * z;
            var erf1 = Erf(arg1);
            var erf2 = Erf(arg2);
            return CoefS * (erf1 - Complex.One * erf2);
        }
        // Dawson’s Integral - complex version

        // Sine and cosine integrals - complex version

        // Hyperbolic sine and cosine integral - complex version

        // Exponential Integral - complex version

        // Logarithmic Integral - complex version

        // Incomplete elliptic integral of the first kind - complex version

        // Complete elliptic integral of the first kind - complex version

        // Incomplete elliptic integral of the second kind - complex version

        // Complete elliptic integral of the second kind - complex version

        // Incomplete elliptic integral of the third kind - complex version

        // Complete elliptic integral of the third kind - complex version

        // Jacobi elliptic functions - complex version
        // Jacobi elliptic amplitude am
        // Jacobi elliptic function sn
        // Jacobi elliptic function cn
        // Jacobi elliptic function dn
        // Jacobi elliptic function cs
        // Jacobi elliptic function cd   
        // Jacobi elliptic function dc
        // Jacobi elliptic function sc
        // Jacobi elliptic function sd
        // Jacobi elliptic function ds
        // Reciprocal Jacobi elliptic functions
        // ns(u,m) = 1/sn(u,m)
        // nc(u,m) = 1/cn(u,m)
        // nd(u, m) = 1/dn(u, m)
        // Bessel functions of first kind
        // Bessel functions of second kind
        // Modified Bessel functions of first kind
        // Modified Bessel functions of second kind
        // Airy functions
        // Student's t-distribution
    }
}
