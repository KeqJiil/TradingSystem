namespace Messaging.IntegrationTests.Infrastructure;

public static class Wait
{
    public static readonly TimeSpan Default = TimeSpan.FromSeconds(30);

    public static async Task UntilAsync(Func<bool> condition, string description, TimeSpan? timeout = null)
    {
        var limit = DateTime.UtcNow + (timeout ?? Default);

        while (!condition())
        {
            if (DateTime.UtcNow > limit)
                throw new TimeoutException($"Timed out waiting for: {description}");

            await Task.Delay(50);
        }
    }
}
