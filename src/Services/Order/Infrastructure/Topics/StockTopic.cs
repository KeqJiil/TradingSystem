namespace Order.Infrastructure.Topics;

public enum StockTopic
{
    StockCreated
}

public static class StockTopicExtensions
{
    public static string Name(this StockTopic topic) => topic switch
    {
        StockTopic.StockCreated => "stock-created-topic",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null)
    };
}
