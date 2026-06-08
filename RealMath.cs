using System.Numerics;

namespace SuperNumbers;

/// <summary>
/// Fixed-point transcendental functions in arbitrary binary precision.
///
/// Every value is a scaled integer: a <see cref="BigInteger"/> <c>V</c> at scale
/// <c>p</c> stands for the real <c>V / 2^p</c>. Each function returns <c>f(v)</c>
/// at the same scale. These are the numeric kernels behind the transcendental
/// nodes of <see cref="BigIrrational"/>; argument reduction keeps the Taylor
/// series in their fast-converging range, and the callers add guard bits so the
/// truncation error stays below one unit in the last place at the requested
/// precision. See DOCS.md (Brent &amp; Zimmermann) for the techniques.
/// </summary>
internal static class RealMath
{
    /// <summary>exp(v), with range reduction exp(v) = exp(v/2^n)^(2^n).</summary>
    public static BigInteger Exp(BigInteger value, int p)
    {
        BigInteger scale = BigInteger.One << p;
        if (value.IsZero) return scale;

        bool negative = value.Sign < 0;
        BigInteger abs = BigInteger.Abs(value);

        // Choose n so that the reduced argument r = abs/2^n is well below 1.
        int n = 0;
        BigInteger probe = abs;
        while (probe >= scale) { probe >>= 1; n++; }
        n += 4;
        BigInteger r = RoundShr(abs, n);

        // Taylor: sum r^k / k!
        BigInteger term = scale;
        BigInteger sum = BigInteger.Zero;
        int k = 0;
        while (!term.IsZero)
        {
            sum += term;
            k++;
            term = MulFx(term, r, scale) / k;
        }

        // Undo the reduction by squaring n times.
        BigInteger result = sum;
        for (int i = 0; i < n; i++) result = MulFx(result, result, scale);

        return negative ? DivRound(scale * scale, result) : result;
    }

    /// <summary>ln(v) for v &gt; 0, via v = m * 2^e (m in [1,2)) and atanh series.</summary>
    public static BigInteger Ln(BigInteger value, int p)
    {
        if (value.Sign <= 0) throw new InvalidOperationException("Logarithm of a non-positive number.");
        BigInteger scale = BigInteger.One << p;

        int e = (int)value.GetBitLength() - 1 - p;            // floor(log2 v)
        BigInteger m = e >= 0 ? RoundShr(value, e) : value << -e; // m in [1,2) at scale

        BigInteger lnM = LnNearOne(m, scale);                 // ln(m)
        return lnM + e * Ln2(scale);
    }

    /// <summary>sin(v), argument reduced into (-pi, pi].</summary>
    public static BigInteger Sin(BigInteger value, int p)
    {
        BigInteger scale = BigInteger.One << p;
        BigInteger r = ReduceAngle(value, scale, p);

        BigInteger x2 = MulFx(r, r, scale);
        BigInteger term = r;            // r^1 / 1!
        BigInteger sum = BigInteger.Zero;
        int k = 0;
        while (!term.IsZero)
        {
            sum += (k % 2 == 0) ? term : -term;
            term = MulFx(term, x2, scale) / ((BigInteger)(2 * k + 2) * (2 * k + 3));
            k++;
        }
        return sum;
    }

    /// <summary>cos(v), argument reduced into (-pi, pi].</summary>
    public static BigInteger Cos(BigInteger value, int p)
    {
        BigInteger scale = BigInteger.One << p;
        BigInteger r = ReduceAngle(value, scale, p);

        BigInteger x2 = MulFx(r, r, scale);
        BigInteger term = scale;        // r^0 / 0!
        BigInteger sum = BigInteger.Zero;
        int k = 0;
        while (!term.IsZero)
        {
            sum += (k % 2 == 0) ? term : -term;
            term = MulFx(term, x2, scale) / ((BigInteger)(2 * k + 1) * (2 * k + 2));
            k++;
        }
        return sum;
    }

