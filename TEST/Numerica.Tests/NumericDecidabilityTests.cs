using Numerica;
using Xunit;

namespace Numerica.Tests;

// `Numeric` decides == and < EXACTLY for algebraic formulas (the capability that used
// to live in the deleted BigAlgebric type), and falls back to a numeric comparison only
// for transcendental ones.
public class NumericDecidabilityTests
{
    [Fact]
    public void SqrtTwoSquaredEqualsTwoExactly()
    {
        Assert.True(new Numeric("sqrt(2) * sqrt(2)") == new Numeric("2"));
    }

    [Fact]
    public void SqrtTwoIsNotTwo()
    {
        Assert.True(new Numeric("sqrt(2)") != new Numeric("2"));
    }

    [Fact]
    public void OrderingOfRootsIsExact()
    {
        Assert.True(new Numeric("sqrt(2)") < new Numeric("sqrt(3)"));
        Assert.True(new Numeric("sqrt(3)") > new Numeric("sqrt(2)"));
    }

    [Fact]
    public void GoldenRatioSatisfiesItsEquation()
    {
        // phi^2 == phi + 1
        Assert.True(new Numeric("((1 + sqrt(5))/2)^2") == new Numeric("(1 + sqrt(5))/2 + 1"));
    }

    [Fact]
    public void SumOfRootsSquaredIsExact()
    {
        Assert.True(new Numeric("(sqrt(2) + sqrt(3))^2") == new Numeric("5 + 2*sqrt(6)"));
    }

    [Fact]
    public void CubeRootCubedIsExactlySeven()
    {
        Assert.True(new Numeric("(7^(1/3))^3") == new Numeric("7"));
    }

    [Fact]
    public void TranscendentalsStillCompareNumerically()
    {
        Assert.True(new Numeric("pi") > new Numeric("3"));
        Assert.True(new Numeric("exp(1)") > new Numeric("e - 1/1000"));
    }

    // The exact examples from the README / discussion, kept verbatim as one regression.
    [Fact]
    public void DocumentedExamples()
    {
        Assert.True(new Numeric("sqrt(2) * sqrt(2)") == new Numeric("2"));                    // decided, exact
        Assert.True(new Numeric("(7^(1/3))^3") == new Numeric("7"));                          // exact
        Assert.True(new Numeric("((1+sqrt(5))/2)^2") == new Numeric("(1+sqrt(5))/2 + 1"));    // exact
        Assert.True(new Numeric("sqrt(2)") < new Numeric("sqrt(3)"));                         // exact
        Assert.True(new Numeric("pi") > new Numeric("3"));                                    // numeric fallback
    }
}
