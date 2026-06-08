using Numerica;
using Xunit;

namespace Numerica.Tests;

// The uniform typed-literal forms: bool(...), char(...), int(...), float(...),
// each accepted with or without double quotes (only " is used).
public class NumericTypedLiteralTests
{
    [Theory]
    [InlineData("bool(true)", "1")]
    [InlineData("bool(\"true\")", "1")]
    [InlineData("bool(false)", "0")]
    [InlineData("bool(\"false\")", "0")]
    [InlineData("bool(TRUE)", "1")] // case-insensitive, like bool.Parse
    public void Bool(string formula, string expected)
    {
        Assert.True(new Numeric(formula) == new Numeric(expected));
    }

    [Theory]
    [InlineData("char(A)", 65)]
    [InlineData("char(\"A\")", 65)]
    [InlineData("char(\"Ω\")", 937)]      // code point U+03A9 (not the UTF-8 bytes)
    [InlineData("char(\"😀\")", 128512)]   // U+1F600, given as a surrogate pair
    public void Char(string formula, int expectedCodePoint)
    {
        Assert.True(new Numeric(formula) == new Numeric(expectedCodePoint));
    }

    [Fact]
    public void CharDiffersFromString()
    {
        // char gives the code point; string gives the UTF-8 byte sequence.
        Assert.True(new Numeric("char(\"Ω\")") == new Numeric(937));
        Assert.True(new Numeric("string(\"Ω\")") == new Numeric(52905));
    }

    [Theory]
    [InlineData("int(34567)", "34567")]
    [InlineData("int(\"3467\")", "3467")]
    [InlineData("int(-5)", "-5")]
    public void Int(string formula, string expected)
    {
        Assert.True(new Numeric(formula) == new Numeric(expected));
        Assert.True(new Numeric(formula).IsRational);
    }

    [Fact]
    public void FloatSeparatorIsCultureInvariant()
    {
        // comma and dot mean the same thing, regardless of OS culture
        Assert.True(new Numeric("float(\"2134,23\")") == new Numeric("2134.23"));
        Assert.True(new Numeric("float(2134,23)") == new Numeric("2134.23"));
        Assert.True(new Numeric("float(\"2134.23\")") == new Numeric("2134.23"));
    }

    [Theory]
    [InlineData("float(3,14)", "157/50")]   // 3.14 = 157/50
    [InlineData("float(-0,5)", "-1/2")]
    [InlineData("float(1.5e3)", "1500")]
    public void FloatValues(string formula, string expectedRational)
    {
        Assert.True(new Numeric(formula) == new Numeric(expectedRational));
    }
}
