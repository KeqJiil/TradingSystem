namespace Stock.Infrastructure.Messaging.Workers;

public static class OutboxRetryPolicy
{
    public static bool ShouldRetry(int retries)
    {
        return retries < 3;
    }
}