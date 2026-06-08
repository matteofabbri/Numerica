using System.Globalization;
using System.Numerics;
using Numerica.Parsing;

namespace Numerica;

/// <summary>
/// The front-door numeric type: a number described by an EXPRESSION rather than a
/// stored value. You build it from a formula string -- <c>new Numeric("sqrt(2)*sqrt(2)")</c>
/// -- and it stays a suspended calculation (it holds the <see cref="Parsing.Expr"/>
/// tree) until you ask for a value. Arithmetic just grows the tree, so nothing is
/// evaluated until you call one of the conversions.
///
/// Asking for a value picks the right level of the tower:
///   - <see cref="AsRational"/>   -> <see cref="BigRational"/>   (exact; throws if irrational)
///   - <see cref="AsIrrational"/> -> <see cref="BigIrrational"/> (roots, pi, e, exp/ln/trig)
///   - <see cref="AsComplex"/>    -> <see cref="BigComplex"/>    (anything, incl. the unit i)
///
/// It implements .NET's generic-math <see cref="INumber{TSelf}"/>, so it composes with
/// the standard operators and APIs.
///
/// Equality and ordering are EXACT and decidable whenever the formula is algebraic
/// (rationals, +, -, *, /, integer powers and roots of rationals): the comparison
/// drops down to a minimal-polynomial + isolating-interval engine (see the internal
/// <c>AlgebraicReal</c>). For transcendental formulas (pi, e, exp, ln, trig), where
/// exact == is undecidable (Richardson's theorem), it falls back to a high-precision
/// numeric comparison.
/// </summary>
public sealed class Numeric : INumber<Numeric>
{
    private readonly Expr _expr;
    private BigComplex? _value; // memoized evaluation (the "becomes a value when asked" step)

    public Numeric(string formula) : this(Expr.Parse(formula)) { }
    public Numeric(BigInteger value) : this(new BigRational(value)) { }
    public Numeric(long value) : this(new BigRational(value)) { }

    /// <summary>A date/time becomes its UTC tick count (100 ns since 0001-01-01 UTC).</summary>
    public Numeric(DateTime value) : this(new BigRational(value.ToUniversalTime().Ticks)) { }
    public Numeric(DateTimeOffset value) : this(new BigRational(value.UtcTicks)) { }

    internal Numeric(BigRational value) : this(new Expr.Number(value)) { }
    internal Numeric(Expr expression) => _expr = expression;

    /// <summary>The underlying expression tree (the suspended calculation).</summary>
    internal Expr Expression => _expr;

    /// <summary>The original formula, e.g. "sqrt(2) * sqrt(2)".</summary>
    public string Formula => _expr.ToString()!;

    /// <summary>
    /// A compact, round-trippable string of the COMPUTED value rather than the original
    /// formula. When the value collapses to an exact rational — integers, decimals, dates
    /// as ticks, and algebraic results like <c>sqrt(2)*sqrt(2) → 2</c> or <c>2/6 → 1/3</c>
    /// — it is the reduced <c>numerator/denominator</c>, so equal numbers share one
    /// representation and reload without re-evaluating the formula. Values that stay
    /// irrational or complex (<c>sqrt(2)</c>, <c>pi</c>, ...) keep their <see cref="Formula"/>,
    /// the only exact finite form. Reconstruct either with <c>new Numeric(text)</c>.
    /// </summary>
    public string ToValueString()
        => TryRationalValue(out BigRational r) ? r.ToString() : Formula;

    // True (with the reduced value) when the evaluated number is a plain real rational.
    private bool TryRationalValue(out BigRational value)
    {
        BigComplex v = Value;
        if (v.Imaginary.TryGetRational(out BigRational im) && im.IsZero
            && v.Real.TryGetRational(out value))
            return true;
        value = BigRational.Zero;
        return false;
    }

    private BigComplex Value => _value ??= Evaluate();

    // Prefer the real (BigIrrational) interpretation -- it supports the full set of
    // functions; fall back to the complex one only when the formula uses the unit i.
    private BigComplex Evaluate()
    {
        try { return BigComplex.FromReal(_expr.ToIrrational()); }
        catch (NotSupportedException) { return _expr.ToComplex(); }
    }

