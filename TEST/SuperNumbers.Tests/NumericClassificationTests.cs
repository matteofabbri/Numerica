using SuperNumbers;
using Xunit;

namespace SuperNumbers.Tests;

// The public IsRational / IsIrrational / IsComplex properties.
public class NumericClassificationTests
{
    [Fact]
    public void RationalFormulaIsRational()
    {
        var n = new Numeric("1/3 + 1/6");
        Assert.True(n.IsRational);
        Assert.False(n.IsIrrational);
        Assert.False(n.IsComplex);
    }

    [Fact]
    public void AlgebraicallyRationalIsDetected()
    {
        // The value is 2, even though the formula looks irrational.
        var n = new Numeric("sqrt(2) * sqrt(2)");
        Assert.True(n.IsRational);
        Assert.False(n.IsIrrational);
    }

    [Fact]
    public void IrrationalFormulaIsIrrational()
    {
        var n = new Numeric("(1 + sqrt(5)) / 2");
        Assert.True(n.IsIrrational);
        Assert.False(n.IsRational);
        Assert.False(n.IsComplex);
    }

    [Fact]
    public void ComplexFormulaIsComplex()
    {
        var n = new Numeric("2 + 3*i");
        Assert.True(n.IsComplex);
        Assert.False(n.IsRational);
        Assert.False(n.IsIrrational);
    }

    [Fact]
    public void TranscendentalIsClassifiedIrrational()
    {
        var n = new Numeric("pi");
        Assert.True(n.IsIrrational);
        Assert.False(n.IsComplex);
    }
}
