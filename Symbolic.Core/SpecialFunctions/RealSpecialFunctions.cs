using System;

namespace Calcpad.Core
{
    internal static class RealSpecialFunctions
    {
        // Precision for numerical integration with Tanh-Sinh method
        private const double Precision = 1e-15;
        private const double FpMin = 1e-300;

        // Constants
        private static readonly double LogSqrt2Pi = 0.5 * Math.Log(2 * Math.PI);
        // Euler-Mascheroni constant
        private const double γ = 0.57721566490153286060;
        // Constant for Erf calculation
        private static readonly double c = 2.0 / Math.Sqrt(Math.PI);
        // Constant for Logarithmic integral
        private const double Li2 = 1.0451637801174928;
        // Constant for Elliptic integrals
        private static readonly double HalfPi = Math.PI / 2;

        // Logarithm of Gamma function - Real version
        internal static double GammaLn(double x)
        {
            if (x < 0.5)
                return Math.Log(Math.PI / Math.Sin(Math.PI * x)) - GammaLn(1 - x);

            x -= 1;
            var coef = Lanczos.Coefficients;
            var d = coef[0];
            for (int i = 1; i < 15; i++)
                d += coef[i] / (x + i);

            var t = x + Lanczos.G + 0.5;
            return LogSqrt2Pi + (x + 0.5) * Math.Log(t) - t + Math.Log(d);
        }

        // Gamma function - real version
        internal static double Gamma(double x) => x < 0 ? double.NaN : Math.Exp(GammaLn(x));

         internal static double Digamma(double x) => x < 0 ? double.NaN :
            Solver.TanhSinh(t => (1 - Math.Pow(t, x - 1)) / (1 - t), 0.0, 1.0, Precision) - γ;

        internal static double Polygamma(int m, double x)
        {
            if (x <= FpMin)
                return double.NaN;
            if (m < 0)
                return double.NaN;
            if (m == 0)
                return Digamma(x);

            return (int.IsEvenInteger(m) ? -1 : 1) *
            Solver.TanhSinh(t => Math.Pow(-Math.Log(t), m) * Math.Pow(t, x - 1) / (1 - t), FpMin, 1.0, Precision);

        }

        // Riemann Zeta function
        internal static double RiemannZeta(double x)
        {
            if (x < 1)
                return double.NaN;
            if (x == 1)
                return double.PositiveInfinity;

            var eps = x >= 2.0 ?
                Precision :
                Precision * Math.Pow(10, 16 - Math.Pow(x, 4));
            return Solver.TanhSinh(t => Math.Pow(-Math.Log(t), x - 1) * Math.Pow(t, x - 1) / (1 - t), FpMin, 1.0, Precision) / Gamma(x);
        }

        // Dirichlet Eta function
        internal static double DirichletEta(double x)
        {
            if (x < -FpMin)
                return double.NaN;

            if (x < FpMin)
                return double.PositiveInfinity;

            var eps = x >= 1.0 ?
                Precision :
                Precision * Math.Pow(10, 10 *(1 - x));
            return Solver.TanhSinh(t => Math.Pow(-Math.Log(t), x - 1) * Math.Pow(t, x - 1) / (1 + t), FpMin, 1.0, eps) / Gamma(x);
        }

        // Incomplete gamma functions - real version
        internal static double IncompleteGammaLower(double s, double x)
        {
            if (x < -FpMin || s < FpMin)
                return double.NaN;
            if (x < FpMin)
                return 0.0;
            if (double.IsPositiveInfinity(x))
                return Gamma(s);

            return (Solver.TanhSinh(t => Math.Pow(-Math.Log(t), s), Math.Exp(-x), 1.0, Precision) + Math.Pow(x, s) * Math.Exp(-x)) / s;

        }

        internal static double IncompleteGammaUpper(double s, double x)
        {
            if (x < -FpMin || s < FpMin)
                return double.NaN;
            if (x < FpMin)
                return Gamma(s);
            if (double.IsPositiveInfinity(x))
                return 0.0;

            return (Solver.TanhSinh(t => Math.Pow(-Math.Log(t), s), 0.0, Math.Exp(-x), Precision) - Math.Pow(x, s) * Math.Exp(-x)) / s;
        }

        // Incomplete beta function - real version
        internal static double IncompleteBetaTanhSinh(double x, double a, double b)
        {
            if (x < -FpMin || x > 1.0 || a <= FpMin || b <= FpMin)
                return double.NaN;
            if (x < FpMin)
                return 0.0;
            if (x == 1.0)
                return 1.0;
            return Solver.TanhSinh(t => Math.Pow(t, a - 1) * Math.Pow(1 - t, b - 1), FpMin, x, Precision);
        }
            

