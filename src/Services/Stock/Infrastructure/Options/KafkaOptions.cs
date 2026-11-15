using System.ComponentModel.DataAnnotations;

namespace Stock.Infrastructure.Options;

public class KafkaOptions
{
    public const string Name = "KafkaOptions";

    [Required(AllowEmptyStrings = false)]
    public string BootstrapServers { get; set; } = null!;

    [Required(AllowEmptyStrings = false)]
    public string ProducerClientId { get; set; } = null!;

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    [Range(1, int.MaxValue)]
    public int ReadModelMaxAttempts { get; set; } = 10;

    public TimeSpan OutboxAgeMetricInterval { get; set; } = TimeSpan.FromSeconds(15);
}
