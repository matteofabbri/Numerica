using Numerica;
using Xunit;

namespace Numerica.Tests;

// Implicit multiplication by juxtaposition: a factor followed by something that begins
// with a letter or '(' multiplies. Numbers and a leading '-' never trigger it, so the
// existing arithmetic keeps its meaning.
public class NumericImplicitMultiplicationTests
{
    [Theory]
    [InlineData("2pi", "2 * pi")]
    [InlineData("2sqrt(2)", "2 * sqrt(2)")]
    [InlineData("2(3 + 4)", "14")]
    [InlineData("(1 + 2)(3 + 4)", "21")]
    [InlineData("3pi^2", "3 * pi^2")]          // '^' binds inside the implicit factor
    [InlineData("2i", "2 * i")]                // imaginary unit
    [InlineData("2pi e", "2 * pi * e")]        // chained juxtaposition
    public void JuxtapositionMultiplies(string formula, string equivalent)
    {
        Assert.Equal(
            new Numeric(equivalent).ToDecimalString(40),
            new Numeric(formula).ToDecimalString(40));
    }

    [Fact]
    public void ImplicitTimesIsLeftAssociativeAtMultiplicativeLevel()
    {
        // "1/2pi" parses as "(1/2)*pi", not "1/(2*pi)".
        Assert.True(new Numeric("1/2pi") == new Numeric("pi/2"));
    }

    // The cases that must NOT become an implicit product.
    [Fact]
    public void SubtractionIsNotImplicitMultiplication()
    {
        Assert.True(new Numeric("2 - 3") == new Numeric("-1"));
        Assert.True(new Numeric("pi - 1") == new Numeric("pi + (-1)"));
    }

    [Fact]
    public void TwoBareNumbersDoNotMultiply()
    {
        // "2 2" has no operator and the right side is a number, so it is a syntax error.
        Assert.Throws<Sprache.ParseException>(() => new Numeric("2 2"));
    }

    [Fact]
    public void ScientificNotationStillWins()
    {
        Assert.True(new Numeric("2e3") == new Numeric("2000"));   // not 2*e*3
    }

    [Fact]
    public void ExplicitTimesWithUnaryMinusStillWorks()
    {
        Assert.True(new Numeric("2 * -3") == new Numeric("-6"));
    }
}