        internal static double IncompleteBeta(double x, double a, double b)
        {
            if (x < -FpMin || x > 1.0 || a <= FpMin || b <= FpMin)
                return double.NaN;

            var bt = x != 0.0 && x != 1.0 ? 0.0 :
                Math.Exp(GammaLn(a + b) - GammaLn(a) - GammaLn(b) + a * Math.Log(x) + b * Math.Log(1.0 - x));

            if (x < (a + 1.0) / (a + b + 2.0)) // Use continued fraction directly.
                return bt * IncompleteBetaCF(a, b, x) / a;
            else                               // Use continued fraction after making the symmetry transformation.
                return 1.0 - bt * IncompleteBetaCF(b, a, 1.0 - x) / b;
        }
        private static double IncompleteBetaCF(double a, double b, double x)
        {
            const int MaxIter = 100;
            var qab = a + b;     // These q’s will be used in factors that occur in the coefficients (6.4.6).
            var qap = a + 1.0;
            var qam = a - 1.0;
            var c = 1.0;         // First step of Lentz’s method.
            var d = 1.0 - qab * x / qap;
            if (Math.Abs(d) < FpMin)
                d = FpMin;
            d = 1.0 / d;
            var h = d;
            for (int m = 1; m <= MaxIter; m++)
            {
                var m2 = 2 * m;
                var aa = m * (b - m) * x / ((qam + m2) * (a + m2));
                d = 1.0 + aa * d;
                if (Math.Abs(d) < FpMin) d = FpMin;
                c = 1.0 + aa / c;
                if (Math.Abs(c) < FpMin) c = FpMin;
                d = 1.0 / d;
                h *= d * c;
                aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
                d = 1.0 + aa * d;  // Next step of the recurrence (the odd one)
                if (Math.Abs(d) < FpMin) d = FpMin;
                c = 1.0 + aa / c;
                if (Math.Abs(c) < FpMin) c = FpMin;
                d = 1.0 / d;
                var del = d * c;
                h *= del;
                if (Math.Abs(del - 1.0) < Precision) break;
            }
            return h;
        }

        // Beta function - real version
        internal static double Beta(double x, double y) =>
            x <= FpMin || y <= FpMin ? double.NaN :
            double.Exp(GammaLn(x) + GammaLn(y) - GammaLn(x + y));

        // Error function - real version
        internal static double Erf(double x) =>
            c * Math.Sign(x) * Solver.TanhSinh(t => Math.Exp(-t * t), 0d, Math.Abs(x), Precision);

        // Complementary error function - real version
        internal static double Erfc(double x) => 1.0 - Erf(x);
        internal static double FresnelC(double x) =>
            Solver.TanhSinh(t => Math.Cos(t * t), 0d, x, Precision);

        internal static double FresnelS(double x) =>
            Solver.TanhSinh(t => Math.Sin(t * t), 0d, x, Precision);

        // Sine and cosine integrals
        internal static double SinIntegral(double x) =>
            Math.Abs(x) < FpMin ? 0d :
            Solver.TanhSinh(t => Math.Sin(t) / t, 0d, x, Precision);

        internal static double CosIntegral(double x)
        {
            if(x < -FpMin)
                return double.NaN;
            if (x < FpMin)
                return double.NegativeInfinity;

            return γ + Math.Log(x) + Solver.TanhSinh(t => (Math.Cos(t) - 1d) / t, 0d, x, Precision);
        }

        // Hyperbolic sine and cosine integral
        internal static double SinhIntegral(double x) =>
            Math.Abs(x) < FpMin ? 0d :
            Solver.TanhSinh(t => Math.Sinh(t) / t, 0d, x, Precision);

        internal static double CoshIntegral(double x)
        {
            if (x < -FpMin)
                return double.NaN;
            if (x < FpMin)
                return double.NegativeInfinity;

            return γ + Math.Log(x) + Solver.TanhSinh(t => (Math.Cosh(t) - 1d) / t, 0d, x, Precision);

        }

        // Logarithmic Integral
        internal static double Li(double a, double b) =>
            Solver.TanhSinh(t => 1d / Math.Log(t), a, b, Precision);
        internal static double LogIntegral(double x) =>
            x switch
            {
                < 1d => Li(0d, x),
                > 1d => Li(2d, x) + Li2,
                _ => double.NegativeInfinity,
            };

        // Exponential Integral
        internal static double ExpIntegral(double x) =>
             x switch
             {
                 < -FpMin => -Li(0d, Math.Exp(x)),
                 > FpMin => -Li(2d, Math.Exp(-x)) - Li2,
                 _ => double.NegativeInfinity,
             };

        internal static double DawsonF(double x) =>
            Solver.TanhSinh(t => Math.Exp(t * t), 0d, x, Precision) * Math.Exp(-x * x);

        // Incomplete elliptic integral of the first kind
        internal static double EllipticF(double phi, double m)
        {
            if (phi < -HalfPi || phi > HalfPi)
                return double.NaN;

            if (m > 1)
                return double.NaN;

            return Solver.TanhSinh(t =>
            {
                var sin_t = Math.Sin(t);
                return 1d / Math.Sqrt(Math.Max(1 - m * sin_t * sin_t, FpMin));
            }, 0, phi, Precision);
        }
        // Complete elliptic integral of the first kind
        internal static double EllipticK(double m) => EllipticF(HalfPi, m);

        // Incomplete elliptic integral of the second kind
        internal static double EllipticE(double phi, double m) =>
            m > 1 ? double.NaN :
            Solver.TanhSinh(t =>
            {
                var sin_t = Math.Sin(t);
                return Math.Sqrt(1 - m * sin_t * sin_t);
            }, 0, phi, Precision);

        // Complete elliptic integral of the second kind
        internal static double EllipticE(double m) => EllipticE(HalfPi, m);

        // Incomplete elliptic integral of the third kind
        internal static double EllipticPi(double n, double phi, double m) =>
            m > 1 ? double.NaN :
            Solver.TanhSinh(t =>
            {
                var sin_t = Math.Sin(t);
                var sin2_t = sin_t * sin_t;
                return 1d / Math.Max(1 + n * sin2_t, FpMin) * Math.Sqrt(Math.Max(1 - m * sin2_t, FpMin));
            }, 0, phi, Precision);

