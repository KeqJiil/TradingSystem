namespace Messaging;

internal static class RetryBackoff
{
    public static TimeSpan For(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        var milliseconds = baseDelay.TotalMilliseconds * Math.Pow(2, Math.Max(attempt - 1, 0));
        return TimeSpan.FromMilliseconds(Math.Min(milliseconds, maxDelay.TotalMilliseconds));
    }
}
