using Messaging.Abstractions;

namespace Messaging;

internal class BasicExceptionToOutcome : IExceptionToOutcome
{
    public ConsumeOutcome Map(Exception exception)
    {
        return new ConsumeOutcome(MessageConsumeResult.Retry, exception.Message);
    }
}