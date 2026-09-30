using Microsoft.Extensions.Options;
using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;
using Stock.Infrastructure.Messaging.Publishers;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq;

public class StockToggledStatusDlqConsumer(
    IOptions<DeadLetterOptions> dlqOptions,
    ILogger<DlqRetryableConsumer<StockToggledStatusEvent, ToggleStatusReadModelCommand>> logger,
    IServiceScopeFactory serviceScopeFactory,
    IDlqEventToCommandMapper<StockToggledStatusEvent, ToggleStatusReadModelCommand> mapper,
    IKafkaConsumerFactory consumerFactory,
    IDeadLetterPublisher dlq)
    : DlqRetryableConsumer<StockToggledStatusEvent, ToggleStatusReadModelCommand>(dlqOptions, logger,
        serviceScopeFactory, mapper, consumerFactory, dlq)
{
    protected override string SourceTopic => TopicNames.StockStatusToggled;
}
