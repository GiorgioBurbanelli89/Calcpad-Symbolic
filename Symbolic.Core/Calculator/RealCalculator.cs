using System;

namespace Calcpad.Core
{
    internal class RealCalculator : Calculator
    {
        private static readonly Operator<RealValue>[] _operators =
            [
                UnitPow,
                RealValue.Divide,
                RealValue.IntDiv,
                (in RealValue a, in RealValue b) => a % b,
                RealValue.Multiply,
                (in RealValue a, in RealValue b) => a - b,
                (in RealValue a, in RealValue b) => a + b,
                (in RealValue a, in RealValue b) => a < b,
                (in RealValue a, in RealValue b) => a > b,
                (in RealValue a, in RealValue b) => a <= b,
                (in RealValue a, in RealValue b) => a >= b,
                (in RealValue a, in RealValue b) => a == b,
                (in RealValue a, in RealValue b) => a != b,
                (in RealValue a, in RealValue b) => a & b,
                (in RealValue a, in RealValue b) => a | b,
                (in RealValue a, in RealValue b) => a ^ b,
                //Assignments do not need implementation here
            ];
        private readonly Function<RealValue>[] _functions;
        private readonly Operator<RealValue>[] _functions2;
        private static readonly Func<IScalarValue[], IScalarValue>[] MultiFunctions =
            [
                values => Min(values),
                values => Max(values),
                Sum,
                SumSq,
                Srss,
                Average,
                Product,
                Mean,
                Switch,
                values => And(values),
                values => Or(values),
                values => Xor(values),
                values => Gcd(values),
                values => Lcm(values),
            ];
        internal override int Degrees
        {
            set => _degrees = value;
            get => _degrees;
        }

        public RealCalculator()
        {
            _functions =
            [
                Sin,      // 0
                Cos,      // 1
                Tan,      // 2
                Csc,      // 3
                Sec,      // 4
                Cot,      // 5
                Asin,     // 6
                Acos,     // 7
                Atan,     // 8
                Acsc,     // 9
                Asec,     // 10
                Acot,     // 11
                Sinh,     // 12
                Cosh,     // 13
                Tanh,     // 14
                Csch,     // 15
                Sech,     // 16
                Coth,     // 17
                Asinh,    // 18
                Acosh,    // 19
                Atanh,    // 20
                Acsch,    // 21
                Asech,    // 22
                Acoth,    // 23
                Log10,    // 24
                Log,      // 25
                Log2,     // 26
                Exp,      // 27
                Abs,      // 28
                Sign,     // 29
                UnitSqrt, // 30
                UnitSqrt, // 31
                UnitCbrt, // 32
                Round,    // 33
                Floor,    // 34
                Ceiling,  // 35
                Truncate, // 36
                Real,     // 37
                Imaginary,// 38
                Phase,    // 39
                (in RealValue a) => a, // 40
                Random,   // 41
                Fact,     // 42
                (in RealValue a) => -a, // 43
                Not,       // 44
                Timer,     // 45
                GammaLn,   // 46
                Gamma,     // 47
                Erf,       // 48
                Erfc,      // 49
                FresnelC,  // 50
                FresnelS,  // 51
                Si,        // 52
                Ci,        // 53
                Shi,       // 54
                Chi,       // 55
                Ei,        // 56
                Li,        // 57
                DawsonF,   // 58
                EllipticK, // 59
                EllipticE, // 60
                BesselJ0,  // 61
                BesselJ1,  // 62
                BesselY0,  // 63
                BesselY1,  // 64
                BesselI0,  // 65
                BesselI1,  // 66
                BesselK0,  // 67
                BesselK1,  // 68
                Ai,        // 69
                Aip,       // 70
                Bi,        // 71
                Bip,       // 72
                LambertW   // 73
            ];

            _functions2 =
            [
                Atan2,      // 0
                UnitRoot,   // 1
                (in RealValue a, in RealValue b) => a % b, // 2
                (in RealValue a, in RealValue b) => MandelbrotSet(a, b), // 3
                GammaIncU,  // 4
                GammaIncL,  // 5
                Beta,       // 6
                EllipticF,  // 7
                EllipticEi, // 8
                EllipticP,  // 9
                JacobiAM,   // 10
                JacobiSN,   // 11
                JacobiCN,   // 12
                JacobiDN,   // 13
                JacobiCS,   // 14
                JacobiCD,   // 15
                JacobiDC,   // 16
                JacobiSC,   // 17
                JacobiSD,   // 18
                JacobiDS,   // 19
                JacobiNS,   // 20
                JacobiNC,   // 21
                JacobiND,   // 22
                BesselJn,   // 23
                BesselYn,   // 24
                BesselJv,   // 25
                BesselYv,   // 26
                BesselJvp,  // 27
                BesselYvp,  // 28
                BesselJs,   // 29
                BesselYs,   // 30
                BesselJsp,  // 31
                BesselYsp,  // 32
                BesselIn,   // 33
                BesselKn,   // 34
                BesselIv,   // 35
                BesselKv,   // 36
                BesselIvp,  // 37
                BesselKvp   // 38
            ];
        }

