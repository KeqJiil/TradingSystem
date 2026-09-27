namespace Stock.Infrastructure.Messaging.Consumers;

[Obsolete("Out of order for now together with VersionsBuffer")]
public record struct MessageEnvelope<T>(T Message, Guid? CorrelationId);