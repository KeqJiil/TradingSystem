using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class StockCreatedDlqEventToCommandMapperTests
{
    [Fact]
    public void Map_ReturnsCommand()
    {
        var data = new StockCreatedEvent(Guid.NewGuid(), "Some name", true, "Usd", TimeOnly.MinValue,
            TimeOnly.MaxValue);
        var mapper = new StockCreatedDlqEventToCommandMapper();

        var result = mapper.Map(data);

        Assert.Equal(data.AggregateId, result.AggregateId);
        Assert.Equal(data.IsOpenToTrade, result.IsOpenToTrade);
        Assert.Equal(data.TradingCloseTime, result.TradingCloseTime);
        Assert.Equal(data.Name, result.Name);
        Assert.Equal(data.TradingStartTime, result.TradingStartTime);
        Assert.Equal(data.Currency, result.Currency);
    }
}