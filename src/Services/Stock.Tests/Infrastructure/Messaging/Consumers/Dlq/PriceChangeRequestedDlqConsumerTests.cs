using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stock.Application.Commands.ChangePrice;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Consumers.Dlq;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq;

public class PriceChangeRequestedDlqConsumerTests
{
    [Fact]
    public void SourceTopic_ShouldBePriceChangeRequested()
    {
        var sut = new PriceChangeRequestedDlqConsumer(
            Options.Create(new DeadLetterOptions()),
            NullLogger<DlqRetryableConsumer<PriceChangeRequestedEvent, ChangePriceCommand>>.Instance,
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IDlqEventToCommandMapper<PriceChangeRequestedEvent, ChangePriceCommand>>(),
            Mock.Of<IKafkaConsumerFactory>(),
            Mock.Of<IDeadLetterPublisher>());

        var sourceTopic = typeof(PriceChangeRequestedDlqConsumer)
            .GetProperty("SourceTopic",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(sut);

        Assert.Equal(TopicNames.PriceChangeRequested, sourceTopic);
    }
}