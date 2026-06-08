using SuperNumbers;
using Xunit;

namespace SuperNumbers.Tests;

public class TranscendentalTests
{
    // exp(ln(5)) == 5
    [Fact]
    public void ExpLnRoundTrip()
    {
        BigIrrational five = BigIrrational.FromInteger(5);
        BigIrrational back = BigIrrational.Exp(BigIrrational.Ln(five));
        Assert.True(back.ApproximatelyEquals(five, 120));
    }

    // sin(x)^2 + cos(x)^2 == 1
    [Fact]
    public void PythagoreanIdentity()
    {
        BigIrrational x = new BigRational(7, 5); // 1.4
        BigIrrational s = BigIrrational.Sin(x);
        BigIrrational c = BigIrrational.Cos(x);
        BigIrrational sum = BigIrrational.Power(s, 2) + BigIrrational.Power(c, 2);
        Assert.True(sum.ApproximatelyEquals(BigIrrational.One, 120));
    }

    // tan(x) == sin(x)/cos(x)
    [Fact]
    public void TangentDefinition()
    {
        BigIrrational x = new BigRational(1, 2);
        BigIrrational lhs = BigIrrational.Tan(x);
        BigIrrational rhs = BigIrrational.Sin(x) / BigIrrational.Cos(x);
        Assert.True(lhs.ApproximatelyEquals(rhs, 120));
    }

    [Theory]
    [InlineData(1, "2.7182818284590452353602874713526624977572")] // e^1
    public void ExpDigits(int x, string expected)
    {
        BigIrrational e = BigIrrational.Exp(BigIrrational.FromInteger(x));
        Assert.Equal(expected, e.ToDecimalString(40));
    }

    [Theory]
    [InlineData(2, "0.6931471805599453094172321214581765680755")] // ln 2
    public void LnDigits(int x, string expected)
    {
        BigIrrational l = BigIrrational.Ln(BigIrrational.FromInteger(x));
        Assert.Equal(expected, l.ToDecimalString(40));
    }

    // atan(1) == pi/4
    [Fact]
    public void AtanOneIsQuarterPi()
    {
        BigIrrational lhs = BigIrrational.Atan(BigIrrational.One);
        BigIrrational rhs = BigIrrational.Pi / 4;
        Assert.True(lhs.ApproximatelyEquals(rhs, 120));
    }
}
