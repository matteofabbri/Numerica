using System;
using SuperNumbers;
using Xunit;

namespace SuperNumbers.Tests;

// datetime(<VALUE>) parses any .NET / ISO-8601 date representation into its UTC tick count.
public class NumericDateTimeTests
{
    [Fact]
    public void Iso8601UtcMatchesTickCount()
    {
        var expected = new Numeric(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero));
        Assert.True(new Numeric("datetime(2024-01-15T10:30:00Z)") == expected);
    }

    [Fact]
    public void Iso8601WithOffsetIsNormalizedToUtc()
    {
        var expected = new Numeric(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(2)));
        Assert.True(new Numeric("datetime(2024-01-15T10:30:00+02:00)") == expected);
    }

    [Fact]
    public void DateOnlyIsMidnightUtc()
    {
        var expected = new Numeric(new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        Assert.True(new Numeric("datetime(2024-01-15)") == expected);
    }

    [Fact]
    public void DotNetInvariantFormatIsAccepted()
    {
        var expected = new Numeric(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero));
        Assert.True(new Numeric("datetime(01/15/2024 10:30:00)") == expected);
    }

    [Fact]
    public void TypedConstructorMatchesTheExpression()
    {
        var typed = new Numeric(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
        Assert.True(typed == new Numeric("datetime(2024-01-15T10:30:00Z)"));
    }

    [Fact]
    public void DifferenceOfDatesIsADuration()
    {
        // one day apart -> exactly TimeSpan.FromDays(1).Ticks
        Numeric diff = new Numeric("datetime(2024-01-02) - datetime(2024-01-01)");
        Assert.True(diff == new Numeric(TimeSpan.FromDays(1).Ticks));
    }

    [Fact]
    public void DateTimeIsRational()
    {
        Assert.True(new Numeric("datetime(2024-01-15T10:30:00Z)").IsRational);
    }
}
