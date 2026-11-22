namespace Order.Infrastructure.Topics;

public enum StockTopic
{
    StockCreated,
    StockNameChanged,
    StockStatusToggled,
    StockTradingTimeChanged
}

public static class StockTopicExtensions
{
    public static string Name(this StockTopic topic) => topic switch
    {
        StockTopic.StockCreated => "stock-created-topic",
        StockTopic.StockNameChanged => "stock-name-changed-topic",
        StockTopic.StockStatusToggled => "stock-status-toggled-topic",
        StockTopic.StockTradingTimeChanged => "stock-trading-time-changed-topic",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null)
    };
}
