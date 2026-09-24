using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using OpenTelemetry.Context.Propagation;

namespace Stock.Infrastructure.Observability;

public static class KafkaTelemetry
{
    private const string CorrelationHeader = "x-correlation-id";

    private static readonly TextMapPropagator Propagator = new TraceContextPropagator();

    public static async Task<DeliveryResult<TKey, TValue>> ProduceTracedAsync<TKey, TValue>(
        this IProducer<TKey, TValue> producer, string topic, Message<TKey, TValue> message, CancellationToken ct)
    {
        message.Headers ??= new Headers();

        using var activity = StockTelemetry.Source.StartActivity($"send {topic}", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", topic);
        activity?.SetTag("messaging.kafka.message.key", message.Key?.ToString());

        Inject(message.Headers);

        try
        {
            var result = await producer.ProduceAsync(topic, message, ct);
            activity?.SetTag("messaging.destination.partition.id", result.Partition.Value.ToString());
            activity?.SetTag("messaging.kafka.offset", result.Offset.Value);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    public static Activity? StartProcess<TKey, TValue>(ConsumeResult<TKey, TValue> result, string groupId)
    {
        var activity = StockTelemetry.Source.StartActivity($"process {result.Topic}", ActivityKind.Consumer,
            ExtractParent(result.Message.Headers));
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", result.Topic);
        activity?.SetTag("messaging.consumer.group.name", groupId);
        activity?.SetTag("messaging.destination.partition.id", result.Partition.Value.ToString());
        activity?.SetTag("messaging.kafka.offset", result.Offset.Value);
        activity?.SetTag("correlation.id", CorrelationContext.CorrelationId?.ToString());
        return activity;
    }

    public static Guid? ExtractCorrelationId(Headers? headers)
    {
        return headers is not null
               && headers.TryGetLastBytes(CorrelationHeader, out var bytes)
               && Guid.TryParse(Encoding.UTF8.GetString(bytes), out var id)
            ? id
            : null;
    }

    private static void Inject(Headers headers)
    {
        if (Activity.Current is { } current)
            Propagator.Inject(new PropagationContext(current.Context, default), headers, SetHeader);

        if (CorrelationContext.CorrelationId is { } id)
            SetHeader(headers, CorrelationHeader, id.ToString());
    }

    private static ActivityContext ExtractParent(Headers? headers)
    {
        return headers is not null ? Propagator.Extract(default, headers, GetHeader).ActivityContext : default;
    }

    private static void SetHeader(Headers headers, string key, string value)
    {
        headers.Remove(key);
        headers.Add(key, Encoding.UTF8.GetBytes(value));
    }

    private static IEnumerable<string> GetHeader(Headers headers, string key)
    {
        return headers.TryGetLastBytes(key, out var bytes) ? [Encoding.UTF8.GetString(bytes)] : [];
    }
}
