namespace Messaging.IntegrationTests.Infrastructure;

public static class TestNames
{
    public static string Unique(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
    }
}
