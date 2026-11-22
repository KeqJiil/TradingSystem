using Order.Domain;
using Order.Domain.Specifications;
using Xunit;

namespace Order.Tests.Domain.Specifications;

public class PlaceOrderSpecificationTests
{
    private static readonly TimeOnly Start = new(9, 0);
    private static readonly TimeOnly End = new(17, 0);
    private static readonly TimeOnly NightStart = new(22, 0);
    private static readonly TimeOnly NightEnd = new(6, 0);

    [Fact]
    public void Validate_OpenAndInsideTradingHours_ReturnsSuccess()
    {
        var result = PlaceOrderSpecification.Validate(true, Start, End, new TimeOnly(12, 0));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_NotOpenToTrade_ReturnsViolation()
    {
        var result = PlaceOrderSpecification.Validate(false, Start, End, new TimeOnly(12, 0));

        AssertViolation(result, "Stock is not open to trade.");
    }

    [Theory]
    [InlineData(9, 0, true)]
    [InlineData(16, 59, true)]
    [InlineData(8, 59, false)]
    [InlineData(17, 0, false)]
    [InlineData(17, 1, false)]
    public void Validate_DaySession_UsesHalfOpenInterval(int hour, int minute, bool expectedSuccess)
    {
        var result = PlaceOrderSpecification.Validate(true, Start, End, new TimeOnly(hour, minute));

        AssertOutcome(result, expectedSuccess);
    }

    [Theory]
    [InlineData(22, 0, true)]
    [InlineData(23, 0, true)]
    [InlineData(5, 0, true)]
    [InlineData(6, 0, false)]
    [InlineData(12, 0, false)]
    [InlineData(21, 59, false)]
    public void Validate_OvernightSession_UsesHalfOpenInterval(int hour, int minute, bool expectedSuccess)
    {
        var result = PlaceOrderSpecification.Validate(true, NightStart, NightEnd, new TimeOnly(hour, minute));

        AssertOutcome(result, expectedSuccess);
    }

    private static void AssertOutcome(Result result, bool expectedSuccess)
    {
        if (expectedSuccess)
            Assert.True(result.IsSuccess);
        else
            AssertViolation(result, "Stock is outside trading hours.");
    }

    private static void AssertViolation(Result result, string message)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Violation, result.Error.Type);
        Assert.Equal(message, result.Error.Message);
    }
}
