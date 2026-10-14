using Messaging.Outbox;

namespace Messaging.Dapper.Tests.UnitTests;

public class OutboxRelayOptionsTests
{
    public static TheoryData<string, Action<OutboxRelayOptions>> InvalidOptions => new()
    {
        { nameof(OutboxRelayOptions.PollInterval), o => o.PollInterval = TimeSpan.Zero },
        { nameof(OutboxRelayOptions.PollInterval), o => o.PollInterval = TimeSpan.FromSeconds(-1) },
        { nameof(OutboxRelayOptions.BatchSize), o => o.BatchSize = 0 },
        { nameof(OutboxRelayOptions.MaxParallelKeys), o => o.MaxParallelKeys = 0 },
        { nameof(OutboxRelayOptions.LeaseMinutes), o => o.LeaseMinutes = 0 },
        { nameof(OutboxRelayOptions.MaxAttempts), o => o.MaxAttempts = 0 }
    };

    [Fact]
    public void Validate_DefaultOptions_DoesNotThrow()
    {
        new OutboxRelayOptions().Validate();
    }

    [Fact]
    public void Validate_MinimalValidValues_DoesNotThrow()
    {
        new OutboxRelayOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(1),
            BatchSize = 1,
            MaxParallelKeys = 1,
            LeaseMinutes = 1,
            MaxAttempts = 1
        }.Validate();
    }

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Validate_InvalidValue_Throws(string property, Action<OutboxRelayOptions> configure)
    {
        var options = new OutboxRelayOptions();
        configure(options);

        var ex = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(property, ex.Message);
    }
}
