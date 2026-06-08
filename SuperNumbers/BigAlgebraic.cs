using SuperNumbers.Parsing;
using System.Numerics;

namespace SuperNumbers;

/// <summary>
/// An algebraic number: a root of an integer polynomial, the decidable middle
/// ground of the tower.
///
/// It is represented by a squarefree (monic, over Q) annihilating
/// <see cref="Polynomial"/> together with an isolating rational interval that
/// contains exactly one of its real roots -- the value. A <see cref="BigIrrational"/>
/// hint carries the same number for arbitrary-precision printing.
///
/// Unlike <see cref="BigIrrational"/>, equality and ordering here are EXACT and
/// decidable: two algebraic numbers are equal iff gcd(P, Q) has a root in the
/// overlap of their isolating intervals, and otherwise the intervals can always be
/// refined until they separate. The price is expressiveness: only roots of
/// polynomials live here -- sqrt(2), the golden ratio, cube roots -- while pi and e
/// (transcendental) do not. See DOCS.md (Cohen, Loos).
/// </summary>
public sealed class BigAlgebraic : IComparable<BigAlgebraic>, IEquatable<BigAlgebraic>
{
    public Polynomial Poly { get; }
    public BigRational Low { get; private set; }
    public BigRational High { get; private set; }
    public BigIrrational Hint { get; }

    private readonly bool _isRational;
    private readonly BigRational _rationalValue;

    private BigAlgebraic(Polynomial poly, BigRational low, BigRational high, BigIrrational hint)
    {
        Poly = poly;
        Low = low;
        High = high;
        Hint = hint;
        _isRational = false;
        _rationalValue = BigRational.Zero;
    }

    private BigAlgebraic(BigRational value)
    {
        Poly = Polynomial.Linear(value);
        Low = value;
        High = value;
        Hint = BigIrrational.FromRational(value);
        _isRational = true;
        _rationalValue = value;
    }

    // ---------- Construction ----------

    public static BigAlgebraic FromRational(BigRational value) => new(value);
    public static BigAlgebraic FromInteger(BigInteger value) => new(new BigRational(value));

    /// <summary>The principal (non-negative) square root of a non-negative rational.</summary>
    public static BigAlgebraic Sqrt(BigRational value) => Root(value, 2);

