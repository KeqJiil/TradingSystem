using System.Globalization;
using System.Text;
using Confluent.Kafka;
using Messaging.Abstractions;

namespace Messaging.Dlq;

internal static class DlqReasons
{
    public const string DeadLetter = "dead-letter";
    public const string UnknownType = "unknown-type";
    public const string Exhausted = "exhausted";
}

internal static class DlqMessageBuilder
{
    private const int MaxDetailsLength = 1_000;

    public static (string Topic, Message<string, byte[]> Message) Build(RawMessage raw, string reason, string? details,
        int attempt, DateTimeOffset now)
    {
        var headers = new Dictionary<string, string>(raw.Headers);
        headers.TryAdd(MessagingHeaders.OriginalTopic, raw.Topic);
        headers.TryAdd(MessagingHeaders.FirstFailureAt, now.ToString("O", CultureInfo.InvariantCulture));
        headers[MessagingHeaders.DlqReason] = reason;
        headers[MessagingHeaders.Attempt] = attempt.ToString(CultureInfo.InvariantCulture);

        if (!string.IsNullOrEmpty(details))
            headers[MessagingHeaders.ExceptionMessage] = Truncate(details, MaxDetailsLength);

        var kafkaHeaders = new Headers();
        foreach (var (key, value) in headers)
            kafkaHeaders.Add(key, Encoding.UTF8.GetBytes(value));

        var message = new Message<string, byte[]>
        {
            Key = raw.Key!,
            Value = raw.Payload,
            Headers = kafkaHeaders
        };

        return (TopicNames.Dlq(raw.ConsumerGroup, headers[MessagingHeaders.OriginalTopic]), message);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
