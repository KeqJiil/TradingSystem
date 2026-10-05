namespace Messaging.Tests.TestSupport.Delivery;

public sealed class ExceptionTypeMapper : IExceptionToOutcome
{
    public ConsumeOutcome Map(Exception exception)
    {
        return exception is ArgumentException
            ? new ConsumeOutcome(MessageConsumeResult.DeadLetter, "invalid")
            : new ConsumeOutcome(MessageConsumeResult.Retry, "transient");
    }
}
