using Numerica.Parsing;
using System.Numerics;
using System.Text;

namespace Numerica;

/// <summary>
/// An irrational (more precisely: a closed-form real) represented as an exact
/// SYMBOLIC TREE rather than a single value.
///
/// A rational pair cannot capture sqrt(2): an irrational is, by definition, not a
/// ratio of integers. So instead of storing a number we store the OPERATION that
/// builds it -- a tree whose leaves are <see cref="BigRational"/> constants (plus
/// the symbolic constants pi and e) and whose nodes are sums, products and powers
/// with rational exponents (which cover all roots). This is the "expression"
/// approach used by computer algebra systems (SymPy, Mathematica).
///
/// Two payoffs of keeping the structure:
///   - Exact algebraic simplification. sqrt(2)*sqrt(2) collapses to 2, and
///     phi^2 - phi - 1 collapses to 0 -- symbolically, not just numerically.
///   - Arbitrary-precision evaluation on demand: <see cref="Approximate"/> returns
///     a rational within 2^-bits of the true value, exact to any precision.
///
/// The true value is never computed: the tree stays a suspended calculation until
/// someone asks for digits. The smart constructors (MakeSum / MakeProduct /
/// MakePower) normalize the tree as it is built -- flattening, folding rational
/// parts, combining like powers, and distributing products over sums so that
/// polynomial identities cancel.
///
/// The wall is not printing, it is EQUALITY: deciding a == b on closed-form reals
/// is undecidable in general (Richardson's theorem). The simplifier reaches exact
/// answers when the structure cancels; otherwise we can only compare up to a
/// chosen precision. See DOCS.md for references.
/// </summary>
internal abstract class BigIrrational
{
    public static readonly BigIrrational Zero = new RationalNode(BigRational.Zero);
    public static readonly BigIrrational One = new RationalNode(BigRational.One);

    /// <summary>The circle constant pi (symbolic leaf, evaluated via Machin's formula).</summary>
    public static readonly BigIrrational Pi = new SymbolNode(SymbolNode.PiName);

    /// <summary>Euler's number e (symbolic leaf, evaluated via its Taylor series).</summary>
    public static readonly BigIrrational E = new SymbolNode(SymbolNode.EName);

    /// <summary>The omega constant W(1), root of x*e^x = 1 (symbolic leaf, evaluated via Newton).</summary>
    public static readonly BigIrrational Omega = new SymbolNode(SymbolNode.OmegaName);

    /// <summary>
    /// Catalan's constant G = (π/8)·ln(2 + √3) + (3/8)·Σ 1/((2k+1)²·C(2k,k)). Kept as a
    /// composed expression: the closed-form prefactor reuses the tested π/ln/√ closures and
    /// only the geometrically-convergent rational series is a new symbolic leaf.
    /// </summary>
    public static readonly BigIrrational Catalan =
        Pi / 8 * Ln(FromInteger(2) + Sqrt(FromInteger(3)))
        + FromRational(new BigRational(3, 8)) * new SymbolNode(SymbolNode.CatalanSeriesName);

    // ---------- Construction ----------

    public static BigIrrational FromRational(BigRational value) => new RationalNode(value);
    public static BigIrrational FromInteger(BigInteger value) => new RationalNode(new BigRational(value));

    public static implicit operator BigIrrational(BigRational value) => new RationalNode(value);
    public static implicit operator BigIrrational(BigInteger value) => FromInteger(value);
    public static implicit operator BigIrrational(int value) => FromInteger(value);

    /// <summary>Square root (sugar for raising to the power 1/2).</summary>
    public static BigIrrational Sqrt(BigIrrational value) => MakePower(value, new BigRational(1, 2));

