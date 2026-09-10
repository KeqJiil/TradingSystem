using Stock.Application.Commands.ToggleStatusReadModel;
using Stock.Infrastructure.ExternalEvents;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class StockToggledStatusDlqEventToCommandMapper
    : IDlqEventToCommandMapper<StockToggledStatusEvent, ToggleStatusReadModelCommand>
{
    public ToggleStatusReadModelCommand Map(StockToggledStatusEvent message)
    {
        return new ToggleStatusReadModelCommand(message.AggregateId);
    }
}
