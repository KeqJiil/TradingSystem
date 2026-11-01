using Messaging.Abstractions;
using Order.Domain;

namespace Order.Infrastructure.Messaging.Consumers;

public static class ResultToOutcome
{
    public static ConsumeOutcome ToConsumeOutcome(this Result result)
    {
        if (result.IsSuccess)
            return new ConsumeOutcome(MessageConsumeResult.Success);

        return result.Error.Type == ErrorType.Validation
            ? new ConsumeOutcome(MessageConsumeResult.DeadLetter, result.Error.Message)
            : new ConsumeOutcome(MessageConsumeResult.Retry, result.Error.Message);
    }
}
