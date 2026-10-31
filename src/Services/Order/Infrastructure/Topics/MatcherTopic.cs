namespace Order.Infrastructure.Topics;

public enum MatcherTopic
{
    OrderAccepted,
    OrderRejected,
    Fill,
    OrderExpired,
    OrderCanceled,
    CancelRejected
}

public static class MatcherTopicExtensions
{
    public static string Name(this MatcherTopic topic) => topic switch
    {
        MatcherTopic.OrderAccepted => "matcher-order-accepted-topic",
        MatcherTopic.OrderRejected => "matcher-order-rejected-topic",
        MatcherTopic.Fill => "matcher-fill-topic",
        MatcherTopic.OrderExpired => "matcher-order-expired-topic",
        MatcherTopic.OrderCanceled => "matcher-order-canceled-topic",
        MatcherTopic.CancelRejected => "matcher-cancel-rejected-topic",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null)
    };
}
