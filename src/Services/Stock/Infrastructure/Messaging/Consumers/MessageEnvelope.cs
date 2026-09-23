namespace Stock.Infrastructure.Messaging.Consumers;

public record struct MessageEnvelope<T>(T Message, Guid? CorrelationId);