        // Complete elliptic integral of the third kind
        internal static double EllipticPi(double n, double m) => EllipticPi(n, HalfPi, m);

        // Jacobi elliptic functions
        // Jacobi elliptic amplitude am
        internal static double JacobiAm(double u, double m)
        {
            if (u == 0) 
                return 0;
            if (m == 0)
                return u;
            if (m == 1)
                return 2 * Math.Atan(Math.Exp(u)) - HalfPi;
            return Solver.ModAB(phi => EllipticF(phi, m), -HalfPi, HalfPi, u, Precision, out _);

        }

        // Jacobi elliptic function sn
        internal static double JacobiSn(double u, double m)
        {
            var phi = JacobiAm(u, m);
            return Math.Sin(phi);
        }

        // Jacobi elliptic function cn
        internal static double JacobiCn(double u, double m)
        {
            var phi = JacobiAm(u, m);
            return Math.Cos(phi);
        }

        // Jacobi elliptic function dn
        internal static double JacobiDn(double u, double m)
        {
            var phi = JacobiAm(u, m);
            var sn = Math.Sin(phi);
            return Math.Sqrt(1 - m * sn * sn);
        }

        // Jacobi elliptic function cs
        internal static double JacobiCs(double u, double m)
        {
            var phi = JacobiAm(u, m);
            var sn = Math.Sin(phi);
            var cn = Math.Cos(phi);
            return cn / sn;
        }

        // Jacobi elliptic function cd   
        internal static double JacobiCd(double u, double m)
        {
            var phi = JacobiAm(u, m);
            var sn = Math.Sin(phi);
            var cn = Math.Cos(phi);
            var dn = Math.Sqrt(1 - m * sn * sn);
            return cn / dn;
        }

        // Jacobi elliptic function dc
        internal static double JacobiDc(double u, double m)
        {
            var phi = JacobiAm(u, m);
            var sn = Math.Sin(phi);
            var dn = Math.Sqrt(1 - m * sn * sn);
            var cn = Math.Cos(phi);
            return dn / cn;
        }

        // Jacobi elliptic function sc
        internal static double JacobiSc(double u, double m)
        {
            var phi = JacobiAm(u, m);
            var sn = Math.Sin(phi);
            var cn = Math.Cos(phi);
            return sn / cn;
        }

        // Jacobi elliptic function sd
        internal static double JacobiSd(double u, double m)
        {
            var phi = JacobiAm(u, m);
            var sn = Math.Sin(phi);
            var dn = Math.Sqrt(1 - m * sn * sn);
            return sn / dn;
        }

        // Jacobi elliptic function ds
        internal static double JacobiDs(double u, double m)
        {
            var phi = JacobiAm(u, m);
            var sn = Math.Sin(phi);
            var dn = Math.Sqrt(1 - m * sn * sn);
            return dn / sn;
        }

        // Reciprocal Jacobi elliptic functions
        // ns(u,m) = 1/sn(u,m)
        internal static double JacobiNs(double u, double m) => 1.0 / JacobiSn(u, m);
        // nc(u,m) = 1/cn(u,m)
        internal static double JacobiNc(double u, double m) => 1.0 / JacobiCn(u, m);
        // nd(u, m) = 1/dn(u, m)
        internal static double JacobiNd(double u, double m) => 1.0 / JacobiDn(u, m);
        // Bessel functions of first kind
        // Bessel function J0(x) for any real x.
        internal static double BesselJ0(double x)
        {
            var ax = Math.Abs(x);
            if (ax < 8.0) // Direct rational function fit.
            {
                var y = x * x;
                var ans1 = 57568490574.0 + y * (-13362590354.0 + y * (651619640.7 +
                    y * (-11214424.18 + y * (77392.33017 + y * (-184.9052456)))));
                var ans2 = 57568490411.0 + y * (1029532985.0 + y * (9494680.718 +
                    y * (59272.64853 + y * (267.8532712 + y * 1.0))));
                var ans = ans1 / ans2;
                return ans;
            }
            else // Fitting function (6.5.9).
            {
                var z = 8.0 / ax;
                var y = z * z;
                var xx = ax - 0.785398164;
                var ans1 = 1.0 + y * (-0.1098628627e-2 +
                    y * (0.2734510407e-4 + y * (-0.2073370639e-5 + y * 0.2093887211e-6)));
                var ans2 = -0.1562499995e-1 + y * (0.1430488765e-3 +
                    y * (-0.6911147651e-5 + y * (0.7621095161e-6 - y * 0.934935152e-7)));
                var ans = Math.Sqrt(0.636619772 / ax) * (Math.Cos(xx) * ans1 - z * Math.Sin(xx) * ans2);
                return ans;
            }
        }

