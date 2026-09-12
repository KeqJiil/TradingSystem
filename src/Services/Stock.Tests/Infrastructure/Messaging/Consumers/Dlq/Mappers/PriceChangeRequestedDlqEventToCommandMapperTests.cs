using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class PriceChangeRequestedDlqEventToCommandMapperTests
{
    [Fact]
    public void Map_ReturnsCommand()
    {
        var data = new PriceChangeRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), 100m, DateTimeOffset.UtcNow);
        var mapper = new PriceChangeRequestedDlqEventToCommandMapper();

        var result = mapper.Map(data);

        Assert.NotNull(result);
        Assert.Equal(data.AggregateId, result.AggregateId);
        Assert.Equal(data.EventId, result.EventId);
        Assert.Equal(data.PriceChange, result.PriceChange);
        Assert.Equal(data.OccurredAt, result.OccuredAt);
    }
}