        internal override IScalarValue EvaluateOperator(long index, in IScalarValue a, in IScalarValue b) => _operators[index](a.AsReal(), b.AsReal());
        internal override IScalarValue EvaluateFunction(long index, in IScalarValue a) => _functions[index](a.AsReal());
        internal override IScalarValue EvaluateFunction2(long index, in IScalarValue a, in IScalarValue b) => _functions2[index](a.AsReal(), b.AsReal());
        internal override IValue EvaluateFunction3(long index, in IValue a, in IValue b, in IValue c) => Functions3[index](a, b, c);
        internal override IScalarValue EvaluateMultiFunction(long index, IScalarValue[] a) => MultiFunctions[index](a);
        internal override IScalarValue EvaluateInterpolation(long index, IScalarValue[] a) => Interpolations[index](a);
        internal override Operator<RealValue> GetOperator(long index) => index == PowerOperatorIndex ? Pow : _operators[index];
        internal override Function<RealValue> GetFunction(long index)
        {
            if (index == SqrFunctionIndex || index == SqrtFunctionIndex)
                return Sqrt;

            if (index == CbrtFunctionIndex)
                return Cbrt;

            return _functions[index];
        }
        internal override Operator<RealValue> GetFunction2(long index) => index == RootFunctionIndex ? Root : _functions2[index];
        internal override Function3 GetFunction3(long index) => Functions3[index];
        internal override Func<IScalarValue[], IScalarValue> GetMultiFunction(long index) => MultiFunctions[index];

        public static RealValue Fact(in RealValue a)
        {
            if (a.Units is not null)
                throw Exceptions.FactorialArgumentUnitless();

            return new(Fact(a.D));
        }

        private static RealValue Real(in RealValue value) => value;
        private static RealValue Imaginary(in RealValue _) => RealValue.Zero;
        private static RealValue Phase(in RealValue value) => new(value.D >= 0d ? 0d : Math.PI);
        internal static RealValue Abs(in RealValue value) => new(Math.Abs(value.D), value.Units);
        private static RealValue Sign(in RealValue value) => double.IsNaN(value.D) ? RealValue.NaN : new(Math.Sign(value.D));

        private RealValue Sin(in RealValue value)
        {
            CheckTrigFunctionUnits("sin", value.Units);
            return new(Complex.RealSin(FromAngleUnits(value)));
        }

        private RealValue Cos(in RealValue value)
        {
            CheckTrigFunctionUnits("cos", value.Units);
            return new(Complex.RealCos(FromAngleUnits(value)));
        }

        private RealValue Tan(in RealValue value)
        {
            CheckTrigFunctionUnits("tan", value.Units);
            return new(Math.Tan(FromAngleUnits(value)));
        }

        private RealValue Csc(in RealValue value)
        {
            CheckTrigFunctionUnits("csc", value.Units);
            return new(1 / Math.Sin(FromAngleUnits(value)));
        }

