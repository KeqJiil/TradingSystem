namespace Order.Infrastructure.Topics;

public enum OrderTopic
{
    OrderEvents
}

public static class OrderTopicExtensions
{
    public static string Name(this OrderTopic topic) => topic switch
    {
        OrderTopic.OrderEvents => "order-events-topic",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null)
    };
}
