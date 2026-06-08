using System.Numerics;

namespace SuperNumbers;

/// <summary>
/// Complex number whose real and imaginary parts are each an exact
/// <see cref="BigIrrational"/> symbolic tree.
///
/// Building on the irrationals (rather than on plain rationals) means the parts can
/// themselves carry roots and constants, so identities stay exact: |3 + 4i| folds to
/// 5, i^2 folds to -1, and arithmetic never loses precision before you ask for digits.
/// </summary>
public sealed class BigComplex
{
    public BigIrrational Real { get; }
    public BigIrrational Imaginary { get; }

    public BigComplex(BigIrrational real, BigIrrational imaginary)
    {
        Real = real;
        Imaginary = imaginary;
    }

    // ---------- Constants and constructors ----------

    public static readonly BigComplex Zero = new(BigIrrational.Zero, BigIrrational.Zero);
    public static readonly BigComplex One = new(BigIrrational.One, BigIrrational.Zero);

    /// <summary>The imaginary unit i.</summary>
    public static readonly BigComplex ImaginaryUnit = new(BigIrrational.Zero, BigIrrational.One);

    public static BigComplex FromReal(BigIrrational real) => new(real, BigIrrational.Zero);
    public static BigComplex FromInteger(BigInteger value) => FromReal(BigIrrational.FromInteger(value));
    public static BigComplex FromRational(BigRational value) => FromReal(BigIrrational.FromRational(value));

    public static implicit operator BigComplex(BigIrrational real) => FromReal(real);
    public static implicit operator BigComplex(BigRational value) => FromRational(value);
    public static implicit operator BigComplex(int value) => FromInteger(value);

    // ---------- Arithmetic operators ----------

    public static BigComplex operator +(BigComplex a, BigComplex b)
        => new(a.Real + b.Real, a.Imaginary + b.Imaginary);

    public static BigComplex operator -(BigComplex a, BigComplex b)
        => new(a.Real - b.Real, a.Imaginary - b.Imaginary);

    public static BigComplex operator -(BigComplex a) => new(-a.Real, -a.Imaginary);

    public static BigComplex operator *(BigComplex a, BigComplex b)
        // (a + bi)(c + di) = (ac - bd) + (ad + bc)i
        => new(
            a.Real * b.Real - a.Imaginary * b.Imaginary,
            a.Real * b.Imaginary + a.Imaginary * b.Real);

    public static BigComplex operator /(BigComplex a, BigComplex b)
    {
        // Multiply by the conjugate of b and divide by |b|^2.
        BigIrrational denom = b.Real * b.Real + b.Imaginary * b.Imaginary;
        BigIrrational real = (a.Real * b.Real + a.Imaginary * b.Imaginary) / denom;
        BigIrrational imag = (a.Imaginary * b.Real - a.Real * b.Imaginary) / denom;
        return new BigComplex(real, imag);
    }

    // ---------- Derived quantities ----------

    public BigComplex Conjugate() => new(Real, -Imaginary);

    /// <summary>|z|^2 = re^2 + im^2 (stays exact).</summary>
    public BigIrrational MagnitudeSquared() => Real * Real + Imaginary * Imaginary;

    /// <summary>|z| = sqrt(re^2 + im^2).</summary>
    public BigIrrational Magnitude() => BigIrrational.Sqrt(MagnitudeSquared());

    /// <summary>Integer power by repeated multiplication (exact, no logarithm).</summary>
    public static BigComplex Power(BigComplex z, int exponent)
    {
        if (exponent == 0) return One;

        bool negative = exponent < 0;
        int e = Math.Abs(exponent);
        BigComplex result = One;
        BigComplex baseValue = z;
        while (e > 0)
        {
            if ((e & 1) == 1) result *= baseValue;
            e >>= 1;
            if (e > 0) baseValue *= baseValue;
        }
        return negative ? One / result : result;
    }

    /// <summary>General complex power z^w = exp(w * ln z).</summary>
    public static BigComplex Power(BigComplex z, BigComplex w) => Exp(w * Ln(z));

    // ---------- Transcendental functions ----------

    /// <summary>exp(a + bi) = e^a (cos b + i sin b).</summary>
    public static BigComplex Exp(BigComplex z)
    {
        BigIrrational ea = BigIrrational.Exp(z.Real);
        return new BigComplex(ea * BigIrrational.Cos(z.Imaginary), ea * BigIrrational.Sin(z.Imaginary));
    }

    /// <summary>Principal logarithm: ln|z| + i*arg(z).</summary>
    public static BigComplex Ln(BigComplex z)
    {
        // ln|z| = (1/2) ln(re^2 + im^2)
        BigIrrational lnMagnitude = BigIrrational.Ln(z.MagnitudeSquared()) / 2;
        BigIrrational argument = BigIrrational.Atan2(z.Imaginary, z.Real);
        return new BigComplex(lnMagnitude, argument);
    }

    /// <summary>sin(a + bi) = sin a cosh b + i cos a sinh b.</summary>
    public static BigComplex Sin(BigComplex z)
    {
        (BigIrrational cosh, BigIrrational sinh) = Hyperbolic(z.Imaginary);
        return new BigComplex(
            BigIrrational.Sin(z.Real) * cosh,
            BigIrrational.Cos(z.Real) * sinh);
    }

    /// <summary>cos(a + bi) = cos a cosh b - i sin a sinh b.</summary>
    public static BigComplex Cos(BigComplex z)
    {
        (BigIrrational cosh, BigIrrational sinh) = Hyperbolic(z.Imaginary);
        return new BigComplex(
            BigIrrational.Cos(z.Real) * cosh,
            -(BigIrrational.Sin(z.Real) * sinh));
    }

    /// <summary>Principal square root.</summary>
    public static BigComplex Sqrt(BigComplex z)
    {
        BigIrrational magnitude = z.Magnitude();
        BigIrrational real = BigIrrational.Sqrt((magnitude + z.Real) / 2);
        BigIrrational imagAbs = BigIrrational.Sqrt((magnitude - z.Real) / 2);
        BigIrrational imag = z.Imaginary.SignApprox() < 0 ? -imagAbs : imagAbs;
        return new BigComplex(real, imag);
    }

    // (cosh b, sinh b) = ((e^b + e^-b)/2, (e^b - e^-b)/2)
    private static (BigIrrational Cosh, BigIrrational Sinh) Hyperbolic(BigIrrational b)
    {
        BigIrrational eb = BigIrrational.Exp(b);
        BigIrrational ebInv = BigIrrational.Exp(-b);
        return ((eb + ebInv) / 2, (eb - ebInv) / 2);
    }

    // ---------- Comparison and formatting ----------

    public bool ApproximatelyEquals(BigComplex other, int prec = 200)
        => Real.ApproximatelyEquals(other.Real, prec)
        && Imaginary.ApproximatelyEquals(other.Imaginary, prec);

    public override string ToString()
    {
        string re = Real.ToString()!;
        string im = Imaginary.ToString()!;

        if (im == "0") return re;

        bool imNegative = im.StartsWith('-');
        string imAbs = imNegative ? im[1..] : im;
        if (imAbs == "1") imAbs = string.Empty; // render "i" rather than "1i"

        if (re == "0")
            return imNegative ? $"-{imAbs}i" : $"{imAbs}i";

        string sign = imNegative ? " - " : " + ";
        return $"{re}{sign}{imAbs}i";
    }
}