        // Bessel function J1(x) for any real x.
        internal static double BesselJ1(double x)
        {
            var ax = Math.Abs(x);
            if (ax < 8.0) // Direct rational approximation.
            {
                var y = x * x;
                var ans1 = x * (72362614232.0 + y * (-7895059235.0 + y * (242396853.1
                    + y * (-2972611.439 + y * (15704.48260 + y * (-30.16036606))))));
                var ans2 = 144725228442.0 + y * (2300535178.0 + y * (18583304.74
                    + y * (99447.43394 + y * (376.9991397 + y * 1.0))));
                var ans = ans1 / ans2;
                return ans;
            }
            else // Fitting function (6.5.9).
            {
                var z = 8.0 / ax;
                var y = z * z;
                var xx = ax - 2.356194491;
                var ans1 = 1.0 + y * (0.183105e-2 + y * (-0.3516396496e-4
                    + y * (0.2457520174e-5 + y * (-0.240337019e-6))));
                var ans2 = 0.04687499995 + y * (-0.2002690873e-3
                    + y * (0.8449199096e-5 + y * (-0.88228987e-6
                    + y * 0.105787412e-6)));
                var ans = Math.Sqrt(0.636619772 / ax) * (Math.Cos(xx) * ans1 - z * Math.Sin(xx) * ans2);
                return x < 0.0 ? -ans : ans;
            }
        }

        // Bessel function Jn(x) for integer order n >= 2 and any real x.
        internal static double BesselJn(int n, double x)
        {
            const double ACC = 160.0;
            const double BIGNO = 1.0e10;
            const double BIGNI = 1.0e-10;
            if (n < 2)
                throw new ArgumentException("Index n less than 2 in BesselJn");

            var ax = Math.Abs(x);
            if (ax == 0.0)
                return 0.0;

            if (ax > n) // Upwards recurrence from J0 and J1.
            {
                var tox = 2.0 / ax;
                var bjm = BesselJ0(ax);
                var bj = BesselJ1(ax);
                for (var j = 1; j < n; j++)
                {
                    var bjp = j * tox * bj - bjm;
                    bjm = bj;
                    bj = bjp;
                }
                return bj;

            }
            else // Downwards recurrence from an even m
            {
                var tox = 2.0 / ax;
                var m = 2 * ((n + (int)Math.Sqrt(ACC * n)) / 2);
                var jsum = false;
                var bjp = 0.0;
                var ans = 0.0;
                var sum = 0.0;
                var bj = 1.0;
                for (var j = m; j > 0; j--)   // The downward recurrence.
                {
                    var bjm = j * tox * bj - bjp;
                    bjp = bj;
                    bj = bjm;
                    if (Math.Abs(bj) > BIGNO) // Renormalize to prevent overflows.
                    {
                        bj *= BIGNI;
                        bjp *= BIGNI;
                        ans *= BIGNI;
                        sum *= BIGNI;
                    }
                    if (jsum) sum += bj;    // Accumulate the sum.
                    jsum = !jsum;           // Change 0 to 1 or vice versa.
                    if (j == n) ans = bjp;  // Save the unnormalized answer.
                }
                sum = 2.0 * sum - bj;       // Compute(5.5.16)
                ans /= sum;                 // and use it to normalize the answer.
                return x < 0.0 && int.IsOddInteger(n) ? -ans : ans;
            }
        }

        // Bessel functions of second kind
        // Bessel function Y0(x) for positive x.
        internal static double BesselY0(double x)
        {
            if (x < 8.0) // Rational function approximation of (6.5.8).
            {
                var y = x * x;
                var ans1 = -2957821389.0 + y * (7062834065.0 + y * (-512359803.6
                    + y * (10879881.29 + y * (-86327.92757 + y * 228.4622733))));
                var ans2 = 40076544269.0 + y * (745249964.8 + y * (7189466.438
                    + y * (47447.26470 + y * (226.1030244 + y * 1.0))));
                var ans = (ans1 / ans2) + 0.636619772 * BesselJ0(x) * Math.Log(x);
                return ans;
            }
            else // Fitting function (6.5.10).
            {
                var z = 8.0 / x;
                var y = z * z;
                var xx = x - 0.785398164;
                var ans1 = 1.0 + y * (-0.1098628627e-2 + y * (0.2734510407e-4
                    + y * (-0.2073370639e-5 + y * 0.2093887211e-6)));
                var ans2 = -0.1562499995e-1 + y * (0.1430488765e-3
                    + y * (-0.6911147651e-5 + y * (0.7621095161e-6
                    + y * (-0.934945152e-7))));
                var ans = Math.Sqrt(0.636619772 / x) * (Math.Sin(xx) * ans1 + z * Math.Cos(xx) * ans2);
                return ans;
            }
        }

        // Bessel function Y1(x) for positive x.
        internal static double BesselY1(double x)
        {
            if (x < 8.0) // Rational function approximation of (6.5.8).
            {
                var z = 8.0 / x;
                var xx = x - 2.356194491;
                var y = z * z;
                var ans1 = 1.0 + y * (0.183105e-2 + y * (-0.3516396496e-4
                    + y * (0.2457520174e-5 + y * (-0.240337019e-6))));
                var ans2 = 0.04687499995 + y * (-0.2002690873e-3
                    + y * (0.8449199096e-5 + y * (-0.88228987e-6
                    + y * 0.105787412e-6)));
                var ans = Math.Sqrt(0.636619772 / x) * (Math.Sin(xx) * ans1 + z * Math.Cos(xx) * ans2);
                return ans;
            }
            else // Fitting function (6.5.10).
            {
                var z = 8.0 / x;
                var y = z * z;
                var xx = x - 2.356194491;
                var ans1 = 1.0 + y * (0.183105e-2 + y * (-0.3516396496e-4
                    + y * (0.2457520174e-5 + y * (-0.240337019e-6))));
                var ans2 = 0.04687499995 + y * (-0.2002690873e-3
                    + y * (0.8449199096e-5 + y * (-0.88228987e-6
                    + y * 0.105787412e-6)));
                var ans = Math.Sqrt(0.636619772 / x) * (Math.Sin(xx) * ans1 + z * Math.Cos(xx) * ans2);
                return ans;
            }
        }

