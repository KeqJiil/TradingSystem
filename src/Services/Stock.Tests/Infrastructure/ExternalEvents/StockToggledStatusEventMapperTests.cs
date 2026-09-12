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
            DateTimeOffset.UtcNow
        );

        var applicationEvent = StockToggledStatusEventMapper.MapFrom(externalEvent);

        Assert.Equal(externalEvent.AggregateId, applicationEvent.AggregateId);
        Assert.Equal(externalEvent.ToggledAt, applicationEvent.ToggledAt);
    }

    [Fact]
    public void MapToExternal_ShouldMapFieldsCorrectly()
    {
        var applicationEvent = new Stock.Application.Events.StockToggledStatusEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );

        var externalEvent = StockToggledStatusEventMapper.MapToExternal(applicationEvent);

        Assert.Equal(applicationEvent.AggregateId, externalEvent.AggregateId);
        Assert.Equal(applicationEvent.ToggledAt, externalEvent.ToggledAt);
    }
}