        private RealValue Sec(in RealValue value)
        {
            CheckTrigFunctionUnits("sec", value.Units);
            return new(1 / Math.Cos(FromAngleUnits(value)));
        }

        private RealValue Cot(in RealValue value)
        {
            CheckTrigFunctionUnits("cot", value.Units);
            return new(1 / Math.Tan(FromAngleUnits(value)));
        }

        private static RealValue Sinh(in RealValue value) /* Hyperbolic sin */
        {
            CheckTrigFunctionUnits("sinh", value.Units);
            return new(Math.Sinh(value.D));
        }

        private static RealValue Cosh(in RealValue value)
        {
            CheckTrigFunctionUnits("cosh", value.Units);
            return new(Math.Cosh(value.D));
        }

        private static RealValue Tanh(in RealValue value)
        {
            CheckTrigFunctionUnits("tanh", value.Units);
            return new(Math.Tanh(value.D));
        }

        private static RealValue Csch(in RealValue value)
        {
            CheckTrigFunctionUnits("csch", value.Units);
            return new(1 / Math.Sinh(value.D));
        }

        private static RealValue Sech(in RealValue value)
        {
            CheckTrigFunctionUnits("sech", value.Units);
            return new(1 / Math.Cosh(value.D));
        }

        private static RealValue Coth(in RealValue value)
        {
            CheckTrigFunctionUnits("coth", value.Units);
            return new(1 / Math.Tanh(value.D));
        }

        private RealValue Asin(in RealValue value)
        {
            CheckTrigFunctionUnits("asin", value.Units);
            return ToAngleUnits(Math.Asin(value.D));
        }

        private RealValue Acos(in RealValue value)
        {
            CheckTrigFunctionUnits("acos", value.Units);
            return ToAngleUnits(Math.Acos(value.D));
        }

        private RealValue Atan(in RealValue value)
        {
            CheckTrigFunctionUnits("atan", value.Units);
            return ToAngleUnits(Math.Atan(value.D));
        }

        private RealValue Acsc(in RealValue value)
        {
            CheckTrigFunctionUnits("acsc", value.Units);
            return value.D == 0d ?
                RealValue.PositiveInfinity :
                ToAngleUnits(Math.Asin(1d / value.D));
        }

        private RealValue Asec(in RealValue value)
        {
            CheckTrigFunctionUnits("asec", value.Units);
            return value.D == 0d ?
                RealValue.PositiveInfinity :
                ToAngleUnits(Math.Acos(1d / value.D));
        }

        private RealValue Acot(in RealValue value)
        {
            CheckTrigFunctionUnits("acot", value.Units);
            return ToAngleUnits(Math.Atan(1d / value.D));
        }

        private static RealValue Asinh(in RealValue value)
        {
            CheckTrigFunctionUnits("asinh", value.Units);
            return new(Math.Asinh(value.D));
        }

        private static RealValue Acosh(in RealValue value)
        {
            CheckTrigFunctionUnits("acosh", value.Units);
            return new(Math.Acosh(value.D));
        }

        private static RealValue Atanh(in RealValue value)
        {
            CheckTrigFunctionUnits("atanh", value.Units);
            return new(Math.Atanh(value.D));
        }

        private static RealValue Acsch(in RealValue value)
        {
            CheckTrigFunctionUnits("acsch", value.Units);
            return new(Math.Asinh(1d / value.D));
        }

        private static RealValue Asech(in RealValue value)
        {
            CheckTrigFunctionUnits("asech", value.Units);
            return new(Math.Acosh(1d / value.D));
        }

        private static RealValue Acoth(in RealValue value)
        {
            CheckTrigFunctionUnits("acoth", value.Units);
            return new(Math.Atanh(1 / value.D));
        }

        private static RealValue Log(in RealValue value)
        {
            CheckTrigFunctionUnits("ln", value.Units);
            return new(Math.Log(value.D));
        }

        private static RealValue Log10(in RealValue value)
        {
            CheckTrigFunctionUnits("log", value.Units);
            return new(Math.Log10(value.D));
        }