        // Bessel function Yn(x) for integer order n >= 2 and positive x.
        internal static double BesselYn(int n, double x)
        {
            if (n < 2) throw new ArgumentException("Index n less than 2 in BesselYn,");
            var tox = 2.0 / x;
            var by = BesselY1(x);
            var bym = BesselY0(x);
            for (int j = 1; j < n; j++)
            {
                var byp = j * tox * by - bym;
                bym = by;
                by = byp;
            }
            return by;
        }

        // Bessel functions of of fractional order
        internal static void BesselJY(double x, double xnu, out double rj, out double ry, out double rjp, out double ryp)
        {
            const int MAXIT = 10000;
            const double EPS = 1.0e-10;
            const double FPMIN = 1.0e-30;
            const double XMIN = 2.0;
            if (x <= 0.0 || xnu < 0.0)
                throw new ArgumentException("bad arguments in BesselJY");

            var nl = (x < XMIN ? (int)(xnu + 0.5) : Math.Max(0, (int)(xnu - x + 1.5)));
            var xmu = xnu - nl;
            var xmu2 = xmu * xmu;
            var xi = 1.0 / x;
            var xi2 = 2.0 * xi;
            var w = xi2 / Math.PI;
            var isign = 1;
            var h = xnu * xi;
            if (h < FPMIN) h = FPMIN;
            var b = xi2 * xnu;
            var d = 0.0;
            var c = h;
            int i = 0;
            for (; i < MAXIT; i++)
            {
                b += xi2;
                d = b - d;
                if (Math.Abs(d) < FPMIN) d = FPMIN;
                c = b - 1.0 / c;
                if (Math.Abs(c) < FPMIN) c = FPMIN;
                d = 1.0 / d;
                var del = c * d;
                h = del * h;
                if (d < 0.0) isign = -isign;
                if (Math.Abs(del - 1.0) <= EPS) break;
            }
            if (i >= MAXIT)
                throw new ArgumentException("x is too large in BesselJY. Try asymptotic expansion.");

            var rjl = isign * FPMIN;
            var rjpl = h * rjl;
            var rjl1 = rjl;
            var rjp1 = rjpl;
            var fact = xnu * xi;
            for (int l = nl - 1; l >= 0; l--)
            {
                var rjtemp = fact * rjl + rjpl;
                fact -= xi;
                rjpl = fact * rjtemp - rjl;
                rjl = rjtemp;
            }
            if (rjl == 0.0) rjl = EPS;
            var f = rjpl / rjl;
            double rjmu, rymu, ry1;
            if (x < XMIN)
            {
                var x2 = 0.5 * x;
                var pimu = Math.PI * xmu;
                fact = (Math.Abs(pimu) < EPS ? 1.0 : pimu / Math.Sin(pimu));
                d = -Math.Log(x2);
                var e = xmu * d;
                var fact2 = Math.Abs(e) < EPS ? 1.0 : Math.Sinh(e) / e;
                Beschb(xmu, out double gam1, out double gam2, out double gampl, out double gammi);
                var ff = 2.0 / Math.PI * fact * (gam1 * Math.Cosh(e) + gam2 * fact2 * d);
                e = Math.Exp(e);
                var p = e / (gampl * Math.PI);
                var q = 1.0 / (e * Math.PI * gammi);
                var pimu2 = 0.5 * pimu;
                var fact3 = (Math.Abs(pimu2) < EPS ? 1.0 : Math.Sin(pimu2) / pimu2);
                var r = Math.PI * pimu2 * fact3 * fact3;
                c = 1.0;
                d = -x2 * x2;
                var sum = ff + r * q;
                var sum1 = p;
                for (i = 1; i <= MAXIT; i++)
                {
                    ff = (i * ff + p + q) / (i * i - xmu2);
                    c *= (d / i);
                    p /= (i - xmu);
                    q /= (i + xmu);
                    var del = c * (ff + r * q);
                    sum += del;
                    var del1 = c * p - i * del;
                    sum1 += del1;
                    if (Math.Abs(del) < (1.0 + Math.Abs(sum)) * EPS) break;
                }
                if (i > MAXIT)
                    throw new MathParserException("BesselJY series failed to converge.");

                rymu = -sum;
                ry1 = -sum1 * xi2;
                var rymup = xmu * xi * rymu - ry1;
                rjmu = w / (rymup - f * rymu);
            }
            else
            {
                var a = 0.25 - xmu2;
                var p = -0.5 * xi;
                var q = 1.0;
                var br = 2.0 * x;
                var bi = 2.0;
                fact = a * xi / (p * p + q * q);
                var cr = br + q * fact;
                var ci = bi + p * fact;
                var den = br * br + bi * bi;
                var dr = br / den;
                var di = -bi / den;
                var dlr = cr * dr - ci * di;
                var dli = cr * di + ci * dr;
                var temp = p * dlr - q * dli;
                q = p * dli + q * dlr;
                p = temp;
                for (i = 1; i < MAXIT; i++)
                {
                    a += 2 * i;
                    bi += 2.0;
                    dr = a * dr + br;
                    di = a * di + bi;
                    if (Math.Abs(dr) + Math.Abs(di) < FPMIN) dr = FPMIN;
                    fact = a / (cr * cr + ci * ci);
                    cr = br + cr * fact;
                    ci = bi - ci * fact;
                    if (Math.Abs(cr) + Math.Abs(ci) < FPMIN) cr = FPMIN;
                    den = dr * dr + di * di;
                    dr /= den;
                    di /= -den;
                    dlr = cr * dr - ci * di;
                    dli = cr * di + ci * dr;
                    temp = p * dlr - q * dli;
                    q = p * dli + q * dlr;
                    p = temp;
                    if (Math.Abs(dlr - 1.0) + Math.Abs(dli) <= EPS) break;
                }
                if (i >= MAXIT)
                    throw new MathParserException("cf2 failed in BesselJY");

                var gam = (p - f) / q;
                rjmu = Math.Sqrt(w / ((p - f) * gam + q));
                rjmu = Math.CopySign(rjmu, rjl);
                rymu = rjmu * gam;
                var rymup = rymu * (p + q / gam);
                ry1 = xmu * xi * rymu - rymup;
            }
            fact = rjmu / rjl;
            rj = rjl1 * fact;
            rjp = rjp1 * fact;
            for (i = 1; i <= nl; i++)
            {
                var rytemp = (xmu + i) * xi2 * ry1 - rymu;
                rymu = ry1;
                ry1 = rytemp;
            }
            ry = rymu;
            ryp = xnu * xi * rymu - ry1;
        }

