using System.Diagnostics;
using System.Diagnostics.Metrics;
using Messaging.Abstractions;

namespace Messaging;

internal static class MessagingMetrics
{
    private static readonly Histogram<double> ProcessDuration = MessagingTelemetry.Meter.CreateHistogram(
        "messaging.process.duration",
        unit: "s",
        description: "Duration of a single processing attempt of a consumed message.",
        advice: new InstrumentAdvice<double>
        {
            HistogramBucketBoundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
        });

    private static readonly Counter<long> ConsumerRetries = MessagingTelemetry.Meter.CreateCounter<long>(
        "messaging.consumer.retries",
        unit: "{retry}",
        description: "Number of times a failed message was scheduled to be read again.");

    private static readonly Counter<long> ConsumerDeadLetters = MessagingTelemetry.Meter.CreateCounter<long>(
        "messaging.consumer.dlq",
        unit: "{message}",
        description: "Number of messages moved to the DLQ.");

    private static readonly Counter<long> OutboxDispatched = MessagingTelemetry.Meter.CreateCounter<long>(
        "messaging.outbox.dispatched",
        unit: "{message}",
        description: "Number of outbox publish attempts by outcome.");

    public static void RecordDispatched(string topic, string outcome)
    {
        OutboxDispatched.Add(1, new TagList
        {
            { "messaging.destination.name", topic },
            { "messaging.outbox.outcome", outcome }
        });
    }

    public static void RecordProcessed(string topic, string group, string messageType, MessageConsumeResult outcome,
        TimeSpan elapsed)
    {
        ProcessDuration.Record(elapsed.TotalSeconds, new TagList
        {
            { "messaging.destination.name", topic },
            { "messaging.consumer.group.name", group },
            { "messaging.message.type", messageType },
            { "messaging.consume.outcome", outcome.ToString() }
        });
    }

    public static void RecordRetry(string topic, string group, string messageType)
    {
        ConsumerRetries.Add(1, new TagList
        {
            { "messaging.destination.name", topic },
            { "messaging.consumer.group.name", group },
            { "messaging.message.type", messageType }
        });
    }

    public static void RecordDeadLetter(string topic, string group, string reason)
    {
        ConsumerDeadLetters.Add(1, new TagList
        {
            { "messaging.destination.name", topic },
            { "messaging.consumer.group.name", group },
            { "messaging.dlq.reason", reason }
        });
    }
}
