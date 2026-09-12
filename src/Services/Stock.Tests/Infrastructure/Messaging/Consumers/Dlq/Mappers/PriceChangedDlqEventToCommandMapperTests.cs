using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class PriceChangedDlqEventToCommandMapperTests
{
    [Fact]
    public void Map_WithVersion_ReturnsCommand()
    {
        var aggregateId = Guid.NewGuid();
        var version = 5L;
        var priceChangedEvent = new PriceChangedEvent(aggregateId, 100m, version, DateTimeOffset.UtcNow);
        var mapper = new PriceChangedDlqEventToCommandMapper();

        var result = mapper.Map(priceChangedEvent);

        Assert.NotNull(result);
        Assert.Equal(aggregateId, result.AggregateId);
    }

    [Fact]
    public void Map_WithoutVersion_ReturnsNull()
    {
        var aggregateId = Guid.NewGuid();
        var priceChangedEvent = new PriceChangedEvent(aggregateId, 100m, null, DateTimeOffset.UtcNow);
        var mapper = new PriceChangedDlqEventToCommandMapper();

        var result = mapper.Map(priceChangedEvent);

        Assert.Null(result);
    }
}