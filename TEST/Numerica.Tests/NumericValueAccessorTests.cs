using System.Numerics;
using Numerica;
using Xunit;

namespace Numerica.Tests;

// Public, BCL-typed value accessors: TryGetRational / TryGetInteger. They never leak the
// internal number tower and are at least as strong as IsRational.
public class NumericValueAccessorTests
{
    [Theory]
    [InlineData("5", 5, 1)]
    [InlineData("2/6", 1, 3)]            // reduced
    [InlineData("-7/4", -7, 4)]          // sign on the numerator, denominator positive
    [InlineData("0.25", 1, 4)]
    [InlineData("sqrt(2)*sqrt(2)", 2, 1)] // algebraic collapse to a rational
    public void TryGetRational_returns_reduced_fraction(string formula, int num, int den)
    {
        Assert.True(new Numeric(formula).TryGetRational(out BigInteger n, out BigInteger d));
        Assert.Equal(new BigInteger(num), n);
        Assert.Equal(new BigInteger(den), d);
    }

    [Theory]
    [InlineData("sqrt(2)")]
    [InlineData("pi")]
    [InlineData("1 + sqrt(3)")]
    public void TryGetRational_is_false_for_irrationals(string formula)
    {
        Assert.False(new Numeric(formula).TryGetRational(out _, out _));
    }

    [Fact]
    public void TryGetRational_recognises_a_rational_hidden_in_an_algebraic_form()
    {
        // 1/(1+sqrt2) + 1/(1-sqrt2) = -2, decided by the algebraic engine.
        Assert.True(new Numeric("1/(1 + sqrt(2)) + 1/(1 - sqrt(2))")
            .TryGetRational(out BigInteger n, out BigInteger d));
        Assert.Equal(new BigInteger(-2), n);
        Assert.Equal(BigInteger.One, d);
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("6!", 720)]
    [InlineData("10/2", 5)]
    [InlineData("2^10", 1024)]
    public void TryGetInteger_returns_whole_numbers(string formula, int expected)
    {
        Assert.True(new Numeric(formula).TryGetInteger(out BigInteger v));
        Assert.Equal(new BigInteger(expected), v);
    }

    [Theory]
    [InlineData("1/3")]
    [InlineData("0.5")]
    [InlineData("sqrt(2)")]
    public void TryGetInteger_is_false_for_non_integers(string formula)
    {
        Assert.False(new Numeric(formula).TryGetInteger(out _));
    }

    [Fact]
    public void TryGetRational_handles_big_values_exactly()
    {
        var n = new Numeric("100000000000000000000000000000 / 4"); // 2.5e28
        Assert.True(n.TryGetRational(out BigInteger num, out BigInteger den));
        Assert.Equal(BigInteger.Parse("25000000000000000000000000000"), num);
        Assert.Equal(BigInteger.One, den);
    }
}
