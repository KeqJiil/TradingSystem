namespace Stock.Presentation.Options;

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
    public const string Stock = "stock-topic";
    public const string Price = "price-topic";
}