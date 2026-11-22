using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Order.Infrastructure.Messaging.Serialization;
using Order.Infrastructure.Messaging.StockEvents;
using Xunit;
using Proto = TradingSystem.Contracts.ProtobufClasses.Stock;

namespace Order.Tests.Infrastructure.Messaging.Serialization;

public class ProtobufParsableSerializerTests
{
    private readonly ProtobufParsableSerializer<StockCreatedEvent> _serializer = new();

    [Fact]
    public void Deserialize_ValidEvent_ReturnsMappedMessage()
    {
        var stockId = Guid.CreateVersion7();

        var result = _serializer.Deserialize<StockCreatedEvent>(CreatedEvent(stockId.ToString()).ToByteArray());

        Assert.True(result.Success);
        Assert.Equal(new StockCreatedEvent(stockId, "AAPL", true, new TimeOnly(9, 30), new TimeOnly(16, 0)),
            result.Message);
    }

    [Fact]
    public void Deserialize_NonGuidAggregateId_ReturnsFailure()
    {
        var result = _serializer.Deserialize<StockCreatedEvent>(CreatedEvent("not-a-guid").ToByteArray());

        Assert.False(result.Success);
    }

    [Fact]
    public void Deserialize_TradingTimeAt24Hours_ReturnsFailure()
    {
        var evt = CreatedEvent(Guid.CreateVersion7().ToString());
        evt.TradingCloseTime = Duration.FromTimeSpan(TimeSpan.FromHours(24));

        var result = _serializer.Deserialize<StockCreatedEvent>(evt.ToByteArray());

        Assert.False(result.Success);
    }

    [Fact]
    public void Deserialize_NullTradingTime_ReturnsFailure()
    {
        var evt = CreatedEvent(Guid.CreateVersion7().ToString());
        evt.TradingStartTime = null;

        var result = _serializer.Deserialize<StockCreatedEvent>(evt.ToByteArray());

        Assert.False(result.Success);
    }

    [Fact]
    public void Deserialize_GarbageBytes_ReturnsFailure()
    {
        var result = _serializer.Deserialize<StockCreatedEvent>([0xFF, 0xFF, 0xFF, 0xFF]);

        Assert.False(result.Success);
    }

    private static Proto.StockCreatedEvent CreatedEvent(string aggregateId) => new()
    {
        AggregateId = aggregateId,
        Name = "AAPL",
        IsOpenToTrade = true,
        Currency = "USD",
        TradingStartTime = Duration.FromTimeSpan(new TimeSpan(9, 30, 0)),
        TradingCloseTime = Duration.FromTimeSpan(new TimeSpan(16, 0, 0))
    };
}
