using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class StockToggledStatusDlqEventToCommandMapperTests
{
    [Fact]
    public void Map_ReturnsCommand()
    {
        var data = new StockToggledStatusEvent(Guid.NewGuid(), DateTimeOffset.Now);
        var mapper = new StockToggledStatusDlqEventToCommandMapper();

        var result = mapper.Map(data);

        Assert.NotNull(result);
        Assert.Equal(data.AggregateId, result.AggregateId);
    }
}