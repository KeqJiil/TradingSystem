using Messaging.Abstractions;

namespace Stock.Infrastructure;

public static class CorrelationContext
{
    public static Guid? CorrelationId
    {
        get => Guid.TryParse(MessagingCorrelation.CorrelationId, out var id) ? id : null;
        set => MessagingCorrelation.CorrelationId = value?.ToString();
    }
}
