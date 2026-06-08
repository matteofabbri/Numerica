using Numerica;
using Xunit;

namespace Numerica.Tests;

// Literal forms accepted by the Numeric constructor / parser.
public class NumericLiteralTests
{
    [Fact]
    public void Booleans()
    {
        Assert.True(new Numeric("true") == new Numeric("1"));
        Assert.True(new Numeric("false") == new Numeric("0"));
        Assert.True(new Numeric("true").IsRational);
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData("2343454675675678567567546545345634534557678678678678678",
                "2343454675675678567567546545345634534557678678678678678")]
    public void IntegersAndBigIntegers(string formula, string expected)
    {
        Assert.Equal(expected, new Numeric(formula).ToDecimalString(0));
    }

    [Theory]
    [InlineData("0xFF", "255")]
    [InlineData("0x10", "16")]
    [InlineData("0xff", "255")]
    public void Hexadecimal(string formula, string expectedInteger)
    {
        Assert.True(new Numeric(formula) == new Numeric(expectedInteger));
    }

    [Theory]
    [InlineData("1.23e5", "123000")]
    [InlineData("-1.23e4", "-12300")]
    [InlineData("6.02E3", "6020")]
    [InlineData("1.5e-2", "0.015")]
    public void ScientificNotation(string formula, string expected)
    {
        Assert.True(new Numeric(formula) == new Numeric(expected));
    }

    [Fact]
    public void UnicodePiSymbol()
    {
        // "π" is decoded to the character pi by the C# compiler before it reaches us.
        Assert.True(new Numeric("π") == new Numeric("pi"));
        Assert.True(new Numeric("π") > new Numeric("3"));
    }

    [Fact]
    public void UnicodeEscapeInTheStringIsDecoded()
    {
        // Here the literal backslash-u sequence actually reaches the parser and is decoded.
        Assert.True(new Numeric("\\u03C0") == new Numeric("pi"));
    }

    [Fact]
    public void UnicodeOmegaIsTheOmegaConstant()
    {
        // "Ω" is the character Omega -> the omega constant, defined by Omega * e^Omega = 1.
        Assert.True(new Numeric("Ω * exp(Ω)") == new Numeric("1"));
        Assert.True(new Numeric("Ω").IsIrrational);
        Assert.True(new Numeric("Ω") > new Numeric("0"));
        Assert.True(new Numeric("Ω") < new Numeric("1"));
    }
}
