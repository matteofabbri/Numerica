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
