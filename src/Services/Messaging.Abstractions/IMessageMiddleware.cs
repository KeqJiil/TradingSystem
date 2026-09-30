namespace Messaging.Abstractions;

public interface IMessageMiddleware
{
    Task OnPublish(IMessageContext context, Func<Task> next);
    Task OnDelivery(IMessageContext context, Func<Task> next);
}