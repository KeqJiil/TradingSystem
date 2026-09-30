namespace Stock.Tests.Infrastructure;

public static class Polling
{
    public static async Task<T> WaitUntilAsync<T>(Func<Task<T?>> query, TimeSpan timeout) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await query();
            if (result is not null) return result;
            await Task.Delay(200);
        }

        throw new TimeoutException($"Condition not met within {timeout}.");
    }

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(200);
        }

        throw new TimeoutException($"Condition not met within {timeout}.");
    }

    public static async Task StaysTrueAsync(Func<Task<bool>> condition, TimeSpan duration)
    {
        var deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            if (!await condition())
                throw new InvalidOperationException("Condition became false before the observation window elapsed.");
            await Task.Delay(200);
        }
    }
}