    /// <summary>atan(v) for any v, via atan(v) = pi/2 - atan(1/v) when |v| &gt; 1.</summary>
    public static BigInteger Atan(BigInteger value, int p)
    {
        BigInteger scale = BigInteger.One << p;
        if (BigInteger.Abs(value) <= scale)
            return AtanSmall(value, scale);

        // |v| > 1: atan(v) = sign(v)*pi/2 - atan(1/v)
        BigInteger inv = DivRound(scale * scale, value);
        BigInteger halfPi = Pi(p) / 2;
        BigInteger small = AtanSmall(inv, scale);
        return value.Sign > 0 ? halfPi - small : -halfPi - small;
    }

    /// <summary>pi via Machin's formula, at scale 2^p.</summary>
    public static BigInteger Pi(int p)
    {
        int gp = p + 8;
        BigInteger scale = BigInteger.One << gp;
        BigInteger pi = 16 * ArctanInverse(5, scale) - 4 * ArctanInverse(239, scale);
        return RoundShr(pi, 8);
    }

    // ---------- internal pieces ----------

    // atan(v) for |v| <= 1 via the power series v - v^3/3 + v^5/5 - ...
    private static BigInteger AtanSmall(BigInteger value, BigInteger scale)
    {
        BigInteger x2 = MulFx(value, value, scale);
        BigInteger power = value;       // v^(2k+1)
        BigInteger sum = BigInteger.Zero;
        int k = 0;
        while (!power.IsZero)
        {
            BigInteger term = power / (2 * k + 1);
            sum += (k % 2 == 0) ? term : -term;
            power = MulFx(power, x2, scale);
            k++;
        }
        return sum;
    }

    // ln(m) for m in [1,2): 2*(t + t^3/3 + t^5/5 + ...) with t = (m-1)/(m+1).
    private static BigInteger LnNearOne(BigInteger m, BigInteger scale)
    {
        BigInteger t = DivRound((m - scale) * scale, m + scale);
        BigInteger t2 = MulFx(t, t, scale);
        BigInteger power = t;
        BigInteger sum = BigInteger.Zero;
        int k = 0;
        while (!power.IsZero)
        {
            sum += power / (2 * k + 1);
            power = MulFx(power, t2, scale);
            k++;
        }
        return 2 * sum;
    }

    // ln(2) at the given scale (t = 1/3 in the atanh series).
    private static BigInteger Ln2(BigInteger scale) => LnNearOne(2 * scale, scale);

    // Reduces an angle into (-pi, pi] at scale 2^p.
    private static BigInteger ReduceAngle(BigInteger value, BigInteger scale, int p)
    {
        BigInteger pi = Pi(p);
        BigInteger twoPi = 2 * pi;
        BigInteger r = ((value % twoPi) + twoPi) % twoPi; // [0, 2pi)
        if (r > pi) r -= twoPi;                            // (-pi, pi]
        return r;
    }

    // arctan(1/xInv) * scale, via the alternating power series.
    private static BigInteger ArctanInverse(BigInteger xInv, BigInteger scale)
    {
        BigInteger xSquared = xInv * xInv;
        BigInteger power = scale / xInv;
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

    // ---------- fixed-point helpers ----------

    private static BigInteger MulFx(BigInteger a, BigInteger b, BigInteger scale) => DivRound(a * b, scale);

    private static BigInteger DivRound(BigInteger a, BigInteger b)
    {
        BigInteger q = a / b;
        BigInteger r = a - q * b;
        if (BigInteger.Abs(r) * 2 >= BigInteger.Abs(b))
            q += ((a.Sign < 0) ^ (b.Sign < 0)) ? -1 : 1;
        return q;
    }

    // round(n / 2^k) for k >= 0.
    private static BigInteger RoundShr(BigInteger n, int k)
    {
        if (k <= 0) return n << -k;
        BigInteger half = BigInteger.One << (k - 1);
        return (n + half) >> k;
    }
}
