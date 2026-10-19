namespace Stock.Infrastructure.Topics;

public enum StockTopic
{
    StockCreated,
    StockStatusToggled,
    Price,
    PriceChangeRequested,
    StockNameChanged,
    StockTradingTimeChanged
}

public static class StockTopicExtensions
{
    public static string Name(this StockTopic topic) => topic switch
    {
        StockTopic.StockCreated => "stock-created-topic",
        StockTopic.StockStatusToggled => "stock-status-toggled-topic",
        StockTopic.Price => "price-topic",
        StockTopic.PriceChangeRequested => "price-change-requested-topic",
        StockTopic.StockNameChanged => "stock-name-changed-topic",
        StockTopic.StockTradingTimeChanged => "stock-trading-time-changed-topic",
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, null)
    };
}
