namespace Order.Infrastructure.Topics;

public enum OrderTopic
{
    OrderPlaced,
    OrderCancelRequested,
    OrderCanceled,
    OrderFilled,
    OrderExpired,
    OrderRejected
}

public static class OrderTopicExtensions
{
    public static string Name(this OrderTopic topic) => topic switch
    {
        OrderTopic.OrderPlaced => "order-placed-topic",
        OrderTopic.OrderCancelRequested => "order-cancel-requested-topic",
        OrderTopic.OrderCanceled => "order-canceled-topic",
        OrderTopic.OrderFilled => "order-filled-topic",
        OrderTopic.OrderExpired => "order-expired-topic",
        OrderTopic.OrderRejected => "order-rejected-topic",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null)
    };
}