        private static RealValue Log2(in RealValue value)
        {
            CheckTrigFunctionUnits("log_2", value.Units);
            return new(Math.Log2(value.D));
        }

        private static RealValue Exp(in RealValue value)
        {
            CheckTrigFunctionUnits("exp", value.Units);
            return new(Math.Exp(value.D));
        }

        private static RealValue Pow(RealValue value, RealValue power, bool isUnit)
        {
            var u = value.Units;
            if (u is not null && u.IsDimensionless)
                return new RealValue(Math.Pow(value.D * u.GetDimensionlessFactor(), power.D));

            return new(
                Math.Pow(value.D, power.D),
                Unit.Pow(u, power, isUnit),
                isUnit
            );
        }

        internal static RealValue Pow(in RealValue value, in RealValue power) =>
            Pow(value, power, false);

        private static RealValue UnitPow(in RealValue value, in RealValue power) =>
            Pow(value, power, value.IsUnit);

        private static RealValue Sqrt(RealValue value, bool isUnit)
        {
            var u = value.Units;
            if (u is not null && u.IsDimensionless)
                return new(Math.Sqrt(value.D * u.GetDimensionlessFactor()));

            var result = Math.Sqrt(value.D);
            return u is null ?
                new(result) :
                new(result, Unit.Root(u, 2, isUnit), isUnit);
        }

        internal static RealValue Sqrt(in RealValue value) =>
            Sqrt(value, false);

        private static RealValue UnitSqrt(in RealValue value) =>
            Sqrt(value, value.IsUnit);

        private static RealValue Cbrt(RealValue value, bool isUnit)
        {
            var u = value.Units;
            if (u is not null && u.IsDimensionless)
                return new(Math.Cbrt(value.D * u.GetDimensionlessFactor()));

            var result = Math.Cbrt(value.D);
            return u is null ?
                new(result) :
                new(result, Unit.Root(u, 3, isUnit), isUnit);
        }

        private static RealValue Cbrt(in RealValue value) =>
            Cbrt(value, false);

        private static RealValue UnitCbrt(in RealValue value) =>
            Cbrt(value, value.IsUnit);

        private static RealValue Root(in RealValue value, in RealValue root, bool isUnit)
        {
            var n = GetRoot(root);
            var u = value.Units;
            var d = u is not null && u.IsDimensionless ?
                value.D * u.GetDimensionlessFactor() :
                value.D;

            var result = int.IsOddInteger(n) && d < 0 ?
                -Math.Pow(-d, 1d / n) :
                Math.Pow(d, 1d / n);

            return u is null ?
                new(result) :
                new(result, Unit.Root(u, n, isUnit), isUnit);
        }

        private static RealValue Root(in RealValue value, in RealValue root) =>
            Root(value, root, false);

        private static RealValue UnitRoot(in RealValue value, in RealValue root) =>
            Root(value, root, value.IsUnit);

        private static RealValue Round(in RealValue value) =>
            new(Math.Round(value.D, MidpointRounding.AwayFromZero), value.Units);

        private static RealValue Floor(in RealValue value) =>
            new(Math.Floor(value.D), value.Units);

        private static RealValue Ceiling(in RealValue value) =>
            new(Math.Ceiling(value.D), value.Units);

        private static RealValue Truncate(in RealValue value) =>
            new(Math.Truncate(value.D), value.Units);

        private static RealValue Random(in RealValue value) =>
            new(Complex.RealRandom(value.D), value.Units);

        private RealValue Atan2(in RealValue a, in RealValue b) =>
            ToAngleUnits(Math.Atan2(b.D * Unit.Convert(a.Units, b.Units, ','), a.D));

        private static IScalarValue Sum(IScalarValue[] values)
        {
            ref var value = ref values[0];
            var result = value.Re;
            var u = value.Units;
            for (int i = 1, len = values.Length; i < len; ++i)
            {
                value = ref values[i];
                result += value.Re * Unit.Convert(u, value.Units, ',');
            }
            return new RealValue(result, u);
        }

