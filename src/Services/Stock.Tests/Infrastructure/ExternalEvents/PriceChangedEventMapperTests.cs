using Google.Protobuf.WellKnownTypes;
using Stock.Infrastructure.ExternalEvents;
using Xunit;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Tests.Infrastructure.ExternalEvents;

public class PriceChangedEventMapperTests
{
    [Fact]
    public void MapFrom_ShouldMapFieldsCorrectly()
    {
        var aggregateId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var externalEvent = new Proto.PriceChangedEvent
        {
            AggregateId = aggregateId.ToString(),
            PriceChange = "10.5",
            Version = 1,
            OccurredAt = Timestamp.FromDateTimeOffset(occurredAt)
        };

        var internalEvent = PriceChangedEventMapper.MapFrom(externalEvent);

        Assert.Equal(aggregateId, internalEvent.AggregateId);
        Assert.Equal(10.5m, internalEvent.PriceChange);
        Assert.Equal(1, internalEvent.Version);
        Assert.Equal(occurredAt, internalEvent.OccuredAt);
    }

    [Fact]
    public void MapFrom_VersionNotSet_MapsToNull()
    {
        var externalEvent = new Proto.PriceChangedEvent
        {
            AggregateId = Guid.NewGuid().ToString(),
            PriceChange = "10.5",
            OccurredAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };

        var internalEvent = PriceChangedEventMapper.MapFrom(externalEvent);

        Assert.Null(internalEvent.Version);
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

        Assert.Equal(internalEvent.AggregateId.ToString(), externalEvent.AggregateId);
        Assert.Equal("10.5", externalEvent.PriceChange);
        Assert.Equal(internalEvent.Version, externalEvent.Version);
        Assert.Equal(internalEvent.OccuredAt, externalEvent.OccurredAt.ToDateTimeOffset());
    }

    [Fact]
    public void MapToExternal_VersionNull_VersionNotSet()
    {
        var internalEvent = new Stock.Application.Events.PriceChangedEvent(
            Guid.NewGuid(),
            10.5m,
            null,
            DateTimeOffset.UtcNow
        );

        var externalEvent = PriceChangedEventMapper.MapToExternal(internalEvent);

        Assert.False(externalEvent.HasVersion);
    }
}
