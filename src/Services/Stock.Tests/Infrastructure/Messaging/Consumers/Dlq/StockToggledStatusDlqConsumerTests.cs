using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Consumers.Dlq;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq;

public class StockToggledStatusDlqConsumerTests
{
    [Fact]
    public void SourceTopic_ShouldBeStockStatusToggled()
    {
        var sut = new StockToggledStatusDlqConsumer(
            Options.Create(new DeadLetterOptions()),
            NullLogger<DlqRetryableConsumer<StockToggledStatusEvent, ToggleStatusReadModelCommand>>.Instance,
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IDlqEventToCommandMapper<StockToggledStatusEvent, ToggleStatusReadModelCommand>>(),
            Mock.Of<IKafkaConsumerFactory>(),
            Mock.Of<IDeadLetterPublisher>());

        var sourceTopic = typeof(StockToggledStatusDlqConsumer)
            .GetProperty("SourceTopic",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(sut);

        Assert.Equal(TopicNames.StockStatusToggled, sourceTopic);
    }
}