using Stock.Application.Queries.GetHourlyReadModel;
using Stock.Application.Queries.GetWeeklyReadModel;
using Xunit;

namespace Stock.Tests.Application.Queries;

public class QueryValidatorsTests
{
    private static readonly DateTimeOffset From = new(2026, 3, 5, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly StartDate = new(2026, 3, 5);

    [Theory]
    [InlineData(60, true)]
    [InlineData(7 * 24 * 60, true)]
    [InlineData(7 * 24 * 60 + 1, false)]
    [InlineData(0, false)]
    [InlineData(-60, false)]
    public void GetHourly_RequiresToAfterFrom_WithinSevenDays(int rangeMinutes, bool valid)
    {
        var result = new GetHourlyReadModelValidator()
            .Validate(new GetHourlyReadModelQuery(Guid.NewGuid(), From, From.AddMinutes(rangeMinutes)));

        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.All(result.Errors, e => Assert.Equal("To", e.PropertyName));
    }

    [Fact]
    public void GetHourly_RejectsEmptyAggregateId()
    {
        var result = new GetHourlyReadModelValidator()
            .Validate(new GetHourlyReadModelQuery(Guid.Empty, From, From.AddHours(1)));

        Assert.Contains(result.Errors, e => e.PropertyName == "AggregateId");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(366, true)]
    [InlineData(367, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void GetWeekly_RequiresEndAfterStart_WithinOneYear(int rangeDays, bool valid)
    {
        var result = new GetWeeklyReadModelValidator()
            .Validate(new GetWeeklyReadModelQuery(Guid.NewGuid(), StartDate, StartDate.AddDays(rangeDays)));

        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.All(result.Errors, e => Assert.Equal("To", e.PropertyName));
    }

    [Fact]
    public void GetWeekly_RejectsEmptyAggregateId()
    {
        var result = new GetWeeklyReadModelValidator()
            .Validate(new GetWeeklyReadModelQuery(Guid.Empty, StartDate, StartDate.AddDays(7)));

        Assert.Contains(result.Errors, e => e.PropertyName == "AggregateId");
    }
}
