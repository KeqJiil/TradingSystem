namespace Messaging.Tests;

public class RetryBackoffTests
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Max = TimeSpan.FromSeconds(30);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void For_AttemptBelowCap_DoublesDelayWithEachAttempt(int attempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetryBackoff.For(attempt, Base, Max));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(5_000)]
    public void For_DelayAboveCap_ReturnsCap(int attempt)
    {
        Assert.Equal(Max, RetryBackoff.For(attempt, Base, Max));
    }

    [Fact]
    public void For_ZeroBaseDelay_ReturnsZero()
    {
        Assert.Equal(TimeSpan.Zero, RetryBackoff.For(7, TimeSpan.Zero, Max));
    }
}
