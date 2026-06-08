using Numerica;
using Xunit;

namespace Numerica.Tests;

// GetHashCode hashes the VALUE (not the formula), and ToValueString serializes the
// COMPUTED value (reduced rational when possible, formula otherwise) and round-trips.
public class NumericHashAndValueTests
{
    // ---- GetHashCode: equal values hash equal, regardless of how they were written ----

    [Theory]
    [InlineData("1", "1.0")]
    [InlineData("2/6", "1/3")]
    [InlineData("10", "5 + 5")]
    [InlineData("sqrt(2)", "sqrt(8)/2")]      // exactly equal, differently shaped
    [InlineData("sqrt(2)*sqrt(2)", "2")]      // algebraic collapse to a rational
    public void Equal_values_hash_equal(string a, string b)
    {
        var x = new Numeric(a);
        var y = new Numeric(b);

        Assert.True(x == y);                                  // precondition: they ARE equal
        Assert.Equal(x.GetHashCode(), y.GetHashCode());       // ... so they must hash equal
    }

    [Fact]
    public void Different_values_usually_hash_differently()
    {
        // Not a contract (collisions are legal) but a smoke test that it is not constant.
        Assert.NotEqual(new Numeric("1").GetHashCode(), new Numeric("2").GetHashCode());
        Assert.NotEqual(new Numeric("sqrt(2)").GetHashCode(), new Numeric("sqrt(3)").GetHashCode());
    }

    [Fact]
    public void Hashes_compose_in_a_dictionary()
    {
        var map = new Dictionary<Numeric, string>
        {
            [new Numeric("1/3")] = "third",
            [new Numeric("sqrt(2)")] = "root-two",
        };

        Assert.Equal("third", map[new Numeric("2/6")]);       // hits via value hash + ==
        Assert.Equal("root-two", map[new Numeric("sqrt(8)/2")]);
    }

    // ---- ToValueString: the COMPUTED value, reduced, and round-trippable ----

    [Theory]
    [InlineData("5", "5")]
    [InlineData("2/6", "1/3")]                // reduced
    [InlineData("0.5 + 0.25", "3/4")]         // evaluated, not the original formula
    [InlineData("sqrt(2)*sqrt(2)", "2")]      // algebraic collapse -> rational literal
    public void Rational_values_serialize_as_reduced_rational(string formula, string expected)
    {
        Assert.Equal(expected, new Numeric(formula).ToValueString());
    }

    [Fact]
    public void Irrational_values_fall_back_to_the_formula()
    {
        var v = new Numeric("sqrt(2)");
        Assert.Equal(v.Formula, v.ToValueString());           // no exact finite rational form
    }

    [Theory]
    [InlineData("5")]
    [InlineData("2/6")]
    [InlineData("0.5 + 0.25")]
    [InlineData("sqrt(2)")]
    [InlineData("sqrt(2)*sqrt(2)")]
    [InlineData("1 + sqrt(3)")]
    public void ToValueString_round_trips_through_the_constructor(string formula)
    {
        var original = new Numeric(formula);
        var restored = new Numeric(original.ToValueString());
        Assert.True(original == restored);
    }
}
