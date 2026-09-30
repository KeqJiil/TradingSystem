using Stock.Infrastructure.ExternalEvents;
using Xunit;

namespace Stock.Tests.Infrastructure.ExternalEvents;

public class StockCreatedEventMapperTests
{
    [Fact]
    public void MapFrom_ShouldMapAllFieldsCorrectly()
    {
        var externalEvent = new StockCreatedEvent(
            Guid.NewGuid(),
            "Test Stock",
            true,
            "Usd",
            TimeOnly.MinValue,
            TimeOnly.MaxValue
        );

        var applicationEvent = StockCreatedEventMapper.MapFrom(externalEvent);

        Assert.Equal(externalEvent.AggregateId, applicationEvent.AggregateId);
        Assert.Equal(externalEvent.Name, applicationEvent.Name);
        Assert.Equal(externalEvent.IsOpenToTrade, applicationEvent.IsOpenToTrade);
        Assert.Equal(externalEvent.Currency, applicationEvent.Currency);
        Assert.Equal(externalEvent.TradingStartTime, applicationEvent.TradingStartTime);
        Assert.Equal(externalEvent.TradingCloseTime, applicationEvent.TradingCloseTime);
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

        Assert.Equal(applicationEvent.AggregateId, externalEvent.AggregateId);
        Assert.Equal(applicationEvent.Name, externalEvent.Name);
        Assert.Equal(applicationEvent.IsOpenToTrade, externalEvent.IsOpenToTrade);
        Assert.Equal(applicationEvent.Currency, externalEvent.Currency);
        Assert.Equal(applicationEvent.TradingStartTime, externalEvent.TradingStartTime);
        Assert.Equal(applicationEvent.TradingCloseTime, externalEvent.TradingCloseTime);
    }
}