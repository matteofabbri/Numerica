using Numerica;
using Xunit;

namespace Numerica.Tests;

// Postfix factorial "n!" -- exact, non-negative integers only. Sugar for fact(n).
public class NumericFactorialTests
{
    [Theory]
    [InlineData("0!", "1")]
    [InlineData("1!", "1")]
    [InlineData("5!", "120")]
    [InlineData("fact(5)", "120")]      // function spelling
    [InlineData("factorial(6)", "720")]
    [InlineData("10!", "3628800")]
    [InlineData("(2 + 1)!", "6")]       // factorial of a grouped expression
    public void FactorialValues(string formula, string expected)
    {
        Assert.True(new Numeric(formula) == new Numeric(expected));
    }

    [Fact]
    public void FactorialBindsTighterThanPowerAndUnaryMinus()
    {
        Assert.True(new Numeric("2^3!") == new Numeric("64"));   // 2^(3!) = 2^6
        Assert.True(new Numeric("-3!") == new Numeric("-6"));    // -(3!)
    }

    [Fact]
    public void FactorialChainsAsIteratedFactorial()
    {
        // Numerica has no double-factorial: n!! means (n!)!.
        Assert.True(new Numeric("3!!") == new Numeric("720"));   // (3!)! = 6! = 720
    }

    [Fact]
    public void FactorialComposesInArithmetic()
    {
        Assert.True(new Numeric("3! + 4!") == new Numeric("30"));
        Assert.Equal(new BigRational(120), new Numeric("5!").AsRational());
    }

    [Fact]
    public void FactorialOfNonIntegerThrows()
    {
        Assert.Throws<NotSupportedException>(() => new Numeric("(1/2)!").AsRational());
    }

    [Fact]
    public void FactorialOfNegativeThrows()
    {
        Assert.Throws<NotSupportedException>(() => new Numeric("fact(-1)").AsRational());
    }

    [Fact]
    public void FactorialRoundTripsThroughFormulaAndValueString()
    {
        var n = new Numeric("5!");
        Assert.Equal("5!", n.Formula);            // postfix spelling preserved
        Assert.Equal("120", n.ToValueString());   // serialized as its computed value
        Assert.True(new Numeric(n.Formula) == n);
    }
}
