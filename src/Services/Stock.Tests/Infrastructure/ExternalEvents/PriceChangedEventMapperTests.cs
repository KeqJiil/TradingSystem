using Stock.Infrastructure.ExternalEvents;
using Xunit;

namespace Stock.Tests.Infrastructure.ExternalEvents;

public class PriceChangedEventMapperTests
{
    [Fact]
    public void MapFrom_ShouldMapFieldsCorrectly()
    {
        var externalEvent = new PriceChangedEvent(
            Guid.NewGuid(),
            10.5m,
            1,
            DateTimeOffset.UtcNow
        );

        var internalEvent = PriceChangedEventMapper.MapFrom(externalEvent);

        Assert.Equal(externalEvent.AggregateId, internalEvent.AggregateId);
        Assert.Equal(externalEvent.PriceChange, internalEvent.PriceChange);
        Assert.Equal(externalEvent.Version, internalEvent.Version);
        Assert.Equal(externalEvent.OccuredAt, internalEvent.OccuredAt);
    }

    [Fact]
    public void MapToExternal_ShouldMapFieldsCorrectly()
    {
        var internalEvent = new Stock.Application.Events.PriceChangedEvent(
            Guid.NewGuid(),
            10.5m,
            1,
            DateTimeOffset.UtcNow
        );

        var externalEvent = PriceChangedEventMapper.MapToExternal(internalEvent);

        Assert.Equal(internalEvent.AggregateId, externalEvent.AggregateId);
        Assert.Equal(internalEvent.PriceChange, externalEvent.PriceChange);
        Assert.Equal(internalEvent.Version, externalEvent.Version);
        Assert.Equal(internalEvent.OccuredAt, externalEvent.OccuredAt);
    }
}