using Microsoft.Extensions.Options;
using Stock.Application.Commands.ReplayReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq;


public class PriceChangedDlqConsumer(
    IOptions<DeadLetterOptions> dlqOptions,
    ILogger<DlqRetryableConsumer<PriceChangedEvent, ReplayReadModelCommand>> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDlqEventToCommandMapper<PriceChangedEvent, ReplayReadModelCommand> mapper,
    IKafkaConsumerFactory consumerFactory,
    IDeadLetterPublisher dlq)
    : DlqRetryableConsumer<PriceChangedEvent, ReplayReadModelCommand>(dlqOptions, logger, serviceScopeFactory, mapper,
        consumerFactory, dlq)
{
    protected override string SourceTopic => TopicNames.Price;
}
