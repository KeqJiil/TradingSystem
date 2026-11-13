using Google.Protobuf.WellKnownTypes;
using Stock.Infrastructure.ExternalEvents;
using Xunit;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Tests.Infrastructure.ExternalEvents;

public class StockToggledStatusEventMapperTests
{
    [Fact]
    public void MapFrom_ShouldMapFieldsCorrectly()
    {
        var aggregateId = Guid.NewGuid();
        var toggledAt = DateTimeOffset.UtcNow;
        var externalEvent = new Proto.StockToggledStatusEvent
        {
            AggregateId = aggregateId.ToString(),
            ToggledAt = Timestamp.FromDateTimeOffset(toggledAt),
            IsOpenToTrade = true,
            StatusVersion = 7
        };

        var applicationEvent = StockToggledStatusEventMapper.MapFrom(externalEvent);

        Assert.Equal(aggregateId, applicationEvent.AggregateId);
        Assert.Equal(toggledAt, applicationEvent.ToggledAt);
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

        Assert.Equal(applicationEvent.AggregateId.ToString(), externalEvent.AggregateId);
        Assert.Equal(applicationEvent.ToggledAt, externalEvent.ToggledAt.ToDateTimeOffset());
        Assert.Equal(applicationEvent.IsOpenToTrade, externalEvent.IsOpenToTrade);
        Assert.Equal(applicationEvent.StatusVersion, externalEvent.StatusVersion);
    }
}
