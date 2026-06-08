using System.Globalization;
using System.Numerics;

namespace Numerica;

/// <summary>
/// Exact rational number, stored as <c>Numerator / Denominator</c> in lowest terms
/// with a strictly positive denominator.
///
/// Unlike the closed-form reals in <see cref="BigIrrational"/>, here equality is
/// genuinely exact: the rationals are closed under +, -, *, / and integer powers,
/// so two values are equal iff their reduced fractions match. This is the bottom
/// floor of the tower (rationals -> algebraic irrationals -> complex).
/// </summary>
internal readonly struct BigRational : IEquatable<BigRational>, IComparable<BigRational>
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public static readonly BigRational Zero = new(BigInteger.Zero);
    public static readonly BigRational One = new(BigInteger.One);
    public static readonly BigRational MinusOne = new(BigInteger.MinusOne);

    /// <summary>Builds the rational equal to an integer (denominator 1).</summary>
    public BigRational(BigInteger integer)
    {
        Numerator = integer;
        Denominator = BigInteger.One;
    }

    /// <summary>Builds <c>numerator / denominator</c>, reduced to lowest terms.</summary>
    public BigRational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
            throw new DivideByZeroException("Rational denominator is zero.");

        if (denominator.Sign < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        if (gcd > BigInteger.One)
        {
            numerator /= gcd;
            denominator /= gcd;
        }

        Numerator = numerator;
        Denominator = denominator;
    }

    // ---------- Properties ----------

    public int Sign => Numerator.Sign;
    public bool IsZero => Numerator.IsZero;
    public bool IsInteger => Denominator.IsOne;

    public BigRational Abs => Numerator.Sign < 0 ? new BigRational(-Numerator, Denominator) : this;

    /// <summary>1 / this. Throws when this is zero.</summary>
    public BigRational Reciprocal()
    {
        if (IsZero) throw new DivideByZeroException("Cannot invert zero.");
        return new BigRational(Denominator, Numerator);
    }

    // ---------- Conversions ----------

    public static implicit operator BigRational(BigInteger value) => new(value);
    public static implicit operator BigRational(long value) => new(value);
    public static implicit operator BigRational(int value) => new(value);

    public double ToDouble() => (double)Numerator / (double)Denominator;

    // ---------- Arithmetic operators ----------

    public static BigRational operator +(BigRational a, BigRational b)
        => new(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static BigRational operator -(BigRational a, BigRational b)
        => new(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);

    public static BigRational operator -(BigRational a) => new(-a.Numerator, a.Denominator);

    public static BigRational operator *(BigRational a, BigRational b)
        => new(a.Numerator * b.Numerator, a.Denominator * b.Denominator);

    public static BigRational operator /(BigRational a, BigRational b)
    {
        if (b.IsZero) throw new DivideByZeroException("Division by zero rational.");
        return new BigRational(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
    }

    /// <summary>
    /// Truncated remainder <c>a - b*trunc(a/b)</c>: the result has the sign of <paramref name="a"/>
    /// (the same convention as the C# <c>%</c> operator on integers).
    /// </summary>
    public static BigRational Mod(BigRational a, BigRational b)
    {
        if (b.IsZero) throw new DivideByZeroException("Modulo by zero.");
        BigRational quotient = a / b;
        BigInteger truncated = quotient.Numerator / quotient.Denominator; // toward zero
        return a - b * new BigRational(truncated);
    }

    /// <summary>Integer power (negative exponents allowed for non-zero base).</summary>
    public static BigRational Pow(BigRational value, int exponent)
    {
        if (exponent == 0) return One;
        if (exponent < 0)
        {
            if (value.IsZero) throw new DivideByZeroException("Zero raised to a negative power.");
            return new BigRational(
                BigInteger.Pow(value.Denominator, -exponent),
                BigInteger.Pow(value.Numerator, -exponent));
        }
        return new BigRational(
            BigInteger.Pow(value.Numerator, exponent),
            BigInteger.Pow(value.Denominator, exponent));
    }

    // ---------- Comparison and equality (exact) ----------

    public int CompareTo(BigRational other)
        => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public bool Equals(BigRational other)
        => Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object? obj) => obj is BigRational r && Equals(r);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    public static bool operator ==(BigRational a, BigRational b) => a.Equals(b);
    public static bool operator !=(BigRational a, BigRational b) => !a.Equals(b);
    public static bool operator <(BigRational a, BigRational b) => a.CompareTo(b) < 0;
    public static bool operator >(BigRational a, BigRational b) => a.CompareTo(b) > 0;
    public static bool operator <=(BigRational a, BigRational b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BigRational a, BigRational b) => a.CompareTo(b) >= 0;

    // ---------- Parsing and formatting ----------

    /// <summary>Parses "a", "a/b", "-a/b" (whitespace allowed around the slash).</summary>
    public static BigRational Parse(string text)
    {
        if (TryParse(text, out BigRational value)) return value;
        throw new FormatException($"'{text}' is not a valid rational.");
    }

    public static bool TryParse(string text, out BigRational value)
    {
        value = Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        int slash = text.IndexOf('/');
        if (slash < 0)
        {
            if (!BigInteger.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger whole))
                return false;
            value = new BigRational(whole);
            return true;
        }

        if (!BigInteger.TryParse(text[..slash].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger num))
            return false;
        if (!BigInteger.TryParse(text[(slash + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger den))
            return false;
        if (den.IsZero) return false;

        value = new BigRational(num, den);
        return true;
    }

    public override string ToString()
        => Denominator.IsOne ? Numerator.ToString() : $"{Numerator}/{Denominator}";

    /// <summary>Rounded decimal expansion with the requested number of fractional digits.</summary>
    public string ToDecimalString(int digits)
    {
        if (digits < 0) digits = 0;

        bool negative = Numerator.Sign < 0;
        BigInteger absNum = BigInteger.Abs(Numerator);

        BigInteger pow = BigInteger.Pow(10, digits);
        // round(absNum * 10^digits / den)
        BigInteger scaled = absNum * pow;
        BigInteger quotient = scaled / Denominator;
        BigInteger remainder = scaled - quotient * Denominator;
        if (remainder * 2 >= Denominator) quotient += BigInteger.One;

        BigInteger intPart = quotient / pow;
        BigInteger fracPart = quotient % pow;

        string s = intPart.ToString();
        if (digits > 0)
            s += "." + fracPart.ToString().PadLeft(digits, '0');
        return negative ? "-" + s : s;
    }
}
