using Microsoft.Extensions.Options;
using Stock.Application.Commands.ChangePrice;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq;

public class PriceChangeRequestedDlqConsumer(
    IOptions<DeadLetterOptions> dlqOptions,
    ILogger<DlqRetryableConsumer<PriceChangeRequestedEvent, ChangePriceCommand>> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDlqEventToCommandMapper<PriceChangeRequestedEvent, ChangePriceCommand> mapper,
    IKafkaConsumerFactory consumerFactory,
    IDeadLetterPublisher dlq)
    : DlqRetryableConsumer<PriceChangeRequestedEvent, ChangePriceCommand>(dlqOptions, logger, serviceScopeFactory, mapper,
        consumerFactory, dlq)
{
    protected override string SourceTopic => TopicNames.PriceChangeRequested;
}
