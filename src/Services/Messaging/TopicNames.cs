namespace Messaging;

internal static class TopicNames
{
    public static string Dlq(string group, string topic)
    {
        return $"{group}.{topic}.dlq";
    }

    public static string Retry(string group, string topic)
    {
        return $"{group}.{topic}.retry";
    }
}
