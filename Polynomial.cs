using System.Numerics;

namespace SuperNumbers;

/// <summary>
/// Dense univariate polynomial with <see cref="BigRational"/> coefficients,
/// stored low degree first. Used by <see cref="BigAlgebraic"/> for the
/// minimal-polynomial / Sturm machinery that makes comparison of algebraic
/// numbers decidable.
/// </summary>
public sealed class Polynomial
{
    private readonly BigRational[] _coeffs; // index i = coefficient of x^i, trimmed

    public static readonly Polynomial Zero = new(Array.Empty<BigRational>());
    public static readonly Polynomial One = new(new[] { BigRational.One });

    public Polynomial(IEnumerable<BigRational> coefficients)
    {
        BigRational[] c = coefficients.ToArray();
        int n = c.Length;
        while (n > 0 && c[n - 1].IsZero) n--;
        _coeffs = n == c.Length ? c : c[..n];
    }

    /// <summary>x - root.</summary>
    public static Polynomial Linear(BigRational root) => new(new[] { -root, BigRational.One });

    /// <summary>x^n - value.</summary>
    public static Polynomial PowerMinus(int n, BigRational value)
    {
        var c = new BigRational[n + 1];
        for (int i = 0; i <= n; i++) c[i] = BigRational.Zero;
        c[0] = -value;
        c[n] = BigRational.One;
        return new Polynomial(c);
    }

    public int Degree => _coeffs.Length - 1; // -1 for the zero polynomial
    public bool IsZero => _coeffs.Length == 0;
    public BigRational Leading => IsZero ? BigRational.Zero : _coeffs[^1];

    public BigRational this[int i] => i >= 0 && i < _coeffs.Length ? _coeffs[i] : BigRational.Zero;

    public BigRational Evaluate(BigRational x)
    {
        BigRational acc = BigRational.Zero;
        for (int i = _coeffs.Length - 1; i >= 0; i--) acc = acc * x + _coeffs[i];
        return acc;
    }

    public Polynomial Derivative()
    {
        if (Degree < 1) return Zero;
        var c = new BigRational[_coeffs.Length - 1];
        for (int i = 1; i < _coeffs.Length; i++) c[i - 1] = _coeffs[i] * new BigRational(i);
        return new Polynomial(c);
    }

    public static Polynomial operator +(Polynomial a, Polynomial b)
    {
        int n = Math.Max(a._coeffs.Length, b._coeffs.Length);
        var c = new BigRational[n];
        for (int i = 0; i < n; i++) c[i] = a[i] + b[i];
        return new Polynomial(c);
    }

    public static Polynomial operator -(Polynomial a, Polynomial b)
    {
        int n = Math.Max(a._coeffs.Length, b._coeffs.Length);
        var c = new BigRational[n];
        for (int i = 0; i < n; i++) c[i] = a[i] - b[i];
        return new Polynomial(c);
    }

    public static Polynomial operator *(Polynomial a, Polynomial b)
    {
        if (a.IsZero || b.IsZero) return Zero;
        var c = new BigRational[a._coeffs.Length + b._coeffs.Length - 1];
        for (int i = 0; i < c.Length; i++) c[i] = BigRational.Zero;
        for (int i = 0; i < a._coeffs.Length; i++)
            for (int j = 0; j < b._coeffs.Length; j++)
                c[i + j] += a._coeffs[i] * b._coeffs[j];
        return new Polynomial(c);
    }

    public Polynomial Scale(BigRational factor)
    {
        if (factor.IsZero) return Zero;
        var c = new BigRational[_coeffs.Length];
        for (int i = 0; i < c.Length; i++) c[i] = _coeffs[i] * factor;
        return new Polynomial(c);
    }

    /// <summary>Polynomial long division: returns quotient and remainder over Q.</summary>
    public static (Polynomial Quotient, Polynomial Remainder) DivRem(Polynomial a, Polynomial b)
    {
        if (b.IsZero) throw new DivideByZeroException("Division by the zero polynomial.");
        if (a.Degree < b.Degree) return (Zero, a);

        var rem = a._coeffs.ToArray();
        int remDeg = a.Degree;
        var quot = new BigRational[a.Degree - b.Degree + 1];
        for (int i = 0; i < quot.Length; i++) quot[i] = BigRational.Zero;

        BigRational bLead = b.Leading;
        while (remDeg >= b.Degree && !(remDeg == 0 && rem[0].IsZero))
        {
            BigRational factor = rem[remDeg] / bLead;
            int shift = remDeg - b.Degree;
            quot[shift] = factor;
            for (int i = 0; i <= b.Degree; i++)
                rem[shift + i] -= factor * b[i];

            // recompute current remainder degree
            remDeg--;
            while (remDeg >= 0 && rem[remDeg].IsZero) remDeg--;
            if (remDeg < 0) break;
        }

        return (new Polynomial(quot), new Polynomial(rem));
    }