    /// <summary>The real n-th root of a rational (principal positive root for the cases used here).</summary>
    public static BigAlgebraic Root(BigRational value, int n)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (value.IsZero) return FromRational(BigRational.Zero);
        BigIrrational hint = BigIrrational.Root(BigIrrational.FromRational(value), n);
        return FromPolyAndHint(Polynomial.PowerMinus(n, value), hint);
    }

    // ---------- Arithmetic ----------

    public static BigAlgebraic operator +(BigAlgebraic a, BigAlgebraic b)
    {
        if (a._isRational && b._isRational) return FromRational(a._rationalValue + b._rationalValue);
        return FromPolyAndHint(AnnihilatingPoly(a, b, product: false), a.Hint + b.Hint);
    }

    public static BigAlgebraic operator -(BigAlgebraic a, BigAlgebraic b) => a + (-b);

    public static BigAlgebraic operator *(BigAlgebraic a, BigAlgebraic b)
    {
        if (a._isRational && b._isRational) return FromRational(a._rationalValue * b._rationalValue);
        return FromPolyAndHint(AnnihilatingPoly(a, b, product: true), a.Hint * b.Hint);
    }

    public static BigAlgebraic operator /(BigAlgebraic a, BigAlgebraic b) => a * b.Reciprocal();

    public static BigAlgebraic operator -(BigAlgebraic a)
    {
        if (a._isRational) return FromRational(-a._rationalValue);
        // P(-x): flip the sign of every odd-degree coefficient.
        var coeffs = new BigRational[a.Poly.Degree + 1];
        for (int i = 0; i <= a.Poly.Degree; i++)
            coeffs[i] = (i % 2 == 0) ? a.Poly[i] : -a.Poly[i];
        return FromPolyAndHint(new Polynomial(coeffs), -a.Hint);
    }

    public BigAlgebraic Reciprocal()
    {
        if (_isRational) return FromRational(_rationalValue.Reciprocal());
        // 1/x is a root of the reversed polynomial.
        var coeffs = new BigRational[Poly.Degree + 1];
        for (int i = 0; i <= Poly.Degree; i++) coeffs[i] = Poly[Poly.Degree - i];
        BigIrrational hint = BigIrrational.One / Hint;
        return FromPolyAndHint(new Polynomial(coeffs), hint);
    }

    // ---------- Comparison (exact and decidable) ----------

    public int Sign()
    {
        if (_isRational) return _rationalValue.Sign;
        while (true)
        {
            if (Low.Sign > 0) return 1;
            if (High.Sign < 0) return -1;
            Refine(); // the root is irrational, hence non-zero: the interval leaves 0 eventually
        }
    }

    public int CompareTo(BigAlgebraic? other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Equals(other)) return 0;
        while (true)
        {
            if (High < other.Low) return -1;
            if (other.High < Low) return 1;
            Refine();
            other.Refine();
        }
    }

    public bool Equals(BigAlgebraic? other)
    {
        if (other is null) return false;
        if (_isRational && other._isRational) return _rationalValue == other._rationalValue;

        BigRational lo = Low > other.Low ? Low : other.Low;
        BigRational hi = High < other.High ? High : other.High;
        if (lo > hi) return false; // disjoint isolating intervals

        Polynomial g = Polynomial.Gcd(Poly, other.Poly);
        if (g.Degree < 1) return false; // no common algebraic root

        if (g.Evaluate(lo).IsZero || g.Evaluate(hi).IsZero) return true;
        return g.SquareFree().CountRootsBetween(lo, hi) >= 1;
    }

    public override bool Equals(object? obj) => obj is BigAlgebraic a && Equals(a);

    public override int GetHashCode() => 0; // equal values may have different representations; constant is safe

    public static bool operator ==(BigAlgebraic a, BigAlgebraic b) => a.Equals(b);
    public static bool operator !=(BigAlgebraic a, BigAlgebraic b) => !a.Equals(b);
    public static bool operator <(BigAlgebraic a, BigAlgebraic b) => a.CompareTo(b) < 0;
    public static bool operator >(BigAlgebraic a, BigAlgebraic b) => a.CompareTo(b) > 0;
    public static bool operator <=(BigAlgebraic a, BigAlgebraic b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BigAlgebraic a, BigAlgebraic b) => a.CompareTo(b) >= 0;

    // ---------- Output ----------

    public bool IsRational => _isRational;
    public BigRational RationalValue => _isRational
        ? _rationalValue
        : throw new InvalidOperationException("This algebraic number is irrational.");

    public string ToDecimalString(int digits) => Hint.ToDecimalString(digits);

    public override string ToString() => _isRational ? _rationalValue.ToString() : Hint.ToString()!;

    // ---------- Interval refinement ----------

    // One bisection step on the isolating interval, by sign of Poly.
    private void Refine()
    {
        if (_isRational) return;
        BigRational mid = (Low + High) / 2;
        int sm = Poly.Evaluate(mid).Sign;
        if (sm == 0) { Low = mid; High = mid; return; } // mid is the exact root
        int sl = Poly.Evaluate(Low).Sign;
        if (sl == sm) Low = mid; else High = mid;
    }

    // ---------- Root isolation and selection ----------

    private static BigAlgebraic FromPolyAndHint(Polynomial poly, BigIrrational hint)
    {
        Polynomial sf = poly.SquareFree();
        List<Candidate> roots = IsolateRoots(sf);
        if (roots.Count == 0)
            throw new InvalidOperationException("Polynomial has no real root for the requested value.");

        Candidate chosen = SelectByHint(roots, hint);
        if (chosen.IsRational) return FromRational(chosen.Point);
        return new BigAlgebraic(sf, chosen.Low, chosen.High, hint);
    }

    private readonly struct Candidate
    {
        public readonly bool IsRational;
        public readonly BigRational Point;
        public readonly BigRational Low;
        public readonly BigRational High;

        private Candidate(bool isRational, BigRational point, BigRational low, BigRational high)
        {
            IsRational = isRational;
            Point = point;
            Low = low;
            High = high;
        }

        public static Candidate Rational(BigRational p) => new(true, p, p, p);
        public static Candidate Interval(BigRational lo, BigRational hi) => new(false, BigRational.Zero, lo, hi);
        public BigRational Anchor => IsRational ? Point : (Low + High) / 2;
    }

    private static Candidate SelectByHint(List<Candidate> roots, BigIrrational hint)
    {
        if (roots.Count == 1) return roots[0];

        // Smallest gap between distinct root anchors sets the precision we need.
        BigRational minGap = BigRational.Zero;
        for (int i = 0; i < roots.Count; i++)
            for (int j = i + 1; j < roots.Count; j++)
            {
                BigRational d = (roots[i].Anchor - roots[j].Anchor).Abs;
                if (minGap.IsZero || d < minGap) minGap = d;
            }

        // Refine the hint until it is closer to one anchor than half the gap.
        int bits = 32;
        BigRational g;
        while (true)
        {
            g = hint.Approximate(bits);
            BigRational tolerance = new BigRational(BigInteger.One, BigInteger.One << bits);
            if (minGap.IsZero || tolerance * 4 < minGap) break;
            bits *= 2;
        }

        Candidate best = roots[0];
        BigRational bestDistance = (g - roots[0].Anchor).Abs;
        for (int i = 1; i < roots.Count; i++)
        {
            BigRational d = (g - roots[i].Anchor).Abs;
            if (d < bestDistance) { bestDistance = d; best = roots[i]; }
        }
        return best;
    }

    private static List<Candidate> IsolateRoots(Polynomial p)
    {
        var result = new List<Candidate>();

        // Peel off rational roots, starting with 0, then by the rational-root theorem.
        Polynomial work = p;
        if (work.Evaluate(BigRational.Zero).IsZero)
        {
            result.Add(Candidate.Rational(BigRational.Zero));
            work = Polynomial.DivRem(work, Polynomial.Linear(BigRational.Zero)).Quotient;
        }

        if (work.Degree >= 1)
        {
            BigInteger[] ints = work.IntegerCoefficients();
            BigInteger constant = BigInteger.Abs(ints[0]);
            BigInteger leading = BigInteger.Abs(ints[^1]);
            foreach (BigInteger pNum in Divisors(constant))
                foreach (BigInteger qDen in Divisors(leading))
                    foreach (int sign in new[] { 1, -1 })
                    {
                        var candidate = new BigRational(sign * pNum, qDen);
                        if (!work.Evaluate(candidate).IsZero) continue;
                        if (result.Exists(c => c.IsRational && c.Point == candidate)) continue;
                        result.Add(Candidate.Rational(candidate));
                    }

            foreach (Candidate c in result.Where(c => c.IsRational).ToList())
                if (work.Evaluate(c.Point).IsZero)
                    work = Polynomial.DivRem(work, Polynomial.Linear(c.Point)).Quotient;
        }

        // Isolate the remaining (irrational) roots; with no rational roots left,
        // a rational midpoint is never a root, so bisection is clean.
        if (work.Degree >= 1)
        {
            BigRational bound = work.CauchyBound();
            var brackets = new List<(BigRational, BigRational)>();
            IsolateBetween(work, -bound, bound, brackets);

            BigRational targetWidth = new BigRational(BigInteger.One, BigInteger.One << 60);
            foreach ((BigRational lo, BigRational hi) in brackets)
            {
                BigRational l = lo, h = hi;
                RefineBracket(p, ref l, ref h, targetWidth);
                result.Add(Candidate.Interval(l, h));
            }
        }

        return result;
    }

    private static void IsolateBetween(Polynomial p, BigRational a, BigRational b, List<(BigRational, BigRational)> output)
    {
        int count = p.CountRootsBetween(a, b);
        if (count == 0) return;
        if (count == 1) { output.Add((a, b)); return; }
        BigRational mid = (a + b) / 2;
        IsolateBetween(p, a, mid, output);
        IsolateBetween(p, mid, b, output);
    }

    // Bisect (lo, hi) using the sign of p until it is narrower than targetWidth.
    private static void RefineBracket(Polynomial p, ref BigRational lo, ref BigRational hi, BigRational targetWidth)
    {
        int signLo = p.Evaluate(lo).Sign;
        while (hi - lo > targetWidth)
        {
            BigRational mid = (lo + hi) / 2;
            int signMid = p.Evaluate(mid).Sign;
            if (signMid == 0) { lo = mid; hi = mid; return; }
            if (signMid == signLo) lo = mid; else hi = mid;
        }
    }

    private static IEnumerable<BigInteger> Divisors(BigInteger n)
    {
        if (n.IsZero) { yield return BigInteger.One; yield break; }
        n = BigInteger.Abs(n);
        for (BigInteger d = BigInteger.One; d * d <= n; d++)
        {
            if ((n % d).IsZero)
            {
                yield return d;
                if (d * d != n) yield return n / d;
            }
        }
    }

    // ---------- Minimal polynomial of a sum or product, via the regular representation ----------

    // The result lives in the algebra Q[a, b] = Q[x]/(A) (x) Q[y]/(B), of dimension m*n.
    // We iterate the powers 1, gamma, gamma^2, ... and stop at the first linear dependence:
    // that yields the (monic) MINIMAL polynomial of gamma directly -- far smaller and cheaper
    // than a full characteristic polynomial (e.g. degree 2 for (sqrt2+sqrt3)^2 instead of 16).
    private static Polynomial AnnihilatingPoly(BigAlgebraic a, BigAlgebraic b, bool product)
    {
        BigRational[] monicA = Monic(a.Poly);
        BigRational[] monicB = Monic(b.Poly);
        int m = monicA.Length - 1;
        int n = monicB.Length - 1;
        int dim = m * n;

        BigRational[] ApplyGamma(BigRational[] v) => product
            ? MultiplyByBeta(MultiplyByAlpha(v, monicA, m, n), monicB, m, n)
            : VectorSum(MultiplyByAlpha(v, monicA, m, n), MultiplyByBeta(v, monicB, m, n));

        BigRational[] one = ZeroVector(dim);
        one[0] = BigRational.One; // a^0 * b^0

        var powers = new List<BigRational[]> { one };
        BigRational[] current = one;
        for (int degree = 1; degree <= dim; degree++)
        {
            current = ApplyGamma(current);
            if (TrySolve(powers, current, out BigRational[] coeffs))
            {
                // gamma^degree = sum coeffs[i]*gamma^i  =>  minimal poly  x^degree - sum coeffs[i]*x^i
                var c = new BigRational[degree + 1];
                for (int i = 0; i < degree; i++) c[i] = -coeffs[i];
                c[degree] = BigRational.One;
                return new Polynomial(c);
            }
            powers.Add(current);
        }
        throw new InvalidOperationException("Failed to find the minimal polynomial.");
    }

    private static BigRational[] Monic(Polynomial p)
    {
        BigRational lead = p.Leading;
        var c = new BigRational[p.Degree + 1];
        for (int i = 0; i <= p.Degree; i++) c[i] = p[i] / lead;
        return c; // c[^1] == 1
    }

    private static BigRational[] ZeroVector(int dim)
    {
        var v = new BigRational[dim];
        for (int i = 0; i < dim; i++) v[i] = BigRational.Zero;
        return v;
    }

    // Element coordinates are flat: index i*n + j stands for a^i * b^j.
    private static BigRational[] MultiplyByAlpha(BigRational[] c, BigRational[] monicA, int m, int n)
    {
        BigRational[] d = ZeroVector(m * n);
        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
            {
                BigRational v = c[i * n + j];
                if (v.IsZero) continue;
                if (i + 1 < m) d[(i + 1) * n + j] += v;
                else
                    for (int k = 0; k < m; k++)
                        d[k * n + j] -= monicA[k] * v; // a^m = -sum monicA[k] a^k
            }
        return d;
    }

    private static BigRational[] MultiplyByBeta(BigRational[] c, BigRational[] monicB, int m, int n)
    {
        BigRational[] d = ZeroVector(m * n);
        for (int i = 0; i < m; i++)
            for (int j = 0; j < n; j++)
            {
                BigRational v = c[i * n + j];
                if (v.IsZero) continue;
                if (j + 1 < n) d[i * n + j + 1] += v;
                else
                    for (int k = 0; k < n; k++)
                        d[i * n + k] -= monicB[k] * v; // b^n = -sum monicB[k] b^k
            }
        return d;
    }

    private static BigRational[] VectorSum(BigRational[] x, BigRational[] y)
    {
        var d = new BigRational[x.Length];
        for (int i = 0; i < x.Length; i++) d[i] = x[i] + y[i];
        return d;
    }

    // Solves [columns] * solution = target over Q (columns are linearly independent).
    // Returns false when target is not in their span (the new power is independent).
    private static bool TrySolve(List<BigRational[]> columns, BigRational[] target, out BigRational[] solution)
    {
        int rows = target.Length;
        int cols = columns.Count;

        // Augmented matrix [columns | target].
        var m = new BigRational[rows, cols + 1];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++) m[r, c] = columns[c][r];
            m[r, cols] = target[r];
        }

        var pivotRowOf = new int[cols];
        for (int c = 0; c < cols; c++) pivotRowOf[c] = -1;

        int pivotRow = 0;
        for (int c = 0; c < cols && pivotRow < rows; c++)
        {
            int sel = -1;
            for (int r = pivotRow; r < rows; r++)
                if (!m[r, c].IsZero) { sel = r; break; }
            if (sel < 0) continue;

            for (int cc = 0; cc <= cols; cc++) (m[pivotRow, cc], m[sel, cc]) = (m[sel, cc], m[pivotRow, cc]);

            BigRational inv = m[pivotRow, c].Reciprocal();
            for (int cc = 0; cc <= cols; cc++) m[pivotRow, cc] *= inv;

            for (int r = 0; r < rows; r++)
            {
                if (r == pivotRow || m[r, c].IsZero) continue;
                BigRational factor = m[r, c];
                for (int cc = 0; cc <= cols; cc++) m[r, cc] -= factor * m[pivotRow, cc];
            }

            pivotRowOf[c] = pivotRow;
            pivotRow++;
        }

        // Inconsistency: a row 0...0 | nonzero means target is independent of the columns.
        for (int r = 0; r < rows; r++)
        {
            bool allZero = true;
            for (int c = 0; c < cols; c++)
                if (!m[r, c].IsZero) { allZero = false; break; }
            if (allZero && !m[r, cols].IsZero) { solution = Array.Empty<BigRational>(); return false; }
        }

        solution = new BigRational[cols];
        for (int c = 0; c < cols; c++)
            solution[c] = pivotRowOf[c] >= 0 ? m[pivotRowOf[c], cols] : BigRational.Zero;
        return true;
    }
}
