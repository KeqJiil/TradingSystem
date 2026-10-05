namespace Messaging.Dlq;

internal class DlqKafkaProducer(BasicKafkaProducer producer) : IDeadLetterPublisher
{
    public async Task<bool> PublishAsync(RawMessage message, string reason, string? exceptionDetails, int attempt,
        CancellationToken ct)
    {
        var (topic, kafkaMessage) =
            DlqMessageBuilder.Build(message, reason, exceptionDetails, attempt, DateTimeOffset.UtcNow);

        var result = await producer.PublishAsync(topic, kafkaMessage, ct);

        return result.Success;
    }
}
