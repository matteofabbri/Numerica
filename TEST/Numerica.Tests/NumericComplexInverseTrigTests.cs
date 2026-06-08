using Numerica;
using Xunit;

namespace Numerica.Tests;

// asin/acos/atan now evaluate on the complex level too (previously real-only). A real
// formula that steps out of the reals (asin(2), sqrt(-1), ln(-1)) is promoted to its
// complex interpretation instead of throwing.
public class NumericComplexInverseTrigTests
{
    // sin(asin(z)) == z and cos(acos(z)) == z hold for EVERY complex z, so they are a
    // branch-cut-proof way to exercise the complex inverse trig.
    [Theory]
    [InlineData("2")]                 // real, but outside [-1, 1] -> genuinely complex
    [InlineData("3 + 2*i")]
    [InlineData("-1 + i")]
    public void Sin_undoes_Asin(string z)
    {
        Assert.True(new Numeric($"sin(asin({z}))") == new Numeric(z));
    }

    [Theory]
    [InlineData("2")]
    [InlineData("1 - i")]
    [InlineData("0.5 + 2*i")]
    public void Cos_undoes_Acos(string z)
    {
        Assert.True(new Numeric($"cos(acos({z}))") == new Numeric(z));
    }

    // tan(atan(z)) == z for every z except ±i.
    [Theory]
    [InlineData("i/2")]
    [InlineData("2 + i")]
    [InlineData("3")]
    public void Tan_undoes_Atan(string z)
    {
        Assert.True(new Numeric($"tan(atan({z}))") == new Numeric(z));
    }

    [Fact]
    public void Asin_of_two_has_the_known_value()
    {
        // asin(2) = pi/2 - i*acosh(2) = pi/2 - i*ln(2 + sqrt(3)).
        Assert.True(new Numeric("asin(2)") == new Numeric("pi/2 - i*ln(2 + sqrt(3))"));
    }

    // ---- real evaluations that leave the reals now promote to complex ----

    [Fact]
    public void SqrtOfNegativeOneIsI()
    {
        Assert.True(new Numeric("sqrt(-1)") == new Numeric("i"));
    }

    [Fact]
    public void LnOfNegativeOneIsIPi()
    {
        Assert.True(new Numeric("ln(-1)") == new Numeric("i * pi"));
    }

    [Fact]
    public void RealInverseTrigStillEvaluatesReal()
    {
        // In-domain calls stay on the real level and exact identities still hold.
        Assert.True(new Numeric("2 * asin(1)") == new Numeric("pi"));
        Assert.True(new Numeric("4 * atan(1)") == new Numeric("pi"));
    }
}
