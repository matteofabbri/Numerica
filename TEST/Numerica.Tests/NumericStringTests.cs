using Numerica;
using Numerica.Utils;
using Xunit;

namespace Numerica.Tests;

// string(<STRING>) -> UTF-8 bytes -> order-preserving base-256 fraction in [0, 1):
// the bytes b0 b1 ... become 0.b0 b1 ... = (big-endian integer) / 256^count, so the
// numbers sort in the strings' alphabetical (lexicographic) order. The byte <-> fraction
// <-> string conversions live in StringCodec; AsSpan() exposes a value's bytes.
public class NumericStringTests
{
    // The fraction string(...) builds for a given string, as a Numeric, for an
    // expected-value check.
    private static Numeric Encode(string s) => new(StringCodec.Encode(s));

    [Fact]
    public void StringOrder()
    {
        var startArray = Enumerable.Range(0, 256).Select(i => new Numeric($"string(\"FAKE_{i}\")")).ToArray();
        var sortedNum = startArray.OrderBy(n => n).ToArray();
        var sortedString = startArray.Select(n => StringCodec.Decode(n.AsSpan())).OrderBy(s => s).ToArray();

        Assert.Equal(sortedString, sortedNum.Select(n => StringCodec.Decode(n.AsSpan())));
    }

    [Fact]
    public void RawString()
    {
        Assert.True(new Numeric("string(hello)") == Encode("hello"));
    }

    [Fact]
    public void SingleAsciiCharIsItsByteOverScale()
    {
        // 'A' is byte 0x41 = 65, read as the fraction 65/256.
        Assert.True(new Numeric("string(A)") == new Numeric("65/256"));
    }

    [Fact]
    public void EmptyStringIsZero()
    {
        Assert.True(new Numeric("string()") == new Numeric(0));
    }

    [Fact]
    public void QuotedStringWithEscapes()
    {
        Assert.True(new Numeric("string(\"a\\nb\")") == Encode("a\nb"));
        Assert.True(new Numeric("string(\"tab\\there\")") == Encode("tab\there"));
    }

    [Fact]
    public void QuotedUnicodeEscape()
    {
        Assert.True(new Numeric("string(\"\\u03A9\")") == Encode("Ω"));
    }

    [Fact]
    public void ParenthesisInsideQuotedString()
    {
        Assert.True(new Numeric("string(\"a)b\")") == Encode("a)b"));
    }

    [Fact]
    public void MultiByteUtf8()
    {
        // "Ω" is UTF-8 0xCE 0xA9 -> 0xCEA9 = 52905, read as the fraction 52905/65536.
        Assert.True(new Numeric("string(Ω)") == new Numeric("52905/65536"));
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
        Assert.True(n == Encode("ciao"));
        Assert.True(n == new Numeric("1667850607/4294967296")); // 0x6369616F / 256^4
    }
}
