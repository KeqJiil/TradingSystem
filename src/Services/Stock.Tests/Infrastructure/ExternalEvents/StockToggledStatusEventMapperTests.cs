using Stock.Infrastructure.ExternalEvents;
using Xunit;

namespace Stock.Tests.Infrastructure.ExternalEvents;

public class StockToggledStatusEventMapperTests
{
    [Fact]
    public void MapFrom_ShouldMapFieldsCorrectly()
    {
        var externalEvent = new StockToggledStatusEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            true,
            7
        );

        var applicationEvent = StockToggledStatusEventMapper.MapFrom(externalEvent);

        Assert.Equal(externalEvent.AggregateId, applicationEvent.AggregateId);
        Assert.Equal(externalEvent.ToggledAt, applicationEvent.ToggledAt);
        Assert.Equal(externalEvent.IsOpenToTrade, applicationEvent.IsOpenToTrade);
        Assert.Equal(externalEvent.StatusVersion, applicationEvent.StatusVersion);
    }

    [Fact]
    public void MapToExternal_ShouldMapFieldsCorrectly()
    {
        var applicationEvent = new Stock.Application.Events.StockToggledStatusEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            true,
            7
        );

        var externalEvent = StockToggledStatusEventMapper.MapToExternal(applicationEvent);

        Assert.Equal(applicationEvent.AggregateId, externalEvent.AggregateId);
        Assert.Equal(applicationEvent.ToggledAt, externalEvent.ToggledAt);
        Assert.Equal(applicationEvent.IsOpenToTrade, externalEvent.IsOpenToTrade);
        Assert.Equal(applicationEvent.StatusVersion, externalEvent.StatusVersion);
    }
}