using SuperNumbers;
using Xunit;

namespace SuperNumbers.Tests;

public class AlgebraicTests
{
    // The headline identity, now DECIDABLE: sqrt(2)*sqrt(2) == 2 exactly.
    [Fact]
    public void SqrtTwoSquaredIsExactlyTwo()
    {
        BigAlgebraic root2 = BigAlgebraic.Sqrt(2);
        BigAlgebraic product = root2 * root2;
        Assert.True(product == BigAlgebraic.FromInteger(2));
        Assert.True(product.IsRational);
        Assert.Equal(new BigRational(2), product.RationalValue);
    }

    [Fact]
    public void SqrtTwoIsIrrational()
    {
        BigAlgebraic root2 = BigAlgebraic.Sqrt(2);
        Assert.False(root2.IsRational);
        Assert.False(root2 == BigAlgebraic.FromInteger(2));
    }

    [Fact]
    public void OrderingIsDecidable()
    {
        BigAlgebraic root2 = BigAlgebraic.Sqrt(2);
        BigAlgebraic root3 = BigAlgebraic.Sqrt(3);
        Assert.True(root2 < root3);
        Assert.True(root3 > root2);
        Assert.Equal(-1, root2.CompareTo(root3));
    }

    [Fact]
    public void SignIsDecidable()
    {
        BigAlgebraic value = BigAlgebraic.Sqrt(2) - BigAlgebraic.FromInteger(1); // ~0.414
        Assert.Equal(1, value.Sign());
        Assert.Equal(-1, (-value).Sign());
    }

    // Golden ratio phi satisfies phi^2 == phi + 1, exactly.
    [Fact]
    public void GoldenRatioIdentity()
    {
        BigAlgebraic phi = (BigAlgebraic.FromInteger(1) + BigAlgebraic.Sqrt(5)) / BigAlgebraic.FromInteger(2);
        BigAlgebraic lhs = phi * phi;
        BigAlgebraic rhs = phi + BigAlgebraic.FromInteger(1);
        Assert.True(lhs == rhs);
    }

    // (sqrt(2) + sqrt(3))^2 == 5 + 2*sqrt(6), exactly.
    [Fact]
    public void SumOfRootsSquared()
    {
        BigAlgebraic s = BigAlgebraic.Sqrt(2) + BigAlgebraic.Sqrt(3);
        BigAlgebraic lhs = s * s;
        BigAlgebraic rhs = BigAlgebraic.FromInteger(5) + BigAlgebraic.FromInteger(2) * BigAlgebraic.Sqrt(6);
        Assert.True(lhs == rhs);
    }

    [Fact]
    public void CubeRootCubedIsExact()
    {
        BigAlgebraic c = BigAlgebraic.Root(7, 3);
        BigAlgebraic cubed = c * c * c;
        Assert.True(cubed == BigAlgebraic.FromInteger(7));
    }

    [Fact]
    public void DecimalExpansionMatches()
    {
        BigAlgebraic root2 = BigAlgebraic.Sqrt(2);
        Assert.Equal("1.4142135623730950488", root2.ToDecimalString(19));
    }
}