        // Evaluates Γ1 and Γ2 by Chebyshev expansion for |x| ≤ 1/2. Also returns 1/Γ(1 + x) and 1/Γ(1 − x).
        private static void Beschb(double x, out double gam1, out double gam2, out double gampl, out double gammi)
        {
            const int NUSE1 = 7, NUSE2 = 8;
            double[] c1 = [
                -1.142022680371168e0,6.5165112670737e-3,
                3.087090173086e-4,-3.4706269649e-6,6.9437664e-9,
                3.67795e-11,-1.356e-13];
            double[] c2 = [
                1.843740587300905e0,-7.68528408447867e-2,
                1.2719271366546e-3,-4.9717367042e-6,-3.31261198e-8,
                2.423096e-10,-1.702e-13,-1.49e-15];

            // Multiply x by 2 to make range be −1 to 1,
            // and then apply transformation for evaluating even Chebyshev series
            var xx = 8.0 * x * x - 1.0;
            gam1 = Chebev(-1.0, 1.0, c1, NUSE1, xx);
            gam2 = Chebev(-1.0, 1.0, c2, NUSE2, xx);
            gampl = gam2 - x * gam1;
            gammi = gam2 + x * gam1;
        }

        // Chebyshev polynomial evaluation
        private static double Chebev(double a, double b, double[] c, int m, double x)
        {
            if ((x - a) * (x - b) > 0.0)
                throw new ArgumentException("x not in range in routine chebev.");

            var y = (2.0 * x - a - b) / (b - a);
            var y2 = 2.0 * y;
            var d = 0.0;
            var dd = 0.0;
            for (int j = m - 1; j > 0; j--)
            {
                var sv = d;
                d = y2 * d - dd + c[j];
                dd = sv;
            }
            return y * d - dd + 0.5 * c[0];
        }
        internal static void BesselSpherical(int n, double x, out double sj, out double sy, out double sjp, out double syp)
        {
            const double RTPIO2 = 1.253314137315500251;
            if (n < 0 || x <= 0.0)
                throw new ArgumentException("Invalid arguments for BesselSpherical.");

            var order = n + 0.5;
            BesselJY(x, order, out var rj, out var ry, out var rjp, out var ryp);
            var factor = RTPIO2 / Math.Sqrt(x);
            sj = factor * rj;
            sy = factor * ry;
            sjp = factor * rjp - sj / (2.0 * x);
            syp = factor * ryp - sy / (2.0 * x);
        }

        // Modified Bessel functions of first kind

        internal static double BesselI0(double x)
        {
            var ax = Math.Abs(x);
            if (ax < 3.75)
            {
                var y = x / 3.75;
                y *= y;
                var ans = 1.0 + y * (3.5156229 + y * (3.0899424 + y * (1.2067492
                    + y * (0.2659732 + y * (0.360768e-1 + y * 0.45813e-2)))));

                return ans;
            }
            else
            {
                var y = 3.75 / ax;
                var ans = Math.Exp(ax) / Math.Sqrt(ax) * (0.39894228 + y * (0.1328592e-1
                    + y * (0.225319e-2 + y * (-0.157565e-2 + y * (0.916281e-2
                    + y * (-0.2057706e-1 + y * (0.2635537e-1 + y * (-0.1647633e-1
                    + y * 0.392377e-2))))))));

                return ans;
            }
        }

