using Numerica;
using Xunit;

namespace Numerica.Tests;

// base64(<VALUE>) decodes the bytes (standard or URL-safe, with/without padding,
// with/without quotes) and reads them as an order-preserving base-256 fraction in
// [0, 1) -- exactly like string(...) does with its UTF-8 bytes.
public class NumericBase64Tests
{
    [Fact]
    public void StandardBase64MatchesTheDecodedString()
    {
        // "TWFu" is base64 for "Man".
        Assert.True(new Numeric("base64(TWFu)") == new Numeric("string(Man)"));
        Assert.True(new Numeric("base64(\"TWFu\")") == new Numeric("string(\"Man\")"));
    }

    [Fact]
    public void RoundTripsThroughString()
    {
        // "SGVsbG8=" is base64 for "Hello".
        Assert.True(new Numeric("base64(\"SGVsbG8=\")") == new Numeric("string(\"Hello\")"));
    }

    [Fact]
    public void MissingPaddingIsAccepted()
    {
        // "TWE" (no '=') is base64 for "Ma".
        Assert.True(new Numeric("base64(TWE)") == new Numeric("string(Ma)"));
    }

    [Theory]
    // bytes {0xFF, 0xFE} -> 65534/65536; standard "//4=" uses '/', URL-safe "__4" uses '_'
    [InlineData("base64(\"//4=\")", "base64(__4)", "65534/65536")]
    // bytes {0xFA} -> 250/256; standard "+g==" uses '+', URL-safe "-g" uses '-'
    [InlineData("base64(\"+g==\")", "base64(-g)", "250/256")]
    public void StandardAndUrlSafeAgree(string standard, string urlSafe, string expected)
    {
        Assert.True(new Numeric(standard) == new Numeric(urlSafe));
        Assert.True(new Numeric(standard) == new Numeric(expected));
    }

    [Fact]
    public void Base64IsRational()
    {
        Assert.True(new Numeric("base64(TWFu)").IsRational);
    }
}