    // ---------- Conversions (internal: the concrete number types live under the hood) ----------

    internal BigRational AsRational() => _expr.ToRational();
    internal BigIrrational AsIrrational() => _expr.ToIrrational();
    internal BigComplex AsComplex() => Value;

    // ---------- Classification (the public way to ask "what kind of number is this?") ----------

    /// <summary>True when the value has a non-zero imaginary part.</summary>
    public bool IsComplex => Value.Imaginary.SignApprox(DefaultPrecision) != 0;

    /// <summary>
    /// True when the value is provably a rational number. Decided exactly for algebraic
    /// formulas; a transcendental formula that happens to be rational is not detected
    /// (that is undecidable in general).
    /// </summary>
    public bool IsRational => !IsComplex && TryAsAlgebraic(_expr, out AlgebraicReal a) && a.IsRational;

    /// <summary>True when the value is real but not (provably) rational.</summary>
    public bool IsIrrational => !IsComplex && !IsRational;

    public string ToDecimalString(int digits)
    {
        BigComplex v = Value;
        if (v.Imaginary.SignApprox(DefaultPrecision) == 0)
            return v.Real.ToDecimalString(digits);
        string im = v.Imaginary.ToDecimalString(digits);
        string sign = im.StartsWith('-') ? " - " : " + ";
        return $"{v.Real.ToDecimalString(digits)}{sign}{(im.StartsWith('-') ? im[1..] : im)}i";
    }

    private const int DefaultPrecision = 256;

    // ---------- Arithmetic (lazy: only grows the tree) ----------

    public static Numeric operator +(Numeric a, Numeric b) => new(new Expr.Binary('+', a._expr, b._expr));
    public static Numeric operator -(Numeric a, Numeric b) => new(new Expr.Binary('-', a._expr, b._expr));
    public static Numeric operator *(Numeric a, Numeric b) => new(new Expr.Binary('*', a._expr, b._expr));
    public static Numeric operator /(Numeric a, Numeric b) => new(new Expr.Binary('/', a._expr, b._expr));
    public static Numeric operator -(Numeric a) => new(new Expr.Negate(a._expr));
    public static Numeric operator +(Numeric a) => a;
    public static Numeric operator ++(Numeric a) => a + One;
    public static Numeric operator --(Numeric a) => a - One;
    public static Numeric operator %(Numeric a, Numeric b)
        => throw new NotSupportedException("Modulus is not defined for Numeric.");

    public static implicit operator Numeric(int value) => new(new BigRational(value));

    // ---------- Equality and ordering (numeric, up to DefaultPrecision) ----------

    public bool Equals(Numeric? other)
    {
        if (other is null) return false;
        // Exact, decidable path when both formulas are algebraic.
        if (TryAsAlgebraic(_expr, out AlgebraicReal a) && TryAsAlgebraic(other._expr, out AlgebraicReal b))
            return a.Equals(b);
        // Transcendental: high-precision numeric comparison (exact == is undecidable here).
        return Value.ApproximatelyEquals(other.Value, DefaultPrecision);
    }

    public override bool Equals(object? obj) => obj is Numeric n && Equals(n);

    /// <summary>
    /// Hashes the VALUE, not the formula, so numbers that are equal yet carry different
    /// formulas (<c>2/6</c> and <c>1/3</c>, <c>sqrt(2)</c> and <c>sqrt(8)/2</c>) share a
    /// bucket. Each part is evaluated at the comparison precision and its noisy low bits
    /// are dropped, so two values equal up to <see cref="DefaultPrecision"/> collapse to
    /// the same bucket. Exact, decidable consistency with <c>==</c> is impossible for
    /// transcendentals (Richardson's theorem) — this matches equality up to the same
    /// precision <c>==</c> itself uses, and is only ever a hashing hint.
    /// </summary>
    public override int GetHashCode()
    {
        BigComplex v = Value;
        return HashCode.Combine(Bucket(v.Real), Bucket(v.Imaginary));
    }

