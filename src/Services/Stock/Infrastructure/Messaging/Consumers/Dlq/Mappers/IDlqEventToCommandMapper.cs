namespace Stock.Infrastructure.Messaging.Consumers.Dlq.Mappers;

public interface IDlqEventToCommandMapper<TMessage, TCommand>
{
    public TCommand Map(TMessage message);
}