        private static IScalarValue SumSq(IScalarValue[] values)
        {
            ref var value = ref values[0];
            var result = value.Re;
            var u = value.Units;
            result *= result;
            for (int i = 1, len = values.Length; i < len; ++i)
            {
                value = ref values[i];
                var b = value.Re * Unit.Convert(u, value.Units, ',');
                result += b * b;
            }
            return new RealValue(result, u?.Pow(2f));
        }

        internal static IScalarValue Srss(IScalarValue[] values)
        {
            ref var value = ref values[0];
            var result = value.Re;
            var u = value.Units;
            result *= result;
            for (int i = 1, len = values.Length; i < len; ++i)
            {
                value = ref values[i];
                var b = value.Re * Unit.Convert(u, value.Units, ',');
                result += b * b;
            }
            return new RealValue(Math.Sqrt(result), u);
        }

        private static IScalarValue Average(IScalarValue[] values)
        {
            ref var value = ref values[0];
            var result = value.Re;
            var u = value.Units;
            for (int i = 1, len = values.Length; i < len; ++i)
            {
                value = ref values[i];
                result += value.Re * Unit.Convert(u, value.Units, ',');
            }
            return new RealValue(result / values.Length, u);
        }

        private static IScalarValue Product(IScalarValue[] values)
        {
            ref var value = ref values[0];
            var result = value.Re;
            var u = value.Units;
            for (int i = 1, len = values.Length; i < len; ++i)
            {
                value = ref values[i];
                u = Unit.Multiply(u, value.Units, out var b);
                result *= value.Re * b;
            }
            return new RealValue(result, u);
        }

        private static IScalarValue Mean(IScalarValue[] values)
        {
            ref var value = ref values[0];
            var result = value.Re;
            var u = value.Units;
            for (int i = 1, len = values.Length; i < len; ++i)
            {
                value = ref values[i];
                u = Unit.Multiply(u, value.Units, out var b);
                result *= value.Re * b;
            }
            result = Math.Pow(result, 1.0 / values.Length);
            if (u is null)
                return new RealValue(result);

            u = Unit.Root(u, values.Length);
            return new RealValue(result, u);
        }

        protected static RealValue Not(in RealValue value) =>
            Math.Abs(value.D) < RealValue.LogicalZero ? RealValue.One : RealValue.Zero;

        private double FromAngleUnits(in RealValue value)
        {
            if (value.Units is null)
                return value.D * ToRad[_degrees];

            return value.D * value.Units.ConvertTo(AngleUnits[1]);
        }

        private RealValue ToAngleUnits(double value) =>
            _returnAngleUnits ?
            new(value * FromRad[_degrees], AngleUnits[_degrees]) :
            new(value * FromRad[_degrees]);

        protected static RealValue Timer(in RealValue _) => new(Timer(), Unit.Get("s"));

        private static RealValue GammaLn(in RealValue x)
        {
            CheckFunctionUnits("GammaLn", x.Units);
            return new(RealSpecialFunctions.GammaLn(x.D));
        }

        private static RealValue Gamma(in RealValue x)
        {
            CheckFunctionUnits("Gamma", x.Units);
            return new(RealSpecialFunctions.Gamma(x.D));
        }

        private static RealValue Erf(in RealValue x)
        {
            CheckFunctionUnits("Erf", x.Units);
            return new(RealSpecialFunctions.Erf(x.D));
        }

        private static RealValue Erfc(in RealValue x)
        {
            CheckFunctionUnits("Erfc", x.Units);
            return new(RealSpecialFunctions.Erfc(x.D));
        }

        private static RealValue FresnelC(in RealValue x)
        {
            CheckFunctionUnits("FresnelC", x.Units);
            return new(RealSpecialFunctions.FresnelC(x.D));
        }

        private static RealValue FresnelS(in RealValue x)
        {
            CheckFunctionUnits("FresnelS", x.Units);
            return new(RealSpecialFunctions.FresnelS(x.D));
        }