    // Evaluate x*2^prec (within ~1 ulp), then drop the low bits the comparison tolerance
    // could wobble, so values equal up to DefaultPrecision yield the same bucket.
    private const int HashDropBits = 96;
    private static int Bucket(BigIrrational x)
        => (x.Compile()(DefaultPrecision) >> HashDropBits).GetHashCode();

    public int CompareTo(Numeric? other)
    {
        if (other is null) return 1;
        if (TryAsAlgebraic(_expr, out AlgebraicReal a) && TryAsAlgebraic(other._expr, out AlgebraicReal b))
            return a.CompareTo(b);
        int byReal = Value.Real.CompareApprox(other.Value.Real, DefaultPrecision);
        return byReal != 0 ? byReal : Value.Imaginary.CompareApprox(other.Value.Imaginary, DefaultPrecision);
    }

    public int CompareTo(object? obj) => obj is Numeric n ? CompareTo(n) : 1;

    public static bool operator ==(Numeric? a, Numeric? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(Numeric? a, Numeric? b) => !(a == b);
    public static bool operator <(Numeric a, Numeric b) => a.CompareTo(b) < 0;
    public static bool operator >(Numeric a, Numeric b) => a.CompareTo(b) > 0;
    public static bool operator <=(Numeric a, Numeric b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Numeric a, Numeric b) => a.CompareTo(b) >= 0;

    // ---------- INumberBase identities ----------

    public static Numeric One => new(BigRational.One);
    public static Numeric Zero => new(BigRational.Zero);
    public static int Radix => 2;
    public static Numeric AdditiveIdentity => Zero;
    public static Numeric MultiplicativeIdentity => One;

    public static Numeric Abs(Numeric value) => new(new Expr.Function("abs", new[] { value._expr }));

    // ---------- INumberBase classification ----------

    private static bool Real(Numeric v) => v.Value.Imaginary.SignApprox(DefaultPrecision) == 0;

    public static bool IsZero(Numeric value) => value.Value.ApproximatelyEquals(BigComplex.Zero, DefaultPrecision);
    public static bool IsNegative(Numeric value) => Real(value) && value.Value.Real.SignApprox(DefaultPrecision) < 0;
    public static bool IsPositive(Numeric value) => Real(value) && value.Value.Real.SignApprox(DefaultPrecision) > 0;
    public static bool IsRealNumber(Numeric value) => Real(value);
    public static bool IsComplexNumber(Numeric value) => !Real(value);
    public static bool IsImaginaryNumber(Numeric value) => !Real(value) && value.Value.Real.SignApprox(DefaultPrecision) == 0;
    public static bool IsFinite(Numeric value) => true;
    public static bool IsInfinity(Numeric value) => false;
    public static bool IsPositiveInfinity(Numeric value) => false;
    public static bool IsNegativeInfinity(Numeric value) => false;
    public static bool IsNaN(Numeric value) => false;
    public static bool IsNormal(Numeric value) => !IsZero(value);
    public static bool IsSubnormal(Numeric value) => false;
    public static bool IsCanonical(Numeric value) => true;
    public static bool IsInteger(Numeric value) => TryInteger(value, out _);
    public static bool IsEvenInteger(Numeric value) => TryInteger(value, out BigInteger i) && i.IsEven;
    public static bool IsOddInteger(Numeric value) => TryInteger(value, out BigInteger i) && !i.IsEven;

    private static bool TryInteger(Numeric value, out BigInteger result)
    {
        try
        {
            BigRational r = value.AsRational();
            if (r.IsInteger) { result = r.Numerator; return true; }
        }
        catch (NotSupportedException) { }
        catch (DivideByZeroException) { }
        result = BigInteger.Zero;
        return false;
    }

    public static Numeric MaxMagnitude(Numeric x, Numeric y) => CompareMagnitude(x, y) >= 0 ? x : y;
    public static Numeric MaxMagnitudeNumber(Numeric x, Numeric y) => MaxMagnitude(x, y);
    public static Numeric MinMagnitude(Numeric x, Numeric y) => CompareMagnitude(x, y) <= 0 ? x : y;
    public static Numeric MinMagnitudeNumber(Numeric x, Numeric y) => MinMagnitude(x, y);

    private static int CompareMagnitude(Numeric x, Numeric y)
        => x.Value.MagnitudeSquared().CompareApprox(y.Value.MagnitudeSquared(), DefaultPrecision);

    // ---------- Parsing (string -> Numeric is the whole point) ----------

    public static Numeric Parse(string s, IFormatProvider? provider) => new(s);
    public static Numeric Parse(string s, NumberStyles style, IFormatProvider? provider) => new(s);
    public static Numeric Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(s.ToString());
    public static Numeric Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => new(s.ToString());

    public static bool TryParse(string? s, IFormatProvider? provider, out Numeric result) => TryParseCore(s, out result);
    public static bool TryParse(string? s, NumberStyles style, IFormatProvider? provider, out Numeric result) => TryParseCore(s, out result);
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Numeric result) => TryParseCore(s.ToString(), out result);
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out Numeric result) => TryParseCore(s.ToString(), out result);

    private static bool TryParseCore(string? s, out Numeric result)
    {
        if (!string.IsNullOrWhiteSpace(s))
        {
            try { result = new Numeric(s); return true; }
            catch (Exception) { /* not a valid formula */ }
        }
        result = Zero;
        return false;
    }

    // ---------- Conversions to/from other INumberBase types ----------

    static bool INumberBase<Numeric>.TryConvertFromChecked<TOther>(TOther value, out Numeric result) => TryConvertFrom(value, out result);
    static bool INumberBase<Numeric>.TryConvertFromSaturating<TOther>(TOther value, out Numeric result) => TryConvertFrom(value, out result);
    static bool INumberBase<Numeric>.TryConvertFromTruncating<TOther>(TOther value, out Numeric result) => TryConvertFrom(value, out result);
    static bool INumberBase<Numeric>.TryConvertToChecked<TOther>(Numeric value, out TOther result) => TryConvertTo(value, out result);
    static bool INumberBase<Numeric>.TryConvertToSaturating<TOther>(Numeric value, out TOther result) => TryConvertTo(value, out result);
    static bool INumberBase<Numeric>.TryConvertToTruncating<TOther>(Numeric value, out TOther result) => TryConvertTo(value, out result);

    private static bool TryConvertFrom<TOther>(TOther value, out Numeric result) where TOther : INumberBase<TOther>
    {
        if (TOther.IsInteger(value))
        {
            result = new Numeric(BigInteger.CreateChecked(value));
            return true;
        }
        if (!TOther.IsFinite(value)) { result = Zero; return false; }
        result = new Numeric(RationalFromDouble(double.CreateChecked(value)));
        return true;
    }

    private static bool TryConvertTo<TOther>(Numeric value, out TOther result) where TOther : INumberBase<TOther>
    {
        if (!Real(value)) { result = TOther.Zero; return false; }
        if (TryInteger(value, out BigInteger whole))
        {
            result = TOther.CreateChecked(whole);
            return true;
        }
        double d = double.Parse(value.Value.Real.ToDecimalString(17), CultureInfo.InvariantCulture);
        result = TOther.CreateChecked(d);
        return true;
    }

    private static BigRational RationalFromDouble(double d)
    {
        if (d == 0) return BigRational.Zero;
        long bits = BitConverter.DoubleToInt64Bits(d);
        bool negative = bits < 0;
        int exponent = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xF_FFFF_FFFF_FFFF;
        if (exponent == 0) exponent++;
        else mantissa |= 0x10_0000_0000_0000;
        exponent -= 1075;

        BigInteger numerator = mantissa;
        if (negative) numerator = -numerator;
        return exponent >= 0
            ? new BigRational(numerator << exponent, BigInteger.One)
            : new BigRational(numerator, BigInteger.One << -exponent);
    }

    // ---------- Exact algebraic decision (this is the former BigAlgebric, folded in) ----------

    // Realizes the expression as a root of an integer polynomial when possible, which
    // grants exact, decidable == and <. Returns false for transcendental formulas.
    private static bool TryAsAlgebraic(Expr e, out AlgebraicReal result)
    {
        try
        {
            if (TryAlgebraicCore(e, out AlgebraicReal? r) && r is not null) { result = r; return true; }
        }
        catch { /* any failure -> not usable as an exact algebraic value */ }
        result = null!;
        return false;
    }

    private static bool TryAlgebraicCore(Expr e, out AlgebraicReal? result)
    {
        switch (e)
        {
            case Expr.Number n:
                result = AlgebraicReal.FromRational(n.Value);
                return true;

            case Expr.Negate neg when TryAlgebraicCore(neg.Operand, out AlgebraicReal? inner):
                result = -inner!;
                return true;

            case Expr.Binary b:
                return TryAlgebraicBinary(b, out result);

            case Expr.Function f when f.Name == "sqrt" && f.Arguments.Count == 1 && TryRational(f.Argument, out BigRational radicand) && radicand.Sign >= 0:
                result = AlgebraicReal.Sqrt(radicand);
                return true;
        }
        result = null;
        return false;
    }

    private static bool TryAlgebraicBinary(Expr.Binary b, out AlgebraicReal? result)
    {
        result = null;

        if (b.Operator == '^')
        {
            // base^n with integer n: repeated multiplication of an algebraic base.
            if (b.Right is Expr.Number en && en.Value.IsInteger && TryAlgebraicCore(b.Left, out AlgebraicReal? baseValue))
            {
                result = IntegerPower(baseValue!, (int)en.Value.Numerator);
                return true;
            }
            // r^(p/q) with rational base r: (r^p)^(1/q) is the q-th root of a rational.
            if (b.Right is Expr.Number ep && TryRational(b.Left, out BigRational rbase))
            {
                BigRational radicand = BigRational.Pow(rbase, (int)ep.Value.Numerator);
                int degree = (int)ep.Value.Denominator;
                if (radicand.Sign >= 0 || degree % 2 == 1)
                {
                    result = AlgebraicReal.Root(radicand, degree);
                    return true;
                }
            }
            return false;
        }

        if (!TryAlgebraicCore(b.Left, out AlgebraicReal? left) || !TryAlgebraicCore(b.Right, out AlgebraicReal? right))
            return false;

        switch (b.Operator)
        {
            case '+': result = left! + right!; return true;
            case '-': result = left! - right!; return true;
            case '*': result = left! * right!; return true;
            case '/':
                if (right!.Sign() == 0) return false; // division by zero is not a usable value
                result = left! / right;
                return true;
        }
        return false;
    }

    private static AlgebraicReal IntegerPower(AlgebraicReal value, int exponent)
    {
        if (exponent == 0) return AlgebraicReal.FromInteger(BigInteger.One);
        bool negative = exponent < 0;
        int e = Math.Abs(exponent);
        AlgebraicReal result = AlgebraicReal.FromInteger(BigInteger.One);
        AlgebraicReal factor = value;
        while (e > 0)
        {
            if ((e & 1) == 1) result *= factor;
            e >>= 1;
            if (e > 0) factor *= factor;
        }
        return negative ? AlgebraicReal.FromInteger(BigInteger.One) / result : result;
    }

    private static bool TryRational(Expr e, out BigRational value)
    {
        try { value = e.ToRational(); return true; }
        catch (NotSupportedException) { value = BigRational.Zero; return false; }
        catch (DivideByZeroException) { value = BigRational.Zero; return false; }
    }

    // ---------- Formatting ----------

    public override string ToString() => ToDecimalString(20);
    public string ToString(string? format, IFormatProvider? formatProvider) => ToDecimalString(DigitsFromFormat(format));

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        string text = ToDecimalString(DigitsFromFormat(format.ToString()));
        if (text.AsSpan().TryCopyTo(destination)) { charsWritten = text.Length; return true; }
        charsWritten = 0;
        return false;
    }

    private static int DigitsFromFormat(string? format)
        => !string.IsNullOrEmpty(format) && int.TryParse(format, out int n) && n >= 0 ? n : 20;
}
