namespace Messaging.EFCore.Tests.UnitTests;

public class CleanupOptionsTest
{
    public static TheoryData<string, Action<CleanupOptions>> InvalidOptions => new()
    {
        { nameof(CleanupOptions.OutboxBatchSize), o => o.OutboxBatchSize = 0 },
        { nameof(CleanupOptions.InboxBatchSize), o => o.InboxBatchSize = 0 },
        { nameof(CleanupOptions.InboxRetentionMinutes), o => o.InboxRetentionMinutes = 0 },
        { nameof(CleanupOptions.OutboxRetentionMinutes), o => o.OutboxRetentionMinutes = 0 },
        { nameof(CleanupOptions.CleanupIntervalMinutes), o => o.CleanupIntervalMinutes = 0 }
    };

    [Fact]
    public void ConfigureCleanup_DefaultOptions_DoesNotThrow()
    {
        new ServiceCollection().ConfigureCleanup(_ => { });
    }

    [Fact]
    public void ConfigureCleanup_MinimalValidValues_DoesNotThrow()
    {
        new ServiceCollection().ConfigureCleanup(o =>
        {
            o.OutboxBatchSize = 1;
            o.InboxBatchSize = 1;
            o.InboxRetentionMinutes = 1;
            o.OutboxRetentionMinutes = 1;
            o.CleanupIntervalMinutes = 1;
        });
    }

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void ConfigureCleanup_InvalidValue_Throws(string property, Action<CleanupOptions> configure)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().ConfigureCleanup(configure));

        Assert.Contains(property, ex.Message);
    }
}
