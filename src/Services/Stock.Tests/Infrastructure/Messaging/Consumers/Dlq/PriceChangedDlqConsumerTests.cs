using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Stock.Application.Commands.ReplayReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging;
using Stock.Infrastructure.Messaging.Consumers.Dlq;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Consumers.Dlq;

public class PriceChangedDlqConsumerTests
{
    [Fact]
    public void SourceTopic_ShouldBePrice()
    {
        var sut = new PriceChangedDlqConsumer(
            Options.Create(new DeadLetterOptions()),
            NullLogger<DlqRetryableConsumer<PriceChangedEvent, ReplayReadModelCommand>>.Instance,
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IDlqEventToCommandMapper<PriceChangedEvent, ReplayReadModelCommand>>(),
            Mock.Of<IKafkaConsumerFactory>(),
            Mock.Of<IDeadLetterPublisher>());

        var sourceTopic = typeof(PriceChangedDlqConsumer)
            .GetProperty("SourceTopic",
                BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(sut);

        Assert.Equal(TopicNames.Price, sourceTopic);
    }
}