    /// <summary>n-th root (sugar for raising to the power 1/n).</summary>
    public static BigIrrational Root(BigIrrational value, int n)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Root degree must be positive.");
        return MakePower(value, new BigRational(1, n));
    }

    /// <summary>Raise to an integer power.</summary>
    public static BigIrrational Power(BigIrrational value, int exponent)
        => MakePower(value, new BigRational(exponent));

    /// <summary>Raise to a rational power (the general algebraic case).</summary>
    public static BigIrrational Power(BigIrrational value, BigRational exponent)
        => MakePower(value, exponent);

    // ---------- Transcendental functions ----------

    public static BigIrrational Exp(BigIrrational value) => MakeFunction(FunctionNode.ExpName, value);
    public static BigIrrational Ln(BigIrrational value) => MakeFunction(FunctionNode.LnName, value);
    public static BigIrrational Sin(BigIrrational value) => MakeFunction(FunctionNode.SinName, value);
    public static BigIrrational Cos(BigIrrational value) => MakeFunction(FunctionNode.CosName, value);
    public static BigIrrational Tan(BigIrrational value) => MakeFunction(FunctionNode.TanName, value);
    public static BigIrrational Atan(BigIrrational value) => MakeFunction(FunctionNode.AtanName, value);

    // ---------- Operators ----------

    public static BigIrrational operator +(BigIrrational a, BigIrrational b) => MakeSum(new[] { a, b });

    public static BigIrrational operator -(BigIrrational a, BigIrrational b)
        => MakeSum(new[] { a, MakeProduct(new[] { (BigIrrational)new RationalNode(BigRational.MinusOne), b }) });

    public static BigIrrational operator -(BigIrrational a)
        => MakeProduct(new[] { (BigIrrational)new RationalNode(BigRational.MinusOne), a });

    public static BigIrrational operator *(BigIrrational a, BigIrrational b) => MakeProduct(new[] { a, b });

    public static BigIrrational operator /(BigIrrational a, BigIrrational b)
        => MakeProduct(new[] { a, MakePower(b, BigRational.MinusOne) });

    // ---------- Evaluation ----------

    // Internal contract: returns an integer m with |x - m/2^bits| <= 2^-bits,
    // i.e. m is x*2^bits rounded. Composing these closures is the numeric engine.
    internal abstract Func<int, BigInteger> Compile();

    /// <summary>
    /// Returns a rational r with |x - r| &lt;= 2^-bits. The exact value is never
    /// materialized; this is the on-demand evaluation of the symbolic tree.
    /// </summary>
    public BigRational Approximate(int bits)
    {
        if (bits < 0) bits = 0;
        return new BigRational(Compile()(bits), BigInteger.One << bits);
    }

    /// <summary>Rounded decimal expansion with the requested number of fractional digits.</summary>
    public string ToDecimalString(int digits)
    {
        if (digits < 0) digits = 0;
        int bits = (int)Math.Ceiling(digits * 3.3219280949) + 16; // log2(10) ~= 3.32
        BigInteger m = Compile()(bits);                           // x ~= m / 2^bits

        bool negative = m.Sign < 0;
        BigInteger abs = BigInteger.Abs(m);
        BigInteger scaled = ScaleDown(abs * BigInteger.Pow(10, digits), bits);

        BigInteger pow = BigInteger.Pow(10, digits);
        BigInteger intPart = scaled / pow;
        BigInteger fracPart = scaled % pow;

        string s = intPart.ToString();
        if (digits > 0) s += "." + fracPart.ToString().PadLeft(digits, '0');
        return negative ? "-" + s : s;
    }

    /// <summary>
    /// Numeric comparison up to a binary precision. This is NOT exact equality
    /// (which is undecidable): it is the most that can be done in finite time.
    /// Returns -1, 0, 1 with a 2 ulp tolerance to absorb rounding.
    /// </summary>
    public int CompareApprox(BigIrrational other, int prec = 200)
    {
        BigInteger a = Compile()(prec);
        BigInteger b = other.Compile()(prec);
        if (BigInteger.Abs(a - b) <= 2) return 0;
        return a < b ? -1 : 1;
    }

    public bool ApproximatelyEquals(BigIrrational other, int prec = 200) => CompareApprox(other, prec) == 0;

    /// <summary>Numeric sign (-1, 0, +1) up to a binary precision.</summary>
    public int SignApprox(int prec = 200) => CompareApprox(Zero, prec);

    /// <summary>
    /// Two-argument arctangent atan2(y, x), the angle of the point (x, y) in
    /// (-pi, pi]. Quadrant selection uses the numeric signs of x and y.
    /// </summary>
    public static BigIrrational Atan2(BigIrrational y, BigIrrational x)
    {
        int sx = x.SignApprox();
        int sy = y.SignApprox();

        if (sx > 0) return Atan(y / x);
        if (sx < 0) return sy >= 0 ? Atan(y / x) + Pi : Atan(y / x) - Pi;
        // x == 0
        if (sy > 0) return Pi / 2;
        if (sy < 0) return -(Pi / 2);
        return Zero; // (0, 0)
    }

    /// <summary>True when the tree has already collapsed to an exact rational.</summary>
    public bool TryGetRational(out BigRational value)
    {
        if (this is RationalNode r) { value = r.Value; return true; }
        value = BigRational.Zero;
        return false;
    }

    // ---------- Smart constructors (normalization happens here) ----------

    private static BigIrrational MakeSum(IEnumerable<BigIrrational> parts)
    {
        // Flatten nested sums.
        var flat = new List<BigIrrational>();
        void Add(BigIrrational p)
        {
            if (p is SumNode s) { foreach (BigIrrational t in s.Terms) Add(t); }
            else flat.Add(p);
        }
        foreach (BigIrrational p in parts) Add(p);

        // Group like terms by their non-rational part, accumulating rational coefficients.
        BigRational constant = BigRational.Zero;
        var terms = new List<(string Key, BigRational Coeff, BigIrrational Remainder)>();
        foreach (BigIrrational term in flat)
        {
            (BigRational coeff, BigIrrational rest) = SplitCoefficient(term);
            if (rest is RationalNode one && one.Value == BigRational.One)
            {
                constant += coeff;
                continue;
            }
            string key = rest.ToString()!;
            int idx = terms.FindIndex(t => t.Key == key);
            if (idx >= 0) terms[idx] = (key, terms[idx].Coeff + coeff, rest);
            else terms.Add((key, coeff, rest));
        }

        var result = new List<BigIrrational>();
        if (!constant.IsZero) result.Add(new RationalNode(constant));
        foreach (var entry in terms)
        {
            if (entry.Coeff.IsZero) continue;
            result.Add(MakeProduct(new[] { (BigIrrational)new RationalNode(entry.Coeff), entry.Remainder }));
        }

        if (result.Count == 0) return new RationalNode(BigRational.Zero);
        if (result.Count == 1) return result[0];
        return new SumNode(result);
    }

    private static BigIrrational MakeProduct(IEnumerable<BigIrrational> parts)
    {
        // Flatten nested products.
        var flat = new List<BigIrrational>();
        void Add(BigIrrational p)
        {
            if (p is ProductNode pr) { foreach (BigIrrational f in pr.Factors) Add(f); }
            else flat.Add(p);
        }
        foreach (BigIrrational p in parts) Add(p);

        // Distribute over any sum so that polynomial identities can cancel later.
        if (flat.Exists(f => f is SumNode))
        {
            var expanded = new List<BigIrrational> { One };
            foreach (BigIrrational factor in flat)
            {
                IEnumerable<BigIrrational> pieces = factor is SumNode s ? s.Terms : new[] { factor };
                var next = new List<BigIrrational>();
                foreach (BigIrrational acc in expanded)
                    foreach (BigIrrational piece in pieces)
                        next.Add(MakeProduct(new[] { acc, piece })); // sub-factors are sum-free
                expanded = next;
            }
            return MakeSum(expanded);
        }

        // No sums left: fold the rational coefficient and combine like bases.
        BigRational coeff = BigRational.One;
        var bases = new List<(string Key, BigIrrational Base, BigRational Exponent)>();
        foreach (BigIrrational factor in flat)
        {
            if (factor is RationalNode r) { coeff *= r.Value; continue; }

            BigIrrational baseExpr;
            BigRational exponent;
            if (factor is PowerNode pw) { baseExpr = pw.Base; exponent = pw.Exponent; }
            else { baseExpr = factor; exponent = BigRational.One; }

            string key = baseExpr.ToString()!;
            int idx = bases.FindIndex(b => b.Key == key);
            if (idx >= 0) bases[idx] = (key, baseExpr, bases[idx].Exponent + exponent);
            else bases.Add((key, baseExpr, exponent));
        }

        if (coeff.IsZero) return new RationalNode(BigRational.Zero);

        var factors = new List<BigIrrational>();
        foreach (var entry in bases)
        {
            BigIrrational power = MakePower(entry.Base, entry.Exponent);
            if (power is RationalNode rn) coeff *= rn.Value;
            else factors.Add(power);
        }

        if (factors.Count == 0) return new RationalNode(coeff);
        if (coeff != BigRational.One) factors.Insert(0, new RationalNode(coeff));
        if (factors.Count == 1) return factors[0];
        return new ProductNode(factors);
    }

    private static BigIrrational MakePower(BigIrrational baseExpr, BigRational exponent)
    {
        if (exponent.IsZero) return One;
        if (exponent == BigRational.One) return baseExpr;

        // Rational base: fold exact powers and exact roots into a rational.
        if (baseExpr is RationalNode r)
        {
            if (TryExactPower(r.Value, exponent, out BigRational exact))
                return new RationalNode(exact);
        }

        // Power of a power: multiply the exponents.
        if (baseExpr is PowerNode pw)
            return MakePower(pw.Base, pw.Exponent * exponent);

        // Integer power of a sum: expand so the product distributes and cancels.
        if (baseExpr is SumNode && exponent.IsInteger && exponent.Sign > 0)
        {
            int n = (int)exponent.Numerator;
            var copies = new List<BigIrrational>();
            for (int i = 0; i < n; i++) copies.Add(baseExpr);
            return MakeProduct(copies);
        }

        return new PowerNode(baseExpr, exponent);
    }

    private static BigIrrational MakeFunction(string name, BigIrrational arg)
    {
        // A few exact special values; everything else stays a symbolic node.
        if (arg.TryGetRational(out BigRational v))
        {
            if (v.IsZero)
            {
                switch (name)
                {
                    case FunctionNode.ExpName: return One;
                    case FunctionNode.CosName: return One;
                    case FunctionNode.SinName:
                    case FunctionNode.TanName:
                    case FunctionNode.AtanName: return Zero;
                }
            }
            if (v == BigRational.One && name == FunctionNode.LnName) return Zero;
        }
        return new FunctionNode(name, arg);
    }

    // Splits a term into (rational coefficient, remaining symbolic part).
    private static (BigRational, BigIrrational) SplitCoefficient(BigIrrational term)
    {
        if (term is RationalNode r) return (r.Value, One);
        if (term is ProductNode pr)
        {
            BigRational coeff = BigRational.One;
            var rest = new List<BigIrrational>();
            foreach (BigIrrational f in pr.Factors)
            {
                if (f is RationalNode rn) coeff *= rn.Value;
                else rest.Add(f);
            }
            BigIrrational restExpr = rest.Count switch
            {
                0 => One,
                1 => rest[0],
                _ => new ProductNode(rest),
            };
            return (coeff, restExpr);
        }
        return (BigRational.One, term);
    }

    // Tries to evaluate baseValue^(p/q) exactly as a rational; false if it stays irrational.
    private static bool TryExactPower(BigRational baseValue, BigRational exponent, out BigRational result)
    {
        result = BigRational.Zero;

        BigInteger p = exponent.Numerator;
        BigInteger q = exponent.Denominator;
        if (p > int.MaxValue || p < int.MinValue) return false; // keep it symbolic for huge exponents

        BigRational powered = BigRational.Pow(baseValue, (int)p);
        if (q.IsOne) { result = powered; return true; }
        if (q > int.MaxValue) return false;
        int k = (int)q;

        BigInteger num = powered.Numerator;
        BigInteger den = powered.Denominator;
        if (num.Sign < 0 && k % 2 == 0) return false; // even root of a negative -> not real

        int sign = num.Sign < 0 ? -1 : 1;
        if (!TryExactIntegerRoot(BigInteger.Abs(num), k, out BigInteger rootNum)) return false;
        if (!TryExactIntegerRoot(den, k, out BigInteger rootDen)) return false;

        result = new BigRational(sign * rootNum, rootDen);
        return true;
    }

    private static bool TryExactIntegerRoot(BigInteger value, int k, out BigInteger root)
    {
        root = IntegerRoot(value, k);
        return BigInteger.Pow(root, k) == value;
    }

    // ===========================================================================
    // Numeric engine: precision-driven approximation closures.
    //
    // Each closure maps bits -> m with |x - m/2^bits| <= 2^-bits (m is x*2^bits
    // rounded). Operations compose closures and request guard bits from their
    // children so the rounding error stays within one unit in the last place.
    // ===========================================================================

    private static Func<int, BigInteger> ConstantClosure(BigRational value)
    {
        BigInteger num = value.Numerator;
        BigInteger den = value.Denominator; // positive
        return bits => DivRound(bits >= 0 ? num << bits : num, bits >= 0 ? den : den << -bits);
    }

    private static Func<int, BigInteger> AddClosure(Func<int, BigInteger> a, Func<int, BigInteger> b)
        => bits => ScaleDown(a(bits + 2) + b(bits + 2), 2); // 2 guard bits, then round

    private static Func<int, BigInteger> MulClosure(Func<int, BigInteger> a, Func<int, BigInteger> b)
        => bits =>
        {
            const int probe = 30;       // rough magnitude estimate
            BigInteger ax = a(probe);
            BigInteger ay = b(probe);
            int mx = (int)(BigInteger.Abs(ax) + 1).GetBitLength() - probe; // ~ log2|a|
            int my = (int)(BigInteger.Abs(ay) + 1).GetBitLength() - probe; // ~ log2|b|

            const int g = 4;            // guard bits
            int px = bits + Math.Max(my, 0) + g;
            int py = bits + Math.Max(mx, 0) + g;

            BigInteger bx = a(px); // ~= a * 2^px
            BigInteger by = b(py); // ~= b * 2^py
            return ScaleDown(bx * by, px + py - bits);
        };

    private static Func<int, BigInteger> ReciprocalClosure(Func<int, BigInteger> a)
        => bits =>
        {
            // Division needs a lower bound on |x|: this is where undecidability surfaces.
            int p = 10;
            BigInteger v;
            while (true)
            {
                v = a(p);
                if (BigInteger.Abs(v) > 1) break;     // |x| is certainly > 0
                p += 10;
                if (p > 100_000)
                    throw new InvalidOperationException(
                        "Division by (probable) zero: equality to zero is undecidable.");
            }
            int mLow = (int)(BigInteger.Abs(v) - 1).GetBitLength() - p; // x >~ 2^mLow
            const int g = 4;
            int px = bits - 2 * mLow + g;
            if (px < 1) px = 1;

            BigInteger bx = a(px);                     // ~= x * 2^px
            if (bx.IsZero) bx = v;                     // cautious fallback
            return DivRound(BigInteger.One << (bits + px), bx);
        };

    private static Func<int, BigInteger> PowClosure(Func<int, BigInteger> a, int exponent)
    {
        if (exponent == 0) return ConstantClosure(BigRational.One);

        bool negative = exponent < 0;
        int e = Math.Abs(exponent);
        Func<int, BigInteger> result = ConstantClosure(BigRational.One);
        Func<int, BigInteger> b = a;
        while (e > 0)
        {
            if ((e & 1) == 1) result = MulClosure(result, b);
            e >>= 1;
            if (e > 0) b = MulClosure(b, b);
        }
        return negative ? ReciprocalClosure(result) : result;
    }

    private static Func<int, BigInteger> RootClosure(Func<int, BigInteger> a, int n)
    {
        if (n == 1) return a;
        return bits =>
        {
            // (root(x) * 2^bits)^n = x * 2^(n*bits); take the integer n-th root.
            int p = n * (bits + 1);
            BigInteger scaled = a(p);                  // ~= x * 2^(n*(bits+1))
            if (scaled.Sign < 0 && n % 2 == 0)
                throw new InvalidOperationException("Even root of a negative number.");
            BigInteger root = IntegerRoot(BigInteger.Abs(scaled), n); // ~= |x|^(1/n) * 2^(bits+1)
            if (scaled.Sign < 0) root = -root;
            return ScaleDown(root, 1);
        };
    }

    // Rough magnitude of a closure's value, used to size guard/argument bits.
    private static double EstimateValue(Func<int, BigInteger> a)
    {
        const int probe = 48;
        BigInteger m = a(probe);
        return (double)m / Math.Pow(2, probe);
    }

    private static Func<int, BigInteger> ExpClosure(Func<int, BigInteger> a)
        => bits =>
        {
            double v = EstimateValue(a);
            int extra = (int)Math.Max(0, Math.Ceiling(v / 0.6931471805599453)); // output grows like e^v
            int p = bits + 40 + extra;
            return ScaleDown(RealMath.Exp(a(p), p), p - bits);
        };

    private static Func<int, BigInteger> LnClosure(Func<int, BigInteger> a)
        => bits =>
        {
            double v = EstimateValue(a);
            if (v <= 0) throw new InvalidOperationException("Logarithm of a non-positive number.");
            int extra = v < 1 ? (int)Math.Ceiling(-Math.Log2(v)) : 0; // near 0 the slope blows up
            int p = bits + 40 + extra;
            return ScaleDown(RealMath.Ln(a(p), p), p - bits);
        };

    private static Func<int, BigInteger> SinClosure(Func<int, BigInteger> a)
        => bits =>
        {
            double v = EstimateValue(a);
            int extra = (int)Math.Max(0, Math.Ceiling(Math.Log2(Math.Abs(v) + 2))); // angle reduction loss
            int p = bits + 40 + extra;
            return ScaleDown(RealMath.Sin(a(p), p), p - bits);
        };

    private static Func<int, BigInteger> CosClosure(Func<int, BigInteger> a)
        => bits =>
        {
            double v = EstimateValue(a);
            int extra = (int)Math.Max(0, Math.Ceiling(Math.Log2(Math.Abs(v) + 2)));
            int p = bits + 40 + extra;
            return ScaleDown(RealMath.Cos(a(p), p), p - bits);
        };

    private static Func<int, BigInteger> AtanClosure(Func<int, BigInteger> a)
        => bits =>
        {
            int p = bits + 40;
            return ScaleDown(RealMath.Atan(a(p), p), p - bits);
        };

    private static BigInteger PiClosure(int bits)
    {
        // Machin's formula: pi = 16*arctan(1/5) - 4*arctan(1/239).
        int p = bits + 16;
        BigInteger scale = BigInteger.One << p;
        BigInteger pi = 16 * ArctanInverse(5, scale) - 4 * ArctanInverse(239, scale);
        return ScaleDown(pi, 16);
    }

    private static BigInteger OmegaClosure(int bits) => ScaleDown(RealMath.Omega(bits + 16), 16);

    private static BigInteger EClosure(int bits)
    {
        // e = sum 1/k!
        BigInteger scale = BigInteger.One << (bits + 16);
        BigInteger term = scale; // 1/0!
        BigInteger sum = BigInteger.Zero;
        int k = 0;
        while (!term.IsZero)
        {
            sum += term;
            k++;
            term /= k;            // term ~= (1/k!) scaled
        }
        return ScaleDown(sum, 16);
    }

    private static BigInteger CatalanSeriesClosure(int bits)
    {
        // S = sum_{k>=0} 1 / ((2k+1)^2 * C(2k,k)), the rational part of Catalan's constant.
        // C(2k,k) grows like 4^k, so the term ~ 1/(4^k * k^1.5): geometric, ~bits/2 terms.
        BigInteger scale = BigInteger.One << (bits + 16);
        BigInteger sum = BigInteger.Zero;
        BigInteger central = BigInteger.One; // C(0, 0)
        int k = 0;
        while (true)
        {
            BigInteger odd = 2 * k + 1;
            BigInteger term = scale / (odd * odd * central);
            if (term.IsZero) break;
            sum += term;
            k++;
            // C(2k,k) = C(2k-2,k-1) * (2k)(2k-1) / k^2 (exact integer division).
            central = central * (2 * k) * (2 * k - 1) / ((BigInteger)k * k);
        }
        return ScaleDown(sum, 16);
    }

    // arctan(1/xInv) * scale, via the alternating power series.
    private static BigInteger ArctanInverse(BigInteger xInv, BigInteger scale)
    {
        BigInteger xSquared = xInv * xInv;
        BigInteger power = scale / xInv;   // (1/xInv) * scale, the k=0 term magnitude
        BigInteger sum = BigInteger.Zero;
        int k = 0;
        while (!power.IsZero)
        {
            BigInteger term = power / (2 * k + 1);
            sum += (k % 2 == 0) ? term : -term;
            power /= xSquared;
            k++;
        }
        return sum;
    }

    // ---------- Integer helpers ----------

    /// <summary>round(n / 2^k) to an integer (for k &lt;= 0 this is n &lt;&lt; -k).</summary>
    private static BigInteger ScaleDown(BigInteger n, int k)
    {
        if (k <= 0) return n << -k;
        BigInteger half = BigInteger.One << (k - 1);
        return (n + half) >> k;   // round-half-up
    }

    /// <summary>round(a / b) to the nearest integer, ties away from zero.</summary>
    private static BigInteger DivRound(BigInteger a, BigInteger b)
    {
        BigInteger q = a / b;            // truncates toward zero
        BigInteger r = a - q * b;
        if (BigInteger.Abs(r) * 2 >= BigInteger.Abs(b))
            q += ((a.Sign < 0) ^ (b.Sign < 0)) ? -1 : 1;
        return q;
    }

    /// <summary>Integer (floor) n-th root via Newton's method, with a final clamp.</summary>
    private static BigInteger IntegerRoot(BigInteger n, int k)
    {
        if (k == 1) return n;
        if (n.Sign < 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (n.IsZero || n.IsOne) return n;

        int bits = (int)n.GetBitLength();
        BigInteger x = BigInteger.One << ((bits + k - 1) / k);
        while (true)
        {
            BigInteger powKm1 = BigInteger.Pow(x, k - 1);
            BigInteger y = ((k - 1) * x + n / powKm1) / k;
            if (y >= x) break;
            x = y;
        }
        while (BigInteger.Pow(x, k) > n) x -= BigInteger.One;
        while (BigInteger.Pow(x + BigInteger.One, k) <= n) x += BigInteger.One;
        return x;
    }

    // ---------- Node types ----------

    private sealed class RationalNode : BigIrrational
    {
        public BigRational Value { get; }
        public RationalNode(BigRational value) => Value = value;

        internal override Func<int, BigInteger> Compile() => ConstantClosure(Value);
        public override string ToString() => Value.ToString();
    }

    private sealed class SymbolNode : BigIrrational
    {
        public const string PiName = "π"; // greek small letter pi
        public const string EName = "e";
        public const string OmegaName = "Ω"; // greek capital letter omega (the omega constant)
        public const string CatalanSeriesName = "catalan_series"; // internal leaf, never parsed

        public string Name { get; }
        public SymbolNode(string name) => Name = name;

        internal override Func<int, BigInteger> Compile() => Name switch
        {
            PiName => PiClosure,
            EName => EClosure,
            OmegaName => OmegaClosure,
            CatalanSeriesName => CatalanSeriesClosure,
            _ => throw new InvalidOperationException($"Unknown symbolic constant '{Name}'."),
        };

        public override string ToString() => Name;
    }

    private sealed class SumNode : BigIrrational
    {
        public IReadOnlyList<BigIrrational> Terms { get; }
        public SumNode(IReadOnlyList<BigIrrational> terms) => Terms = terms;

        internal override Func<int, BigInteger> Compile()
        {
            Func<int, BigInteger> acc = Terms[0].Compile();
            for (int i = 1; i < Terms.Count; i++) acc = AddClosure(acc, Terms[i].Compile());
            return acc;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Terms.Count; i++)
            {
                string s = Terms[i].ToString()!;
                if (i == 0) sb.Append(s);
                else if (s.StartsWith('-')) sb.Append(" - ").Append(s.AsSpan(1));
                else sb.Append(" + ").Append(s);
            }
            return sb.ToString();
        }
    }

    private sealed class ProductNode : BigIrrational
    {
        public IReadOnlyList<BigIrrational> Factors { get; }
        public ProductNode(IReadOnlyList<BigIrrational> factors) => Factors = factors;

        internal override Func<int, BigInteger> Compile()
        {
            Func<int, BigInteger> acc = Factors[0].Compile();
            for (int i = 1; i < Factors.Count; i++) acc = MulClosure(acc, Factors[i].Compile());
            return acc;
        }

        public override string ToString()
        {
            // Render a leading -1 coefficient as a sign instead of "-1*...".
            IEnumerable<BigIrrational> factors = Factors;
            string prefix = string.Empty;
            if (Factors[0] is RationalNode r && r.Value == BigRational.MinusOne && Factors.Count > 1)
            {
                prefix = "-";
                factors = Factors.Skip(1);
            }
            string body = string.Join("·", factors.Select(WrapInProduct)); // middle dot
            return prefix + body;
        }

        private static string WrapInProduct(BigIrrational factor)
            => factor is SumNode ? $"({factor})" : factor.ToString()!;
    }

    private sealed class FunctionNode : BigIrrational
    {
        public const string ExpName = "exp";
        public const string LnName = "ln";
        public const string SinName = "sin";
        public const string CosName = "cos";
        public const string TanName = "tan";
        public const string AtanName = "atan";

        public string Name { get; }
        public BigIrrational Argument { get; }

        public FunctionNode(string name, BigIrrational argument)
        {
            Name = name;
            Argument = argument;
        }

        internal override Func<int, BigInteger> Compile()
        {
            Func<int, BigInteger> arg = Argument.Compile();
            return Name switch
            {
                ExpName => ExpClosure(arg),
                LnName => LnClosure(arg),
                SinName => SinClosure(arg),
                CosName => CosClosure(arg),
                AtanName => AtanClosure(arg),
                TanName => MulClosure(SinClosure(arg), ReciprocalClosure(CosClosure(arg))),
                _ => throw new InvalidOperationException($"Unknown function '{Name}'."),
            };
        }

        public override string ToString() => $"{Name}({Argument})";
    }

    private sealed class PowerNode : BigIrrational
    {
        public BigIrrational Base { get; }
        public BigRational Exponent { get; }

        public PowerNode(BigIrrational baseExpr, BigRational exponent)
        {
            Base = baseExpr;
            Exponent = exponent;
        }

        internal override Func<int, BigInteger> Compile()
        {
            Func<int, BigInteger> powered = PowClosure(Base.Compile(), (int)Exponent.Numerator);
            return Exponent.Denominator.IsOne ? powered : RootClosure(powered, (int)Exponent.Denominator);
        }

        public override string ToString()
        {
            if (Exponent == new BigRational(1, 2))
                return $"√({Base})"; // square root sign

            string baseText = Base is RationalNode r && r.Value.Sign >= 0 && r.Value.IsInteger
                ? Base.ToString()!
                : $"({Base})";

            return Exponent.IsInteger
                ? $"{baseText}^{Exponent.Numerator}"
                : $"{baseText}^({Exponent})";
        }
    }
}
