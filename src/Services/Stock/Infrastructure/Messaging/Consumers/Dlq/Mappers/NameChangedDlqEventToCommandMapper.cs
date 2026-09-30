using Stock.Application.Commands.UpdateNameReadModel;
using Stock.Infrastructure.ExternalEvents;

namespace Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public class NameChangedDlqEventToCommandMapper
    : IDlqEventToCommandMapper<NameChangedEvent, UpdateNameReadModelCommand>
{
    public UpdateNameReadModelCommand Map(NameChangedEvent message)
    {
        return new UpdateNameReadModelCommand(message.AggregateId, message.Name, message.Version);
    }
}
