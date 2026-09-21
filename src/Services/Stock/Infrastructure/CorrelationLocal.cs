namespace Stock.Infrastructure;

public static class CorrelationContext
{
    private static readonly AsyncLocal<Guid?> _correlationId = new();

    public static Guid? CorrelationId
    {
        get => _correlationId.Value;
        set => _correlationId.Value = value;
    }
}