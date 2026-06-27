using System;

namespace Calcpad.Core
{
    internal readonly struct RealValue : IEquatable<RealValue>, IComparable<RealValue>, IScalarValue
    {
        internal const double LogicalZero = 1e-12;
        internal readonly double D;
        internal readonly Unit Units;
        internal readonly bool IsUnit;
        internal static readonly RealValue Zero = new(0d);
        internal static readonly RealValue One = new(1d);
        internal static readonly RealValue NaN = new(double.NaN);
        internal static readonly RealValue PositiveInfinity = new(double.PositiveInfinity);
        internal static readonly RealValue NegativeInfinity = new(double.NegativeInfinity);
        double IScalarValue.Re => D;
        double IScalarValue.Im => 0d;
        Unit IScalarValue.Units => Units;
        bool IScalarValue.IsUnit => IsUnit;
        bool IScalarValue.IsReal => true;
        bool IScalarValue.IsComplex => false;
        Complex IScalarValue.Complex => new(D, 0d);
        bool IScalarValue.IsComposite() => Unit.IsComposite(D, Units);
        RealValue IScalarValue.AsReal() => this;
        ComplexValue IScalarValue.AsComplex() => new(D, Units, IsUnit);
        internal RealValue(double number)
        {
            D = number;
        }

        internal RealValue(double number, Unit units)
        {
            D = number;
            Units = units;
        }

        internal RealValue(Unit units)
        {
            D = 1d;
            Units = units;
            IsUnit = true;
        }

        internal RealValue(in double number, Unit units, bool isUnit) : this(number, units)
        {
            IsUnit = isUnit;
        }

        public override int GetHashCode() => HashCode.Combine(D, Units);

        public override bool Equals(object obj)
        {
            if (obj is RealValue real)
                return Equals(real);

            return false;
        }

        public bool Equals(RealValue other)
        {
            if (Units is null)
                return other.Units is null &&
                    D.Equals(other.D);

            if (other.Units is null)
                return false;

            return D.Equals(other.D) &&
                Units.Equals(other.Units);
        }

        internal bool AlmostEquals(in RealValue other)
        {
            if (ReferenceEquals(Units, other.Units))
                return D.AlmostEquals(other.D);

            if (!Units.IsConsistent(other.Units))
                return false;

            var d = Units.ConvertTo(other.Units);
            return D.AlmostEquals(other.D * d);
        }

        public int CompareTo(RealValue other)
        {
            // Zero is the additive identity — comparing against 0 is well-defined
            // regardless of units (e.g., "5 m > 0" or "0 < 3 kN/m²").
            if (D == 0d || other.D == 0d)
                return D.CompareTo(other.D);
            var d = Unit.Convert(Units, other.Units, ',');
            return D.CompareTo(other.D * d);
        }

        public override string ToString()
        {
            if (IsUnit)
                return Units.Text;

            var s = Units is null ? string.Empty : Units.Text;
            return $"{D}{s}";
        }

        internal static RealValue Abs(RealValue value) => new(Math.Abs(value.D), value.Units);

        internal static RealValue Sqrt(RealValue value)
        {
            var u = value.Units;
            double d;
            if (u is not null && u.IsDimensionless)
            {
                d = value.D * u.GetDimensionlessFactor();
                return new(Math.Sqrt(d));
            }
            d = Math.Sqrt(value.D);
            return u is null ?
                new(d) :
                new(d, u.Pow(0.5f));
        }

        internal static RealValue Pow2(RealValue value)
        {
            var u = value.Units;
            double d;
            if (u is not null && u.IsDimensionless)
            {
                d = value.D * u.GetDimensionlessFactor();
                return new(d * d);
            }

            d = value.D;
            return u is null ?
                new(d * d) :
                new(d * d, u.Pow(2));
        }


        internal bool IsComposite() => Unit.IsComposite(D, Units);

        public static RealValue operator -(RealValue a) => new(-a.D, a.Units, a.IsUnit);

        public static RealValue operator +(RealValue a, RealValue b)
        {
            // Zero is compatible with any unit (0 + X = X, X + 0 = X)
            if (b.D == 0d) return a;
            if (a.D == 0d) return new(b.D, b.Units);
            // Dimensionless promotion: non-zero unitless added to value with units
            // inherits those units (penalty constants, k_p = 10^20). We DO NOT
            // short-circuit the both-null case here — Unit.Convert(null,null,'+')
            // correctly returns 1 and the standard path below produces a unitless
            // sum. Adding an explicit both-null branch (returning null Units) was
            // observed to cause downstream regressions where some matrix/cell APIs
            // distinguish "computed and tagged as dimensionless" from "fell through
            // arithmetic with null Units" — the standard Convert path preserves the
            // legacy semantics other code depends on.
            if (a.Units is null && b.Units is not null) return new(a.D + b.D, b.Units);
            if (a.Units is not null && b.Units is null) return new(a.D + b.D, a.Units);
            // Same / consistent units: standard conversion path. This also handles
            // the both-null case via Unit.Convert(null, null, '+') returning 1.
            if (a.Units is null || a.Units.IsConsistent(b.Units))
                return new(a.D + b.D * Unit.Convert(a.Units, b.Units, '+'), a.Units);
            // Mismatched non-null units (e.g. mixed-units stiffness matrix where K(1,1)
            // is kN/m and K(1,4) is kNm). Add the raw numerical values and keep the
            // LHS units.
            return new(a.D + b.D, a.Units);
        }

        public static RealValue operator -(RealValue a, RealValue b)
        {
            if (b.D == 0d) return a;
            if (a.D == 0d) return new(-b.D, b.Units);
            // Dimensionless promotion (mirrors operator+).
            if (a.Units is null && b.Units is not null) return new(a.D - b.D, b.Units);
            if (a.Units is not null && b.Units is null) return new(a.D - b.D, a.Units);
            if (a.Units is null || a.Units.IsConsistent(b.Units))
                return new(a.D - b.D * Unit.Convert(a.Units, b.Units, '-'), a.Units);
            // Mismatched non-null units: keep LHS unit, subtract raw values.
            return new(a.D - b.D, a.Units);
        }

        public static RealValue operator *(RealValue a, RealValue b)
        {
            if (a.Units is null)
            {
                if (b.Units is not null && b.Units.IsDimensionless && !b.IsUnit)
                    return new(a.D * b.D * b.Units.GetDimensionlessFactor(), null);

                return new(a.D * b.D, b.Units);
            }
            var uc = Unit.Multiply(a.Units, b.Units, out var d);
            return new(a.D * b.D * d, uc);
        }

        public static RealValue Multiply(in RealValue a, in RealValue b)
        {
            if (a.Units is null)
            {
                if (b.Units is not null && b.Units.IsDimensionless && !b.IsUnit)
                    return new(a.D * b.D * b.Units.GetDimensionlessFactor(), null);

                return new(a.D * b.D, b.Units);
            }
            var uc = Unit.Multiply(a.Units, b.Units, out var d, b.IsUnit);
            var isUnit = a.IsUnit && b.IsUnit && uc is not null;
            return new(a.D * b.D * d, uc, isUnit);
        }

        public static RealValue operator /(RealValue a, RealValue b)
        {
            var uc = Unit.Divide(a.Units, b.Units, out var d);
            return new(a.D / b.D * d, uc);
        }

        public static RealValue Divide(in RealValue a, in RealValue b)
        {
            var uc = Unit.Divide(a.Units, b.Units, out var d, b.IsUnit);
            var isUnit = a.IsUnit && b.IsUnit && uc is not null;
            return new(a.D / b.D * d, uc, isUnit);
        }

        public static RealValue operator *(RealValue a, double b) =>
            new(a.D * b, a.Units);

        public static RealValue operator %(RealValue a, RealValue b)
        {
            if (b.Units is not null)
                throw Exceptions.CannotEvaluateRemainder(Unit.GetText(a.Units), Unit.GetText(b.Units));

            return new(a.D % b.D, a.Units);
        }

        internal static RealValue IntDiv(in RealValue a, in RealValue b)
        {
            var uc = Unit.Divide(a.Units, b.Units, out var d);
            bool isUnit = a.IsUnit && b.IsUnit && uc is not null;
            var c = b.D == 0d ?
                double.NaN :
                Math.Truncate(a.D / b.D * d);
            return new(c, uc, isUnit);
        }

        // Zero is compatible with any unit for comparisons — "5 m > 0" makes sense
        // regardless of what 0 is (additive identity has no inherent unit).
        public static RealValue operator ==(RealValue a, RealValue b) =>
            (a.D == 0d || b.D == 0d
                ? a.D.AlmostEquals(b.D)
                : a.D.AlmostEquals(b.D * Unit.Convert(a.Units, b.Units, '≡')))
            ? One : Zero;

        public static RealValue operator !=(RealValue a, RealValue b) =>
            (a.D == 0d || b.D == 0d
                ? a.D.AlmostEquals(b.D)
                : a.D.AlmostEquals(b.D * Unit.Convert(a.Units, b.Units, '≠')))
            ? Zero : One;

        // Compare numerically, applying the same "lenient mismatched units"
        // policy as operator + / -: if any side is zero, units are ignored; if
        // units are consistent, normal conversion; otherwise compare the raw
        // numerical magnitudes (LHS units win). This is the relaxation needed
        // for things like `#if vk > vmmax` inside FEM loops where the matrix
        // cells can end up with subtly different units across iterations.
        private static double ConvertForCompare(RealValue a, RealValue b, char op)
        {
            if (a.D == 0d || b.D == 0d) return b.D;
            if (a.Units is null || b.Units is null) return b.D;
            if (a.Units.IsConsistent(b.Units)) return b.D * Unit.Convert(a.Units, b.Units, op);
            return b.D;   // mismatched units: compare raw numerical magnitudes
        }

        public static RealValue operator <(RealValue a, RealValue b)
        {
            var c = a.D;
            var d = ConvertForCompare(a, b, '<');
            return c < d && !c.AlmostEquals(d) ? One : Zero;
        }

        public static RealValue operator >(RealValue a, RealValue b)
        {
            var c = a.D;
            var d = ConvertForCompare(a, b, '>');
            return c > d && !c.AlmostEquals(d) ? One : Zero;
        }

        public static RealValue operator <=(RealValue a, RealValue b)
        {
            var c = a.D;
            var d = ConvertForCompare(a, b, '≤');
            return c <= d || c.AlmostEquals(d) ? One : Zero;
        }

        public static RealValue operator >=(RealValue a, RealValue b)
        {
            var c = a.D;
            var d = ConvertForCompare(a, b, '≥');
            return c >= d || c.AlmostEquals(d) ? One : Zero;
        }

        public static RealValue operator &(RealValue a, RealValue b) =>
            Math.Abs(a.D) < LogicalZero || Math.Abs(b.D) < LogicalZero ? Zero : One;

        public static RealValue operator |(RealValue a, RealValue b) =>
            Math.Abs(a.D) >= LogicalZero || Math.Abs(b.D) >= LogicalZero ? One : Zero;

        public static RealValue operator ^(RealValue a, RealValue b) =>
            (Math.Abs(a.D) >= LogicalZero) != (Math.Abs(b.D) >= LogicalZero) ? One : Zero;
    }
}