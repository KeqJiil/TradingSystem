namespace Messaging.Abstractions;

public interface IExceptionToOutcome
{
    ConsumeOutcome Map(Exception exception);
}