        private static RealValue Si(in RealValue x)
        {
            CheckFunctionUnits("Si", x.Units);
            return new(RealSpecialFunctions.SinIntegral(x.D));
        }

        private static RealValue Ci(in RealValue x)
        {
            CheckFunctionUnits("Ci", x.Units);
            return new(RealSpecialFunctions.CosIntegral(x.D));
        }

        private static RealValue Shi(in RealValue x)
        {
            CheckFunctionUnits("Shi", x.Units);
            return new(RealSpecialFunctions.SinhIntegral(x.D));
        }

        private static RealValue Chi(in RealValue x)
        {
            CheckFunctionUnits("Chi", x.Units);
            return new(RealSpecialFunctions.CoshIntegral(x.D));
        }

        private static RealValue Ei(in RealValue x)
        {
            CheckFunctionUnits("Ei", x.Units);
            return new(RealSpecialFunctions.ExpIntegral(x.D));
        }

        private static RealValue Li(in RealValue x)
        {
            CheckFunctionUnits("Li", x.Units);
            return new(RealSpecialFunctions.LogIntegral(x.D));
        }

        private static RealValue DawsonF(in RealValue x)
        {
            CheckFunctionUnits("DawsonF", x.Units);
            return new(RealSpecialFunctions.DawsonF(x.D));
        }

        private static RealValue EllipticK(in RealValue m)
        {
            CheckFunctionUnits("EllipticK", m.Units);
            return new(RealSpecialFunctions.EllipticK(m.D));
        }

        private static RealValue EllipticE(in RealValue m)
        {
            CheckFunctionUnits("EllipticE", m.Units);
            return new(RealSpecialFunctions.EllipticE(m.D));
        }

        private static RealValue BesselJ0(in RealValue x)
        {
            CheckFunctionUnits("BesselJ0", x.Units);
            return new(RealSpecialFunctions.BesselJ0(x.D));
        }

        private static RealValue BesselJ1(in RealValue x)
        {
            CheckFunctionUnits("BesselJ1", x.Units);
            return new(RealSpecialFunctions.BesselJ1(x.D));
        }

        private static RealValue BesselY0(in RealValue x)
        {
            CheckFunctionUnits("BesselY0", x.Units);
            return new(RealSpecialFunctions.BesselY0(x.D));
        }

        private static RealValue BesselY1(in RealValue x)
        {
            CheckFunctionUnits("BesselY1", x.Units);
            return new(RealSpecialFunctions.BesselY1(x.D));
        }

        private static RealValue BesselI0(in RealValue x)
        {
            CheckFunctionUnits("BesselI0", x.Units);
            return new(RealSpecialFunctions.BesselI0(x.D));
        }

        private static RealValue BesselI1(in RealValue x)
        {
            CheckFunctionUnits("BesselI1", x.Units);
            return new(RealSpecialFunctions.BesselI1(x.D));
        }

        private static RealValue BesselK0(in RealValue x)
        {
            CheckFunctionUnits("BesselK0", x.Units);
            return new(RealSpecialFunctions.BesselK0(x.D));
        }

        private static RealValue BesselK1(in RealValue x)
        {
            CheckFunctionUnits("BesselK1", x.Units);
            return new(RealSpecialFunctions.BesselK1(x.D));
        }

        private static RealValue Ai(in RealValue x)
        {
            CheckFunctionUnits("Ai", x.Units);
            RealSpecialFunctions.Airy(x.D, out var ai, out var _, out _, out _);
            return new(ai);
        }

        private static RealValue Aip(in RealValue x)
        {
            CheckFunctionUnits("Ai′", x.Units);
            RealSpecialFunctions.Airy(x.D, out _, out var aip, out _, out _);
            return new(aip);
        }

        private static RealValue Bi(in RealValue x)
        {
            CheckFunctionUnits("Bi", x.Units);
            RealSpecialFunctions.Airy(x.D, out var _, out var _, out var bi, out _);
            return new(bi);
        }

