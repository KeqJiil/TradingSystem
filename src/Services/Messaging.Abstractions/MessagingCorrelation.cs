namespace Messaging.Abstractions;

public static class MessagingCorrelation
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string? CorrelationId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
