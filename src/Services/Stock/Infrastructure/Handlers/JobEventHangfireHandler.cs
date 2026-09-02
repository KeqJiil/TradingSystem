using Hangfire;
using MediatR;
using Stock.Application.Events;

namespace Stock.Infrastructure.Handlers;

public class JobEventHangfireHandler<TEvent>(IBackgroundJobClient jobQueue)
    : INotificationHandler<TEvent> where TEvent : JobEvent
{
    public Task Handle(TEvent @event, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}