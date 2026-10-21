using Messaging.Abstractions;

namespace Stock.Infrastructure.Messaging;

public class StockExceptionToOutcome : IExceptionToOutcome
{
    public ConsumeOutcome Map(Exception exception)
    {
        return new ConsumeOutcome(
            RetryableErrors.IsRetryable(exception) ? MessageConsumeResult.Retry : MessageConsumeResult.DeadLetter,
            exception.Message);
    }
}
