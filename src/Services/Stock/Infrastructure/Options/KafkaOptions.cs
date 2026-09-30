using System.ComponentModel.DataAnnotations;

namespace Stock.Infrastructure.Options;

public class KafkaOptions
{
    public const string Name = "KafkaOptions";

    [Required(AllowEmptyStrings = false)]
    public string BootstrapServers { get; set; } = null!;

    [Required(AllowEmptyStrings = false)]
    public string ProducerClientId { get; set; } = null!;
}

public static class TopicNames
{
    public const string StockCreated = "stock-created-topic";
    public const string StockStatusToggled = "stock-status-toggled-topic";
    public const string Price = "price-topic";
    public const string PriceChangeRequested = "price-change-requested-topic";
    public const string StockNameChanged = "stock-name-changed-topic";
    public const string StockTradingTimeChanged = "stock-trading-time-changed-topic";

    public static string[] All =>
        [StockCreated, StockStatusToggled, Price, PriceChangeRequested, StockNameChanged, StockTradingTimeChanged];
}