        private static RealValue Bip(in RealValue x)
        {
            CheckFunctionUnits("Bi′", x.Units);
            RealSpecialFunctions.Airy(x.D, out _, out _, out _, out var bip);
            return new(bip);
        }

        private static RealValue GammaIncU(in RealValue s, in RealValue x)
        {
            CheckFunctionUnits("GammaInc‾", s.Units, x.Units);
            return new(RealSpecialFunctions.IncompleteGammaUpper(s.D, x.D));
        }

        private static RealValue GammaIncL(in RealValue s, in RealValue x)
        {
            CheckFunctionUnits("GammaInc_", s.Units, x.Units);
            return new(RealSpecialFunctions.IncompleteGammaLower(s.D, x.D));
        }

        private static RealValue Beta(in RealValue x, in RealValue y)
        {
            CheckFunctionUnits("Beta", x.Units, y.Units);
            return new(RealSpecialFunctions.Beta(x.D, y.D));
        }

        private static RealValue EllipticF(in RealValue phi, in RealValue m)
        {
            CheckFunctionUnits("EllipticF", phi.Units, m.Units);
            return new(RealSpecialFunctions.EllipticF(phi.D, m.D));
        }

        private static RealValue EllipticEi(in RealValue phi, in RealValue m)
        {
            CheckFunctionUnits("EllipticEi", phi.Units, m.Units);
            return new(RealSpecialFunctions.EllipticE(phi.D, m.D));
        }

        private static RealValue EllipticP(in RealValue n, in RealValue m)
        {
            CheckFunctionUnits("EllipticP", n.Units, m.Units);
            return new(RealSpecialFunctions.EllipticPi(n.D, m.D));
        }

        private static RealValue JacobiAM(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiAM", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiAm(u.D, m.D));
        }

        private static RealValue JacobiSN(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiSN", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiSn(u.D, m.D));
        }

        private static RealValue JacobiCN(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiCN", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiCn(u.D, m.D));
        }

        private static RealValue JacobiDN(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiDN", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiDn(u.D, m.D));
        }
        private static RealValue JacobiCS(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiCS", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiCs(u.D, m.D));
        }

        private static RealValue JacobiCD(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiCD", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiCd(u.D, m.D));
        }

        private static RealValue JacobiDC(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiDC", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiDc(u.D, m.D));
        }

        private static RealValue JacobiSC(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiSC", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiSc(u.D, m.D));
        }

        private static RealValue JacobiSD(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiSD", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiSd(u.D, m.D));
        }

        private static RealValue JacobiDS(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiDS", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiDs(u.D, m.D));
        }

        private static RealValue JacobiNS(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiNS", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiNs(u.D, m.D));
        }

        private static RealValue JacobiNC(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiNC", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiNc(u.D, m.D));
        }

        private static RealValue JacobiND(in RealValue u, in RealValue m)
        {
            CheckFunctionUnits("JacobiND", u.Units, m.Units);
            return new(RealSpecialFunctions.JacobiNd(u.D, m.D));
        }
        private static RealValue BesselJn(in RealValue n, in RealValue x)
        {
            CheckFunctionUnits("BesselJn", n.Units, x.Units);
            var i = IScalarValue.AsInt(n);
            return new(RealSpecialFunctions.BesselJn(i, x.D));
        }
        private static RealValue BesselYn(in RealValue n, in RealValue x)
        {
            CheckFunctionUnits("BesselYn", n.Units, x.Units);
            var i = IScalarValue.AsInt(n);
            return new(RealSpecialFunctions.BesselYn(i, x.D));
        }

        private static RealValue BesselJv(in RealValue x, in RealValue nu)
        {
            CheckFunctionUnits("BesselJv", x.Units, nu.Units);
            RealSpecialFunctions.BesselJY(x.D, nu.D, out double jv, out _, out _, out _);
            return new(jv);
        }

        private static RealValue BesselYv(in RealValue x, in RealValue nu)
        {
            CheckFunctionUnits("BesselYv", x.Units, nu.Units);
            RealSpecialFunctions.BesselJY(x.D, nu.D, out _, out double yv, out _, out _);
            return new(yv);
        }

