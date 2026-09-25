using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Observability;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;

namespace Stock.Infrastructure.Messaging.Publishers;

public sealed class DeadLetterPublisher(
    IKafkaPublisher publisher,
    IOptions<DeadLetterOptions> deadLetterOptions,
    ILogger<DeadLetterPublisher> logger) : IDeadLetterPublisher
{
    private const int MaxExceptionMessageLength = 1_000;
    private const int MaxStackTraceLength = 4_000;

    public Task PublishAsync<TValue>(string sourceTopic, TValue value, Exception exception, int attempt,
        CancellationToken ct)
        where TValue : IExternalEvent
    {
        return PublishAsync(sourceTopic, value, RetryableErrors.IsRetryable(exception), attempt, exception, ct);
    }

    public Task PublishAsync<TValue>(string sourceTopic, TValue value, bool isRetryable, int attempt,
        Exception? exception, CancellationToken ct)
        where TValue : IExternalEvent
    {
        var headers = CreateHeaders(sourceTopic, attempt);
        if (exception is not null) AddExceptionHeaders(headers, exception);

        return ProduceAsync(sourceTopic, isRetryable, value, headers, ct);
    }

    public Task PublishFatalAsync<TValue>(string sourceTopic, TValue value, Exception exception, int attempt,
        CancellationToken ct)
        where TValue : IExternalEvent
    {
        return PublishAsync(sourceTopic, value, false, attempt, exception, ct);
    }

    public async Task PublishPoisonAsync(string sourceTopic, Message<byte[], byte[]> message, Exception exception,
        CancellationToken ct)
    {
        var headers = new Headers();
        foreach (var header in message.Headers ?? [])
            headers.Add(header.Key, header.GetValueBytes());

        headers.Add("original-topic", Encoding.UTF8.GetBytes(sourceTopic));
        headers.Add("timestamp", BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        AddExceptionHeaders(headers, exception);

        var topic = DeadLetterTopic(sourceTopic, false);

        await publisher.PublishAsync(topic,
            new Message<string, byte[]>
            {
                Key = message.Key is null ? null! : Encoding.UTF8.GetString(message.Key),
                Value = message.Value,
                Headers = headers
            }, ct);

        StockTelemetry.DlqPublished.Add(1, Tags(topic, "poison"));
        logger.LogWarning(exception, "Published an undeserializable message from {SourceTopic} to {DeadLetterTopic}",
            sourceTopic, topic);
    }

    public async Task PublishUnknownAsync<TValue>(TValue value, CancellationToken ct)
    {
        var topic = deadLetterOptions.Value.UnknownTopic;
        var headers = new Headers
        {
            { "original-topic", Encoding.UTF8.GetBytes(topic) },
            { "timestamp", BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) }
        };

        await publisher.PublishAsync(topic,
            new Message<string, byte[]>
                { Key = null!, Value = JsonSerializer.SerializeToUtf8Bytes(value), Headers = headers },
            ct);

        StockTelemetry.DlqPublished.Add(1, Tags(topic, "unknown"));
        logger.LogWarning("Published a message of type {ValueType} to {DeadLetterTopic}", typeof(TValue).Name, topic);
    }

    private async Task ProduceAsync<TValue>(string sourceTopic, bool isRetryable, TValue value, Headers headers,
        CancellationToken ct)
        where TValue : IExternalEvent
    {
        var topic = DeadLetterTopic(sourceTopic, isRetryable);
        var bytes = new ProtobufNetSerializer<TValue>()
            .Serialize(value, new SerializationContext(MessageComponentType.Value, topic, headers));

        await publisher.PublishAsync(topic,
            new Message<string, byte[]> { Key = value.AggregateId.ToString(), Value = bytes, Headers = headers }, ct);

        StockTelemetry.DlqPublished.Add(1, Tags(topic, isRetryable ? "retry" : "fatal"));
        logger.LogWarning("Published {MessageType} for aggregate {AggregateId} from {SourceTopic} to {DeadLetterTopic}",
            typeof(TValue).Name, value.AggregateId, sourceTopic, topic);
    }

    private string DeadLetterTopic(string sourceTopic, bool isRetryable)
    {
        return sourceTopic + deadLetterOptions.Value.TopicSuffix + (isRetryable ? ".retry" : ".fatal");
    }

    private static KeyValuePair<string, object?>[] Tags(string topic, string kind)
    {
        return [new("topic", topic), new("kind", kind)];
    }

    private static Headers CreateHeaders(string sourceTopic, int attempt)
    {
        return new Headers
        {
            { "original-topic", Encoding.UTF8.GetBytes(sourceTopic) },
            { "attempt-count", BitConverter.GetBytes(attempt) },
            { "timestamp", BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) }
        };
    }

    private static void AddExceptionHeaders(Headers headers, Exception exception)
    {
        headers.Add("exception-type", Encoding.UTF8.GetBytes(exception.GetType().FullName ?? exception.GetType().Name));
        headers.Add("exception-message", Encoding.UTF8.GetBytes(Truncate(exception.Message, MaxExceptionMessageLength)));
        headers.Add("exception-stacktrace",
            Encoding.UTF8.GetBytes(Truncate(exception.StackTrace ?? string.Empty, MaxStackTraceLength)));
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
