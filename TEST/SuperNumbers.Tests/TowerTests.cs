using SuperNumbers;
using SuperNumbers.Parsing;
using Xunit;

namespace SuperNumbers.Tests;

// The symbolic identities the library is built to honour, fixed as regressions.
public class TowerTests
{
    [Fact]
    public void RationalIsExact()
    {
        BigRational sum = new BigRational(1, 3) + new BigRational(1, 6);
        Assert.Equal(new BigRational(1, 2), sum);
        Assert.True(new BigRational(1, 3) == new BigRational(2, 6));
    }

    [Fact]
    public void SqrtTwoSquaredSimplifiesToTwo()
    {
        BigIrrational root2 = BigIrrational.Sqrt(2);
        Assert.Equal("2", (root2 * root2).ToString());
    }

    [Fact]
    public void GoldenRatioCancelsToZero()
    {
        BigIrrational phi = (BigIrrational.One + BigIrrational.Sqrt(5)) / 2;
        BigIrrational identity = BigIrrational.Power(phi, 2) - phi - BigIrrational.One;
        Assert.Equal("0", identity.ToString());
    }

    [Fact]
    public void ComplexMagnitudeIsExact()
    {
        BigComplex z = new(BigIrrational.FromInteger(3), BigIrrational.FromInteger(4));
        Assert.Equal("5", z.Magnitude().ToString());
    }

    [Fact]
    public void ImaginaryUnitSquaredIsMinusOne()
    {
        BigComplex i2 = BigComplex.ImaginaryUnit * BigComplex.ImaginaryUnit;
        Assert.Equal("-1", i2.ToString());
    }

    [Fact]
    public void SqrtTwoDigits()
    {
        Assert.Equal("1.4142135623730950488", BigIrrational.Sqrt(2).ToDecimalString(19));
    }
}

// The string -> expression -> value pipeline (Sprache), at all three levels.
public class ParserTests
{
    [Fact]
    public void ParsesAndEvaluatesRational()
    {
        Expr e = Expr.Parse("1/3 + 1/6");
        Assert.Equal(new BigRational(1, 2), e.ToRational());
    }

    [Fact]
    public void ParsesRespectsPrecedence()
    {
        Expr e = Expr.Parse("2 + 3 * 4");
        Assert.Equal(new BigRational(14), e.ToRational());
    }

    [Fact]
    public void ParsesUnaryAndPower()
    {
        Assert.Equal(new BigRational(-4), Expr.Parse("-2^2").ToRational());   // -(2^2)
        Assert.Equal(new BigRational(512), Expr.Parse("2^3^2").ToRational()); // right assoc: 2^(3^2)
    }

    [Fact]
    public void ParsesIrrationalExpression()
    {
        Expr e = Expr.Parse("sqrt(2) * sqrt(2)");
        Assert.Equal("2", e.ToIrrational().ToString());
    }

    [Fact]
    public void EulerIdentityIsApproximatelyZero()
    {
        // exp(i*pi) + 1 == 0
        BigComplex value = Expr.Parse("exp(i * pi) + 1").ToComplex();
        Assert.True(value.ApproximatelyEquals(BigComplex.Zero, 80));
    }

    [Fact]
    public void DecimalLiteralsParse()
    {
        Expr e = Expr.Parse("0.5 + 0.25");
        Assert.Equal(new BigRational(3, 4), e.ToRational());
    }

    [Fact]
    public void SameTreeEvaluatesAtThreeLevels()
    {
        Expr e = Expr.Parse("1/2 + 1/2");
        Assert.Equal(new BigRational(1), e.ToRational());
        Assert.Equal("1", e.ToIrrational().ToString());
        Assert.Equal("1", e.ToComplex().ToString());
    }
}
