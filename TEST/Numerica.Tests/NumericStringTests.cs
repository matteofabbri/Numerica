using System.Numerics;
using System.Text;
using Numerica;
using Xunit;

namespace Numerica.Tests;

// string(<STRING>) -> UTF-8 bytes -> big-endian unsigned BigInteger.
public class NumericStringTests
{
    private static BigInteger Encode(string s)
        => new BigInteger(Encoding.UTF8.GetBytes(s), isUnsigned: true, isBigEndian: true);

    [Fact]
    public void RawString()
    {
        Assert.True(new Numeric("string(hello)") == new Numeric(Encode("hello")));
    }

    [Fact]
    public void SingleAsciiCharIsItsCodePoint()
    {
        // 'A' is byte 0x41 = 65.
        Assert.True(new Numeric("string(A)") == new Numeric(65));
    }

    [Fact]
    public void EmptyStringIsZero()
    {
        Assert.True(new Numeric("string()") == new Numeric(0));
    }

    [Fact]
    public void QuotedStringWithEscapes()
    {
        Assert.True(new Numeric("string(\"a\\nb\")") == new Numeric(Encode("a\nb")));
        Assert.True(new Numeric("string(\"tab\\there\")") == new Numeric(Encode("tab\there")));
    }

    [Fact]
    public void QuotedUnicodeEscape()
    {
        Assert.True(new Numeric("string(\"\\u03A9\")") == new Numeric(Encode("Ω")));
    }

    [Fact]
    public void ParenthesisInsideQuotedString()
    {
        Assert.True(new Numeric("string(\"a)b\")") == new Numeric(Encode("a)b")));
    }

    [Fact]
    public void MultiByteUtf8()
    {
        // "Ω" is UTF-8 0xCE 0xA9 -> 0xCEA9 = 52905.
        Assert.True(new Numeric("string(Ω)") == new Numeric(52905));
    }

    [Fact]
    public void StringIsRational()
    {
        Assert.True(new Numeric("string(hello)").IsRational);
    }

    [Fact]
    public void QuotedSimpleWordIsValid()
    {
        var n = new Numeric("string(\"ciao\")");
        Assert.True(n == new Numeric(Encode("ciao")));
        Assert.Equal("1667850607", n.ToDecimalString(0)); // 0x6369616F
    }
}
