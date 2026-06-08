using Numerica;
using Xunit;

namespace Numerica.Tests;

// Power a^b across the tower: integer, rational, irrational and complex exponents, plus
// the pow(a, b) function form (sugar for a ^ b).
public class NumericPowerTests
{
    [Theory]
    [InlineData("2^10", "1024")]
    [InlineData("2^(1/2)", "sqrt(2)")]          // rational exponent stays algebraic
    [InlineData("(-8)^(1/3)", "-2")]            // odd root of a negative is real
    [InlineData("27^(2/3)", "9")]
    public void AlgebraicExponents(string formula, string expected)
    {
        Assert.True(new Numeric(formula) == new Numeric(expected));
    }

    [Fact]
    public void IrrationalExponentEvaluatesViaExpLog()
    {
        // 2^pi = exp(pi * ln 2); compare the decimal expansion (transcendental).
        Assert.Equal(
            new Numeric("exp(pi * ln(2))").ToDecimalString(40),
            new Numeric("2^pi").ToDecimalString(40));
    }

    [Fact]
    public void PowFunctionIsSugarForCaret()
    {
        Assert.True(new Numeric("pow(2, 10)") == new Numeric("1024"));
        Assert.True(new Numeric("pow(2, 1/2)") == new Numeric("sqrt(2)"));
        Assert.Equal(
            new Numeric("2^pi").ToDecimalString(30),
            new Numeric("pow(2, pi)").ToDecimalString(30));
    }

    [Fact]
    public void NegativeBaseWithRealExponentIsComplex()
    {
        // (-2)^pi leaves the reals (ln of a negative), so it evaluates on the complex level.
        var v = new Numeric("(-2)^pi");
        Assert.True(v.IsComplex);
        // (-2)^pi = exp(pi * ln(-2)) = exp(pi*(ln2 + i*pi)).
        Assert.True(v == new Numeric("exp(pi * (ln(2) + i*pi))"));
    }

    [Fact]
    public void ComplexBaseAndExponent()
    {
        // i^i = exp(i * ln i) = exp(i * i*pi/2) = exp(-pi/2), a positive real.
        Assert.True(new Numeric("i^i") == new Numeric("exp(-pi/2)"));
    }
}
