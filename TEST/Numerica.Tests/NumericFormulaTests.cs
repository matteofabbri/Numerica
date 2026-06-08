using System.Numerics;
using Numerica;
using Xunit;

namespace Numerica.Tests;

// Formulas built straight from strings via `new Numeric("...")` -- the type stays a
// suspended calculation and only becomes a value when one of the As* methods is called.
// (These are the formulas that used to live in the BasicSample.)
public class NumericFormulaTests
{
    // ---- rational level: AsRational() is exact ----

    [Theory]
    [InlineData("1/3 + 1/6", 1, 2)]
    [InlineData("(2/3)^3", 8, 27)]
    [InlineData("2 + 3 * 4", 14, 1)]
    [InlineData("2^3^2", 512, 1)]          // right associative: 2^(3^2)
    [InlineData("-2^2", -4, 1)]            // -(2^2)
    [InlineData("0.5 + 0.25", 3, 4)]
    [InlineData("abs(-3/4)", 3, 4)]
    public void RationalFormulas(string formula, int num, int den)
    {
        var value = new Numeric(formula);
        Assert.Equal(new BigRational(num, den), value.AsRational());
    }

    // ---- irrational level: the symbolic tree cancels exactly ----

    [Fact]
    public void SqrtTwoSquaredIsTwo()
    {
        var value = new Numeric("sqrt(2) * sqrt(2)");
        Assert.Equal("2", value.AsIrrational().ToString());
    }

    [Fact]
    public void GoldenRatioIdentityIsZero()
    {
        var value = new Numeric("((1 + sqrt(5))/2)^2 - (1 + sqrt(5))/2 - 1");
        Assert.Equal("0", value.AsIrrational().ToString());
    }

    [Fact]
    public void SumOfRootsSquared()
    {
        var lhs = new Numeric("(sqrt(2) + sqrt(3))^2");
        var rhs = new Numeric("5 + 2*sqrt(6)");
        Assert.True(lhs == rhs);
    }

    [Fact]
    public void SqrtTwoDigits()
    {
        Assert.Equal("1.4142135623730950488", new Numeric("sqrt(2)").ToDecimalString(19));
    }

    // ---- transcendental identities (numeric, high precision) ----

    [Fact]
    public void ExpLnRoundTrip()
    {
        Assert.True(new Numeric("exp(ln(5))") == new Numeric("5"));
    }

    [Fact]
    public void PythagoreanIdentity()
    {
        Assert.True(new Numeric("sin(1)^2 + cos(1)^2") == Numeric.One);
    }

    [Fact]
    public void AtanOneIsQuarterPi()
    {
        Assert.True(new Numeric("4 * atan(1)") == new Numeric("pi"));
    }

    // ---- complex level ----

    [Fact]
    public void EulerIdentity()
    {
        // exp(i*pi) + 1 == 0
        BigComplex value = new Numeric("exp(i * pi) + 1").AsComplex();
        Assert.True(value.ApproximatelyEquals(BigComplex.Zero, 80));
    }

    [Fact]
    public void ComplexMagnitude()
    {
        var value = new Numeric("abs(3 + 4*i)");
        Assert.Equal("5", value.AsComplex().Real.ToString());
    }

    [Fact]
    public void ImaginaryUnitSquared()
    {
        var value = new Numeric("i^2");
        Assert.Equal("-1", value.AsComplex().ToString());
    }

    // ---- extended functions: roots, logs, inverse/hyperbolic trig ----

    // Roots of rationals fold to an EXACT value at construction, so `==` is decidable.
    [Theory]
    [InlineData("cbrt(27)", "3")]
    [InlineData("root(81, 4)", "3")]
    [InlineData("root(32, 5)", "2")]
    public void ExactRoots(string formula, string expected)
    {
        Assert.True(new Numeric(formula) == new Numeric(expected));
    }

    // Logs of perfect powers are integer-valued but TRANSCENDENTAL (not folded), so we
    // compare the decimal expansion -- the honest test for non-algebraic formulas.
    [Theory]
    [InlineData("log10(1000)", "3")]
    [InlineData("log2(64)", "6")]
    [InlineData("logb(81, 3)", "4")]
    [InlineData("log(125, 5)", "3")]
    public void LogarithmValues(string formula, string expectedInteger)
    {
        Assert.Equal(expectedInteger, new Numeric(formula).ToDecimalString(0));
    }

    [Fact]
    public void Atan2SelectsTheSecondQuadrant()
    {
        // atan2(1, -1) = 3*pi/4.
        AssertSameValue("atan2(1, -1)", "3 * pi / 4");
    }

    [Fact]
    public void AsinOfOneIsHalfPi()
    {
        // asin(1) = atan2(1, 0) folds to pi/2 symbolically, so this is exact.
        Assert.True(new Numeric("2 * asin(1)") == new Numeric("pi"));
    }

