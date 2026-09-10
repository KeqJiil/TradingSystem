using Stock.Application.Commands.ReplayReadModel;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class PriceChangedDlqEventToCommandMapper
    : IDlqEventToCommandMapper<ExternalEvents.PriceChangedEvent, ReplayReadModelCommand>
{
    public ReplayReadModelCommand? Map(ExternalEvents.PriceChangedEvent message)
    {
        if (!message.Version.HasValue) return null;
        return new ReplayReadModelCommand(message.AggregateId, message.Version.Value);
    }
}