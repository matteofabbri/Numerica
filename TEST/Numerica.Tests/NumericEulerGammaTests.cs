using Numerica;
using Xunit;

namespace Numerica.Tests;

// The Euler-Mascheroni constant gamma, evaluated via Brent-McMillan.
public class NumericEulerGammaTests
{
    // gamma = 0.57721566490153286060651209008240243104215933593992...
    [Theory]
    [InlineData(10, "0.5772156649")]
    [InlineData(30, "0.577215664901532860606512090082")]
    [InlineData(50, "0.57721566490153286060651209008240243104215933593992")]
    public void Gamma_matches_known_digits(int digits, string expected)
    {
        Assert.Equal(expected, new Numeric("egamma").ToDecimalString(digits));
    }

    [Theory]
    [InlineData("egamma")]
    [InlineData("gamma")]
    [InlineData("γ")]
    public void All_spellings_agree(string name)
    {
        Assert.Equal(
            new Numeric("egamma").ToDecimalString(40),
            new Numeric(name).ToDecimalString(40));
    }

    [Fact]
    public void Gamma_is_irrational_and_in_range()
    {
        var g = new Numeric("egamma");
        Assert.True(g.IsIrrational);
        Assert.True(g > new Numeric("0.577") && g < new Numeric("0.578"));
    }

    [Fact]
    public void Gamma_composes_in_formulas()
    {
        // exp(gamma) ~ 1.7810724179901979852..., and gamma participates in arithmetic.
        Assert.True(new Numeric("2egamma") == new Numeric("egamma + egamma")); // implicit mult
        Assert.Equal(
            "1.781072417990197985236504103107",
            new Numeric("exp(egamma)").ToDecimalString(30));
    }
}
