using Numerica;
using Xunit;

namespace Numerica.Tests;

// Catalan's constant G = (pi/8)*ln(2+sqrt(3)) + (3/8)*sum 1/((2k+1)^2 C(2k,k)).
public class NumericCatalanTests
{
    // G = 0.9159655941772190150546035149323841107741493742816721342664981196...
    [Theory]
    [InlineData(10, "0.9159655942")]
    [InlineData(30, "0.915965594177219015054603514932")]
    [InlineData(50, "0.91596559417721901505460351493238411077414937428167")]
    public void Catalan_matches_known_digits(int digits, string expected)
    {
        Assert.Equal(expected, new Numeric("catalan").ToDecimalString(digits));
    }

    [Fact]
    public void Catalan_is_irrational_and_in_range()
    {
        var g = new Numeric("catalan");
        Assert.True(g.IsIrrational);
        Assert.True(g > new Numeric("0.915") && g < new Numeric("0.916"));
    }

    [Fact]
    public void Catalan_composes_in_formulas()
    {
        // 8*G - 3*sum-part should equal pi*ln(2+sqrt(3)) by construction; check the
        // simpler identity that catalan participates in arithmetic and implicit mult.
        Assert.Equal(
            new Numeric("2 * catalan").ToDecimalString(40),
            new Numeric("catalan + catalan").ToDecimalString(40));
        Assert.Equal(
            new Numeric("2catalan").ToDecimalString(40),       // implicit multiplication
            new Numeric("2 * catalan").ToDecimalString(40));
    }
}
