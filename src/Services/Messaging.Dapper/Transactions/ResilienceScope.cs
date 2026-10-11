namespace Messaging.Dapper.Transactions;

public static class ResilienceScope
{
    private static readonly AsyncLocal<bool> IsActiveValue = new();

    public static bool IsActive
    {
        get => IsActiveValue.Value;
        set => IsActiveValue.Value = value;
    }
}