        private static RealValue BesselJvp(in RealValue x, in RealValue nu)
        {
            CheckFunctionUnits("BesselJvp", x.Units, nu.Units);
            RealSpecialFunctions.BesselJY(x.D, nu.D, out _, out _, out double jvp, out _);
            return new(jvp);
        }

        private static RealValue BesselYvp(in RealValue x, in RealValue nu)
        {
            CheckFunctionUnits("BesselYvp", x.Units, nu.Units);
            RealSpecialFunctions.BesselJY(x.D, nu.D, out _, out _, out _, out double yvp);
            return new(yvp);
        }


        private static RealValue BesselJs(in RealValue n, in RealValue x)
        {
            CheckFunctionUnits("BesselJs", n.Units, x.Units);
            var i = IScalarValue.AsInt(n);
            RealSpecialFunctions.BesselSpherical(i, x.D, out double js, out _, out _, out _);
            return new(js);
        }

        private static RealValue BesselYs(in RealValue n, in RealValue x)
        {
            CheckFunctionUnits("BesselYs", n.Units, x.Units);
            var i = IScalarValue.AsInt(n);
            RealSpecialFunctions.BesselSpherical(i, x.D, out _, out double ys, out _, out _);
            return new(ys);
        }

        private static RealValue BesselJsp(in RealValue n, in RealValue x)
        {
            CheckFunctionUnits("BesselJsp", n.Units, x.Units);
            var i = IScalarValue.AsInt(n);
            RealSpecialFunctions.BesselSpherical(i, x.D, out _, out _, out double jsp, out _);
            return new(jsp);
        }

        private static RealValue BesselYsp(in RealValue n, in RealValue x)
        {
            CheckFunctionUnits("BesselYsp", n.Units, x.Units);
            var i = IScalarValue.AsInt(n);
            RealSpecialFunctions.BesselSpherical(i, x.D, out _, out _, out _, out double ysp);
            return new(ysp);
        }

        private static RealValue BesselIn(in RealValue n, in RealValue x)
        {
            CheckFunctionUnits("BesselIn", n.Units, x.Units);
            var i = IScalarValue.AsInt(n);
            return new(RealSpecialFunctions.BesselIn(i, x.D));
        }

        private static RealValue BesselKn(in RealValue n, in RealValue x)
        {
            CheckFunctionUnits("BesselKn", n.Units, x.Units);
            var i = IScalarValue.AsInt(n);
            return new(RealSpecialFunctions.BesselKn(i, x.D));
        }

        private static RealValue BesselIv(in RealValue x, in RealValue nu)
        {
            CheckFunctionUnits("BesselIv", x.Units, nu.Units);
            RealSpecialFunctions.BesselIK(x.D, nu.D, out double iv, out _, out _, out _);
            return new(iv);
        }
        private static RealValue BesselKv(in RealValue x, in RealValue nu)
        {
            CheckFunctionUnits("BesselKv", x.Units, nu.Units);
            RealSpecialFunctions.BesselIK(x.D, nu.D, out _, out double kv, out _, out _);
            return new(kv);
        }

        private static RealValue BesselIvp(in RealValue x, in RealValue nu)
        {
            CheckFunctionUnits("BesselIvp", x.Units, nu.Units);
            RealSpecialFunctions.BesselIK(x.D, nu.D, out _, out _, out double ivp, out _);
            return new(ivp);
        }

        private static RealValue BesselKvp(in RealValue x, in RealValue nu)
        {
            CheckFunctionUnits("BesselKvp", x.Units, nu.Units);
            RealSpecialFunctions.BesselIK(x.D, nu.D, out _, out _, out _, out double kvp);
            return new(kvp);
        }

        private static RealValue LambertW(in RealValue x)
        {
            CheckFunctionUnits("LambertW", x.Units);
            return new(RealSpecialFunctions.LambertW(x.D));
        }
    }
}
