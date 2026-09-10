namespace Stock.Infrastructure.Options;

public class KafkaOptions
{
    public const string Name = "KafkaOptions";

    public string BootstrapServers { get; set; }
    public string ProducerClientId { get; set; }

    public string GroupId { get; set; }
    public string ConsumerClientId { get; set; }
}

public static class TopicNames
{
    public const string StockCreated = "stock-created-topic";
    public const string StockStatusToggled = "stock-status-toggled-topic";
    public const string Price = "price-topic";
    public const string PriceChangeRequested = "price-change-requested-topic";
}
