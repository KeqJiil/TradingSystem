using Google.Protobuf.WellKnownTypes;
using Stock.Infrastructure.ExternalEvents;
using Xunit;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Stock.Tests.Infrastructure.ExternalEvents;

public class StockCreatedEventMapperTests
{
    [Fact]
    public void MapFrom_ShouldMapAllFieldsCorrectly()
    {
        var aggregateId = Guid.NewGuid();
        var externalEvent = new Proto.StockCreatedEvent
        {
            AggregateId = aggregateId.ToString(),
            Name = "Test Stock",
            IsOpenToTrade = true,
            Currency = "Usd",
            TradingStartTime = Duration.FromTimeSpan(TimeOnly.MinValue.ToTimeSpan()),
            TradingCloseTime = Duration.FromTimeSpan(TimeOnly.MaxValue.ToTimeSpan())
        };

        var applicationEvent = StockCreatedEventMapper.MapFrom(externalEvent);

        Assert.Equal(aggregateId, applicationEvent.AggregateId);
        Assert.Equal(externalEvent.Name, applicationEvent.Name);
        Assert.Equal(externalEvent.IsOpenToTrade, applicationEvent.IsOpenToTrade);
        Assert.Equal(externalEvent.Currency, applicationEvent.Currency);
        Assert.Equal(TimeOnly.MinValue, applicationEvent.TradingStartTime);
        Assert.Equal(TimeOnly.MaxValue, applicationEvent.TradingCloseTime);
    }

    [Fact]
    public void MapToExternal_ShouldMapAllFieldsCorrectly()
    {
        var applicationEvent = new Stock.Application.Events.StockCreatedEvent(
            Guid.NewGuid(),
            "Test Stock",
            true,
            "Usd",
            TimeOnly.MinValue,
            TimeOnly.MaxValue
        );

        var externalEvent = StockCreatedEventMapper.MapToExternal(applicationEvent);

        Assert.Equal(applicationEvent.AggregateId.ToString(), externalEvent.AggregateId);
        Assert.Equal(applicationEvent.Name, externalEvent.Name);
        Assert.Equal(applicationEvent.IsOpenToTrade, externalEvent.IsOpenToTrade);
        Assert.Equal(applicationEvent.Currency, externalEvent.Currency);
        Assert.Equal(applicationEvent.TradingStartTime.ToTimeSpan(), externalEvent.TradingStartTime.ToTimeSpan());
        Assert.Equal(applicationEvent.TradingCloseTime.ToTimeSpan(), externalEvent.TradingCloseTime.ToTimeSpan());
    }
}
