using System.Net.Sockets;
using Confluent.Kafka;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Stock.Application.Abstractions;
using Stock.Application.Events;
using Stock.Infrastructure.Options;

namespace Stock.Infrastructure.Messaging.Publishers;

public class DeadLetterPublisher(
    IKafkaProducerFactory kafkaProducerFactory,
    IOptions<DeadLetterOptions> deadLetterOptions) : IDeadLetterPublisher
{
    public Task PublishAsync<TValue>(string sourceTopic, TValue value, Exception exception, int attempt, CancellationToken ct)
        where TValue : BasicEvent
    {
        var topic = sourceTopic + deadLetterOptions.Value.TopicSuffix + (IsRetryableException(exception) ? ".retry" : ".fatal");
        var producer = kafkaProducerFactory.Create<TValue>(topic);

        var headers = new Headers
        {
            { "exception-message", System.Text.Encoding.UTF8.GetBytes(exception.Message) },
            { "exception-stacktrace", System.Text.Encoding.UTF8.GetBytes(exception.StackTrace ?? string.Empty) },
            { "original-topic", System.Text.Encoding.UTF8.GetBytes(sourceTopic) },
            { "attempt-count", BitConverter.GetBytes(attempt) },
            { "timestamp", BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) }
        };

        return producer.ProduceAsync(topic,
            new Message<string, TValue> { Value = value, Key = value.AggregateId.ToString(), Headers = headers }, ct);
    }

    public Task PublishAsync<TValue>(string sourceTopic, TValue value, bool isRetryable, int attempt, CancellationToken ct)
        where TValue : BasicEvent
    {
        var topic = sourceTopic + deadLetterOptions.Value.TopicSuffix + (isRetryable ? ".retry" : ".fatal");
        var producer = kafkaProducerFactory.Create<TValue>(topic);

        var headers = new Headers
        {
            { "original-topic", System.Text.Encoding.UTF8.GetBytes(sourceTopic) },
            { "attempt-count", BitConverter.GetBytes(attempt) },
            { "timestamp", BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) }
        };

        return producer.ProduceAsync(topic,
            new Message<string, TValue> { Value = value, Key = value.AggregateId.ToString(), Headers = headers }, ct);
    }

    public Task PublishUnknownAsync<TValue>(TValue value, CancellationToken ct)
    {
        var producer = kafkaProducerFactory.Create<TValue>(deadLetterOptions.Value.UnknownTopic);

        var headers = new Headers
        {
            { "original-topic", System.Text.Encoding.UTF8.GetBytes(deadLetterOptions.Value.UnknownTopic) },
            { "timestamp", BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) }
        };

        return producer.ProduceAsync(deadLetterOptions.Value.UnknownTopic,
            new Message<string, TValue> { Value = value, Headers = headers }, ct);
    }

    private bool IsRetryableException(Exception ex)
    {
        return ex switch
        {
            SqlException
            {
                Number: -2 or 1205 or 4060 or 4221 or 40197 or 40501 or 40613
                or 49918 or 49919 or 49920 or 233 or 10053 or 10054 or 10060
                or 921 or 922 or 923 or 924 or 926
            } => true,
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