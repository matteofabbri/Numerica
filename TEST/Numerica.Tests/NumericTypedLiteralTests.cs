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

    [Theory]
    [InlineData("rational(3/7)", "3/7")]
    [InlineData("rational(\"3/7\")", "3/7")]
    [InlineData("rational(-10/4)", "-5/2")]   // reduced to lowest terms
    [InlineData("rational(5)", "5")]
    public void Rational(string formula, string expected)
    {
        Assert.True(new Numeric(formula) == new Numeric(expected));
        Assert.True(new Numeric(formula).IsRational);
    }

    [Theory]
    [InlineData("hex(FF)", "255")]
    [InlineData("hex(0xFF)", "255")]            // optional 0x prefix
    [InlineData("hex(-1A)", "-26")]
    [InlineData("bin(1010)", "10")]
    [InlineData("bin(0b1111_1111)", "255")]     // 0b prefix and '_' separators
    [InlineData("oct(17)", "15")]
    [InlineData("oct(0o755)", "493")]
    public void BaseIntegers(string formula, string expected)
    {
        Assert.True(new Numeric(formula) == new Numeric(expected));
    }

    [Fact]
    public void TimeSpanIsTicks()
    {
        // 1 hour = 3600 s = 36_000_000_000 ticks of 100 ns
        Assert.True(new Numeric("timespan(1:00:00)") == new Numeric("36000000000"));
        // 1.00:00:00 is one day, matching datetime subtraction over a day
        Assert.True(new Numeric("timespan(1.00:00:00)") == new Numeric("864000000000"));
    }

    [Fact]
    public void GuidIsItsBigEndianInteger()
    {
        // The canonical text read as a single 128-bit hex integer.
        Assert.True(new Numeric("guid(00000000-0000-0000-0000-000000000001)") == new Numeric("1"));
        Assert.True(new Numeric("guid(\"00000000-0000-0000-0000-0000000000ff\")") == new Numeric("255"));
    }

    [Theory]
    [InlineData("complex(3+4i)", "3 + 4*i")]
    [InlineData("complex(\"3-4i\")", "3 - 4*i")]
    [InlineData("complex(4i)", "4*i")]
    [InlineData("complex(i)", "i")]
    [InlineData("complex(-i)", "-i")]
    [InlineData("complex(2.5+0.5i)", "2.5 + 0.5*i")]
    [InlineData("complex(7)", "7")]            // a bare real is still fine
    public void Complex(string formula, string equivalent)
    {
        Assert.True(new Numeric(formula) == new Numeric(equivalent));
    }

    [Fact]
    public void ComplexMagnitudeIsExact()
    {
        // |3 + 4i| folds to exactly 5.
        Assert.Equal("5", new Numeric("abs(complex(3+4i))").AsComplex().Real.ToString());
        Assert.True(new Numeric("complex(3+4i)").IsComplex);
    }
}
