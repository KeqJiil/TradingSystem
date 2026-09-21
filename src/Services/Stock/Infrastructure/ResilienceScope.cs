namespace Stock.Infrastructure;

public static class ResilienceScope
{
    private static readonly AsyncLocal<bool> _isActive = new();

    public static bool IsActive
    {
        get => _isActive.Value;
        set => _isActive.Value = value;
    }
}