        internal static double BesselI1(double x)
        {
            var ax = Math.Abs(x);
            if (ax < 3.75)
            {
                var y = x / 3.75;
                y *= y;
                var ans = ax * (0.5 + y * (0.87890594 + y * (0.51498869 + y * (0.15084934
                    + y * (0.2658733e-1 + y * (0.301532e-2 + y * 0.32411e-3))))));
                
                return x < 0.0 ? -ans : ans;
            }
            else
            {
                var y = 3.75 / ax;
                var ans = 0.2282967e-1 + y * (-0.2895312e-1 + y * (0.1787654e-1
                    - y * 0.420059e-2));
                ans = 0.39894228 + y * (-0.3988024e-1 + y * (-0.362018e-2
                    + y * (0.163801e-2 + y * (-0.1031555e-1 + y * ans))));
                ans *= Math.Exp(ax) / Math.Sqrt(ax);

                return x < 0.0 ? -ans : ans;
            }
        }

        // Returns the modified Bessel function In(x) for any real x and n ≥ 2.
        internal static double BesselIn(int n, double x)
        {
            const double ACC = 200.0;
            const double BIGNO = 1.0e10;
            const double BIGNI = 1.0e-10;
            if (n < 2)
                throw new ArgumentException("Index n less than 2 in BesselIn.");

            if (x == 0.0)
                return 0.0;

            var tox = 2.0 / Math.Abs(x);
            var bip = 0.0;
            var ans = 0.0;
            var bi = 1.0;
            for (int j = 2 * (n + (int)Math.Sqrt(ACC * n)); j > 0; j--)  // Downward recurrence from even m.
            {
                var bim = bip + j * tox * bi; 
                bip = bi;
                bi = bim;
                if (Math.Abs(bi) > BIGNO)  // Renormalize to prevent overflows.
                {
                    ans *= BIGNI;
                    bi *= BIGNI;
                    bip *= BIGNI;
                }
                if (j == n) ans = bip;
            }
            ans *= BesselI0(x) / bi; // Normalize with bessi0.
            return x < 0.0 && int.IsOddInteger(n) ? -ans : ans;
        }

        // Modified Bessel functions of second kind
        internal static double BesselK0(double x)
        {
            if (x <= 2.0)
            {
                var y = x * x / 4.0;
                var ans = (-Math.Log(x / 2.0) * BesselI0(x)) + (-0.57721566 + y * (0.42278420
                    + y * (0.23069756 + y * (0.3488590e-1 + y * (0.262698e-2
                    + y * (0.10750e-3 + y * 0.74e-5))))));

                return ans;
            }
            else
            {
                var y = 2.0 / x;
                var ans = (Math.Exp(-x) / Math.Sqrt(x)) * (1.25331414 + y * (-0.7832358e-1
                    + y * (0.2189568e-1 + y * (-0.1062446e-1 + y * (0.587872e-2
                    + y * (-0.251540e-2 + y * 0.53208e-3))))));

                return ans;
            }
        }

        internal static double BesselK1(double x)
        {
            if (x <= 2.0)
            {
                var y = x * x / 4.0;
                var ans = (Math.Log(x / 2.0) * BesselI1(x)) + (1.0 / x) * (1.0 + y * (0.15443144
                    + y * (-0.67278579 + y * (-0.18156897 + y * (-0.1919402e-1
                    + y * (-0.110404e-2 + y * (-0.4686e-4)))))));

                return ans;
            }
            else
            {
                var y = 2.0 / x;
                var ans = (Math.Exp(-x) / Math.Sqrt(x)) * (1.25331414 + y * (0.23498619
                    + y * (-0.3655620e-1 + y * (0.1504268e-1 + y * (-0.780353e-2
                    + y * (0.325614e-2 + y * (-0.68245e-3)))))));

                return ans;
            }
        }

        internal static double BesselKn(int n, double x)
        {
            if (n < 2)
                throw new ArgumentException("Index n less than 2 in BesselKn");

            var tox = 2.0 / x;
            var bkm = BesselK0(x);
            var bk = BesselK1(x);
            for (int j = 1; j < n; j++)
            {
                var bkp = bkm + j * tox * bk;
                bkm = bk;
                bk = bkp;
            }
            return bk;
        }