    public Polynomial Monic()
    {
        if (IsZero) return Zero;
        return Scale(Leading.Reciprocal());
    }

    /// <summary>Monic greatest common divisor over Q (Euclid).</summary>
    public static Polynomial Gcd(Polynomial a, Polynomial b)
    {
        while (!b.IsZero)
        {
            Polynomial r = DivRem(a, b).Remainder;
            a = b;
            b = r;
        }
        return a.IsZero ? Zero : a.Monic();
    }

    /// <summary>Squarefree part P / gcd(P, P').</summary>
    public Polynomial SquareFree()
    {
        if (Degree < 1) return Monic();
        Polynomial g = Gcd(this, Derivative());
        if (g.Degree < 1) return Monic();
        return DivRem(this, g).Quotient.Monic();
    }

    // ---------- Sturm machinery (for real-root counting / isolation) ----------

    /// <summary>The Sturm chain p0 = P, p1 = P', p_{k+1} = -rem(p_{k-1}, p_k).</summary>
    public List<Polynomial> SturmChain()
    {
        var chain = new List<Polynomial> { this, Derivative() };
        while (!chain[^1].IsZero && chain[^1].Degree >= 1)
        {
            Polynomial next = DivRem(chain[^2], chain[^1]).Remainder.Scale(BigRational.MinusOne);
            if (next.IsZero) break;
            chain.Add(next);
        }
        return chain;
    }

    private static int SignVariations(List<Polynomial> chain, BigRational x)
    {
        int variations = 0;
        int previousSign = 0;
        foreach (Polynomial p in chain)
        {
            int sign = p.Evaluate(x).Sign;
            if (sign == 0) continue;
            if (previousSign != 0 && sign != previousSign) variations++;
            previousSign = sign;
        }
        return variations;
    }

    /// <summary>
    /// Number of distinct real roots of this squarefree polynomial in the open
    /// interval (a, b), assuming a and b are not themselves roots.
    /// </summary>
    public int CountRootsBetween(BigRational a, BigRational b)
    {
        List<Polynomial> chain = SturmChain();
        return SignVariations(chain, a) - SignVariations(chain, b);
    }

    /// <summary>A Cauchy bound: every real root lies in (-B, B).</summary>
    public BigRational CauchyBound()
    {
        if (Degree < 1) return BigRational.One;
        BigRational max = BigRational.Zero;
        BigRational lead = Leading.Abs;
        for (int i = 0; i < Degree; i++)
        {
            BigRational ratio = _coeffs[i].Abs / lead;
            if (ratio > max) max = ratio;
        }
        return BigRational.One + max;
    }

    /// <summary>Integer (primitive) coefficients, low degree first, with positive leading sign.</summary>
    public BigInteger[] IntegerCoefficients()
    {
        if (IsZero) return Array.Empty<BigInteger>();

        BigInteger denLcm = BigInteger.One;
        foreach (BigRational c in _coeffs)
            denLcm = denLcm / BigInteger.GreatestCommonDivisor(denLcm, c.Denominator) * c.Denominator;

        var ints = new BigInteger[_coeffs.Length];
        BigInteger content = BigInteger.Zero;
        for (int i = 0; i < _coeffs.Length; i++)
        {
            ints[i] = _coeffs[i].Numerator * (denLcm / _coeffs[i].Denominator);
            content = BigInteger.GreatestCommonDivisor(content, BigInteger.Abs(ints[i]));
        }
        if (content.IsZero) content = BigInteger.One;
        if (ints[^1].Sign < 0) content = -content;
        for (int i = 0; i < ints.Length; i++) ints[i] /= content;
        return ints;
    }

    public override string ToString()
    {
        if (IsZero) return "0";
        var terms = new List<string>();
        for (int i = _coeffs.Length - 1; i >= 0; i--)
        {
            if (_coeffs[i].IsZero) continue;
            string coeff = _coeffs[i].ToString();
            terms.Add(i switch
            {
                0 => coeff,
                1 => $"{coeff}*x",
                _ => $"{coeff}*x^{i}",
            });
        }
        return string.Join(" + ", terms);
    }
}
