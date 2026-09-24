using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Stock.Application.Exceptions;
using Stock.Infrastructure.ExternalEvents;
using Stock.Infrastructure.Observability;
using Stock.Infrastructure.Options;
using Stock.Infrastructure.Serialization;

namespace Stock.Infrastructure.Messaging.Publishers;

public sealed class DeadLetterPublisher(
    IKafkaProducerFactory kafkaProducerFactory,
    IOptions<DeadLetterOptions> deadLetterOptions) : IDeadLetterPublisher, IDisposable
{
    private readonly IProducer<string, byte[]> _producer = kafkaProducerFactory.CreateRaw("dead-letter-producer");

    public Task PublishAsync<TValue>(string sourceTopic, TValue value, Exception exception, int attempt,
        CancellationToken ct)
        where TValue : IExternalEvent
    {
        var headers = CreateHeaders(sourceTopic, attempt);
        AddExceptionHeaders(headers, exception);

        return ProduceAsync(DeadLetterTopic(sourceTopic, IsRetryableException(exception)), value, headers, ct);
    }

    public Task PublishAsync<TValue>(string sourceTopic, TValue value, bool isRetryable, int attempt,
        CancellationToken ct)
        where TValue : IExternalEvent
    {
        return ProduceAsync(DeadLetterTopic(sourceTopic, isRetryable), value, CreateHeaders(sourceTopic, attempt), ct);
    }

    public Task PublishFatalAsync<TValue>(string sourceTopic, TValue value, Exception exception, int attempt,
        CancellationToken ct)
        where TValue : IExternalEvent
    {
        var headers = CreateHeaders(sourceTopic, attempt);
        AddExceptionHeaders(headers, exception);

        return ProduceAsync(DeadLetterTopic(sourceTopic, false), value, headers, ct);
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

        await _producer.ProduceTracedAsync(DeadLetterTopic(sourceTopic, false),
            new Message<string, byte[]>
            {
                Key = message.Key is null ? null! : Encoding.UTF8.GetString(message.Key),
                Value = message.Value,
                Headers = headers
            }, ct);
    }

    public async Task PublishUnknownAsync<TValue>(TValue value, CancellationToken ct)
    {
        var topic = deadLetterOptions.Value.UnknownTopic;
        var headers = new Headers
        {
            { "original-topic", Encoding.UTF8.GetBytes(topic) },
            { "timestamp", BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) }
        };

        await _producer.ProduceTracedAsync(topic,
            new Message<string, byte[]>
                { Key = null!, Value = JsonSerializer.SerializeToUtf8Bytes(value), Headers = headers },
            ct);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }

    private async Task ProduceAsync<TValue>(string topic, TValue value, Headers headers, CancellationToken ct)
        where TValue : IExternalEvent
    {
        var bytes = new ProtobufNetSerializer<TValue>()
            .Serialize(value, new SerializationContext(MessageComponentType.Value, topic, headers));

        await _producer.ProduceTracedAsync(topic,
            new Message<string, byte[]> { Key = value.AggregateId.ToString(), Value = bytes, Headers = headers }, ct);
    }

    private string DeadLetterTopic(string sourceTopic, bool isRetryable)
    {
        return sourceTopic + deadLetterOptions.Value.TopicSuffix + (isRetryable ? ".retry" : ".fatal");
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
        headers.Add("exception-message", Encoding.UTF8.GetBytes(exception.Message));
        headers.Add("exception-stacktrace", Encoding.UTF8.GetBytes(exception.StackTrace ?? string.Empty));
    }

    private static bool IsRetryableException(Exception ex)
    {
        return ex switch
        {
            SqlException
            {
                Number: -2 or 1205 or 4060 or 4221 or 40197 or 40501 or 40613
                or 49918 or 49919 or 49920 or 233 or 10053 or 10054 or 10060
                or 921 or 922 or 923 or 924 or 926
            } => true,
            ReadModelNotFoundException => true,
            KafkaException kex => !kex.Error.IsFatal && IsRetryableKafkaCode(kex.Error.Code),
            TimeoutException => true,
            SocketException => true,
            IOException { InnerException: SocketException } => true,
            OperationCanceledException ocex when !ocex.CancellationToken.IsCancellationRequested => true,
            _ => false
        };
    }

    private static bool IsRetryableKafkaCode(ErrorCode code)
    {
        return code is
            ErrorCode.RequestTimedOut
            or ErrorCode.NotEnoughReplicas
            or ErrorCode.NotEnoughReplicasAfterAppend
            or ErrorCode.NetworkException
            or ErrorCode.Local_TimedOut
            or ErrorCode.Local_Transport
            or ErrorCode.Local_AllBrokersDown
            or ErrorCode.BrokerNotAvailable
            or ErrorCode.GroupLoadInProgress
            or ErrorCode.GroupCoordinatorNotAvailable
            or ErrorCode.NotCoordinatorForGroup
            or ErrorCode.RebalanceInProgress;
    }
}