        internal static void BesselIK(double x, double xnu, out double ri, out double rk, out double rip, out double rkp)
        {
            const int MAXIT = 10000;
            const double EPS = 1.0e-10;
            const double FPMIN = 1.0e-30;
            const double XMIN = 2.0;
            if (x <= 0.0 || xnu < 0.0)
                throw new ArgumentException("Bad arguments in BesselIK.");

            var nl = (int)(xnu + 0.5);
            var xmu = xnu - nl;
            var xmu2 = xmu * xmu;
            var xi = 1.0 / x;
            var xi2 = 2.0 * xi;
            var h = xnu * xi;
            if (h < FPMIN) h = FPMIN;
            var b = xi2 * xnu;
            var d = 0.0;
            var c = h;
            int i = 0;
            for (; i < MAXIT; i++)
            {
                b += xi2;
                d = 1.0 / (b + d);
                c = b + 1.0 / c;
                var del = c * d;
                h = del * h;
                if (Math.Abs(del - 1.0) <= EPS) break;
            }
            if (i >= MAXIT)
                throw new ArgumentException("x too large in BesselIK. Try asymptotic expansion");
            var ril = FPMIN;
            var ripl = h * ril;
            var ril1 = ril;
            var rip1 = ripl;
            var fact = xnu * xi;
            for (int l = nl - 1; l >= 0; l--)
            {
                var ritemp = fact * ril + ripl;
                fact -= xi;
                ripl = fact * ritemp + ril;
                ril = ritemp;
            }
            var f = ripl / ril;
            double rkmu, rk1, delh;
            if (x < XMIN)
            {
                var x2 = 0.5 * x;
                var pimu = Math.PI * xmu;
                fact = Math.Abs(pimu) < EPS ? 1.0 : pimu / Math.Sin(pimu);
                d = -Math.Log(x2);
                var e = xmu * d;
                var fact2 = Math.Abs(e) < EPS ? 1.0 : Math.Sinh(e) / e;
                Beschb(xmu, out var gam1, out var gam2, out var gampl, out var gammi);
                var ff = fact * (gam1 * Math.Cosh(e) + gam2 * fact2 * d);
                var sum = ff;
                e = Math.Exp(e);
                var p = 0.5 * e / gampl;
                var q = 0.5 / (e * gammi);
                c = 1.0;
                d = x2 * x2;
                var sum1 = p;
                for (i = 1; i <= MAXIT; i++)
                {
                    ff = (i * ff + p + q) / (i * i - xmu2);
                    c *= d / i;
                    p /= i - xmu;
                    q /= i + xmu;
                    var del = c * ff;
                    sum += del;
                    var del1 = c * (p - i * ff);
                    sum1 += del1;
                    if (Math.Abs(del) < Math.Abs(sum) * EPS) break;
                }
                if (i > MAXIT) throw new MathParserException("BesselK series failed to converge.");
                rkmu = sum;
                rk1 = sum1 * xi2;
            }
            else
            {
                b = 2.0 * (1.0 + x);
                d = 1.0 / b;
                h = delh = d;
                var q1 = 0.0;
                var q2 = 1.0;
                var a1 = 0.25 - xmu2;
                var q = c = a1;
                var a = -a1;
                var s = 1.0 + q * delh;
                for (i = 1; i < MAXIT; i++)
                {
                    a -= 2 * i;
                    c = -a * c / (i + 1.0);
                    var qnew = (q1 - b * q2) / a;
                    q1 = q2;
                    q2 = qnew;
                    q += c * qnew;
                    b += 2.0;
                    d = 1.0 / (b + a * d);
                    delh = (b * d - 1.0) * delh;
                    h += delh;
                    var dels = q * delh;
                    s += dels;
                    if (Math.Abs(dels / s) <= EPS) break;
                }
                if (i >= MAXIT) throw new MathParserException("BesselIK failed to converge in cf2");
                h = a1 * h;
                rkmu = Math.Sqrt(Math.PI / (2.0 * x)) * Math.Exp(-x) / s;
                rk1 = rkmu * (xmu + x + 0.5 - h) * xi;
            }
            var rkmup = xmu * xi * rkmu - rk1;
            var rimu = xi / (f * rkmu - rkmup);
            ri = rimu * ril1 / ril;
            rip = rimu * rip1 / ril;
            for (i = 1; i <= nl; i++)
            {
                var rktemp = (xmu + i) * xi2 * rk1 + rkmu;
                rkmu = rk1;
                rk1 = rktemp;
            }
            rk = rkmu;
            rkp = xnu * xi * rkmu - rk1;
        }

        // Airy functions
        internal static void Airy(double x, out double ai, out double bi, out double aip, out double bip)
        {
            const double ONOVRT = 0.577350269189626;
            const double THIRD = (1.0 / 3.0);
            const double TWOTHRD = 2.0 * THIRD;
            var absx = Math.Abs(x);
            var rootx = Math.Sqrt(absx);
            var z = TWOTHRD * absx * rootx;
            if (x > 0.0)
            {
                BesselIK(z, THIRD, out var ri, out var rk, out _, out _);
                ai = rootx * ONOVRT * rk / Math.PI;
                bi = rootx * (rk / Math.PI + 2.0 * ONOVRT * ri);
                BesselIK(z, TWOTHRD, out ri, out rk, out _, out _);
                aip = -x * ONOVRT * rk / Math.PI;
                bip = x * (rk / Math.PI + 2.0 * ONOVRT * ri);
            }
            else if (x < 0.0)
            {
                BesselJY(z, THIRD, out var rj, out var ry, out _, out _);
                ai = 0.5 * rootx * (rj - ONOVRT * ry);
                bi = -0.5 * rootx * (ry + ONOVRT * rj);
                BesselJY(z, TWOTHRD, out rj, out ry, out _, out _);
                aip = 0.5 * absx * (ONOVRT * ry + rj);
                bip = 0.5 * absx * (ONOVRT * rj - ry);
            }
            else
            {
                ai = 0.355028053887817;
                bi = ai / ONOVRT;
                aip = -0.258819403792807;
                bip = -aip / ONOVRT;
            }
        }

        // Lambert W function
        internal static double LambertW(double x)
        {
            const double x_min = -1 / Math.E;
            if ( x < x_min)
                return double.NaN;

            if (x < Math.E)
                return Solver.ModAB(ξ => ξ * Math.Exp(ξ), -1, 1, x, Precision, out _);
            
            const double k_e = Math.E / (Math.E - 1);
            var ln_x = Math.Log(x);
            var ln_ln_x = Math.Log(ln_x);
            var w_a = ln_x - ln_ln_x;
            var w_b = ln_ln_x / ln_x;
            var w_min = w_a + 0.5 * w_b;
            var w_max = w_a + k_e * w_b;
            return Solver.ModAB(ξ => ξ * Math.Exp(ξ), w_min, w_max, x, Precision, out _);
        }
    }
}