using System.ComponentModel.DataAnnotations;

namespace Order.Infrastructure.Options;

public class KafkaOptions
{
    public const string Name = "KafkaOptions";

    [Required(AllowEmptyStrings = false)]
    public string BootstrapServers { get; set; } = null!;

    [Required(AllowEmptyStrings = false)]
    public string ProducerClientId { get; set; } = null!;
}