    [Fact]
    public void AcosIsTheAsinComplement()
    {
        // asin(x) + acos(x) = pi/2; the atan terms cancel symbolically.
        Assert.True(new Numeric("asin(1/2) + acos(1/2)") == new Numeric("pi/2"));
    }

    [Fact]
    public void HyperbolicPythagoreanIdentity()
    {
        // cosh(x)^2 - sinh(x)^2 = 1.
        AssertSameValue("cosh(2)^2 - sinh(2)^2", "1");
    }

    [Fact]
    public void AtanhRoundTripsThroughTanh()
    {
        AssertSameValue("tanh(atanh(1/3))", "1/3");
    }

    [Fact]
    public void AsinhRoundTripsThroughSinh()
    {
        AssertSameValue("sinh(asinh(2))", "2");
    }

    // ---- new constants ----

    [Fact]
    public void TauIsTwoPi()
    {
        // tau = pi*2 and 2*pi build the same symbolic tree, so this is exact.
        Assert.True(new Numeric("tau") == new Numeric("2 * pi"));
    }

    [Fact]
    public void GoldenRatioConstantSatisfiesItsIdentity()
    {
        // phi^2 - phi - 1 == 0, exactly (phi stays symbolic).
        Assert.Equal("0", new Numeric("phi^2 - phi - 1").AsIrrational().ToString());
    }

    // ---- modulo and variadic reductions (exact, rational) ----

    [Theory]
    [InlineData("7 % 3", 1, 1)]
    [InlineData("-7 % 3", -1, 1)]          // sign follows the dividend
    [InlineData("7 % -3", 1, 1)]
    [InlineData("mod(7, 3)", 1, 1)]
    [InlineData("(1/2) % (1/3)", 1, 6)]    // rational remainder
    [InlineData("10 % 2", 0, 1)]
    public void ModuloFormulas(string formula, int num, int den)
    {
        Assert.Equal(new BigRational(num, den), new Numeric(formula).AsRational());
    }

    [Fact]
    public void ModuloBindsLikeMultiplication()
    {
        // 2 + 7 % 3 == 2 + (7 % 3) == 3, not (2 + 7) % 3.
        Assert.Equal(new BigRational(3), new Numeric("2 + 7 % 3").AsRational());
    }

    [Theory]
    [InlineData("min(3, 1, 2)", 1, 1)]
    [InlineData("max(3, 1, 2)", 3, 1)]
    [InlineData("min(-1/2, 1/3)", -1, 2)]
    [InlineData("max(1/4, 1/3, 1/5)", 1, 3)]
    [InlineData("gcd(24, 36)", 12, 1)]
    [InlineData("gcd(24, 36, 60)", 12, 1)]
    [InlineData("lcm(4, 6)", 12, 1)]
    [InlineData("lcm(2, 3, 4)", 12, 1)]
    public void ReductionFormulas(string formula, int num, int den)
    {
        Assert.Equal(new BigRational(num, den), new Numeric(formula).AsRational());
    }

    [Fact]
    public void MinMaxSelectIrrationalArguments()
    {
        // The chosen value stays the exact symbolic argument.
        Assert.Equal("√(2)", new Numeric("max(sqrt(2), 1.4)").AsIrrational().ToString());
        Assert.Equal("√(2)", new Numeric("min(sqrt(2), sqrt(3))").AsIrrational().ToString());
    }

    // Numeric (decimal-expansion) equality, the honest test for transcendental formulas.
    private static void AssertSameValue(string formula, string expected, int digits = 40)
        => Assert.Equal(
            new Numeric(expected).ToDecimalString(digits),
            new Numeric(formula).ToDecimalString(digits));

    // ---- ordering and INumber<T> integration ----

    [Fact]
    public void OrderingIsNumeric()
    {
        Assert.True(new Numeric("sqrt(2)") < new Numeric("sqrt(3)"));
        Assert.True(new Numeric("pi") > new Numeric("3"));
    }

    [Fact]
    public void WorksThroughGenericMath()
    {
        // SumAll is constrained to INumber<T>; Numeric plugs straight in.
        Numeric total = SumAll(new Numeric("1/2"), new Numeric("1/3"), new Numeric("1/6"));
        Assert.True(total == Numeric.One);
    }

    [Fact]
    public void AbsThroughGenericMath()
    {
        Numeric value = Numeric.Abs(new Numeric("-7/2"));
        Assert.Equal(new BigRational(7, 2), value.AsRational());
    }

    private static T SumAll<T>(params T[] values) where T : INumber<T>
    {
        T acc = T.Zero;
        foreach (T v in values) acc += v;
        return acc;
    }
}
