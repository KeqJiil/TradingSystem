using Hangfire;
using MediatR;
using Stock.Application.Abstractions;
using Stock.Application.Events;

namespace Stock.Infrastructure.Handlers;

public class JobEventHangfireHandler<TEvent>(IBackgroundJobClient jobQueue)
    : INotificationHandler<TEvent> where TEvent : JobEvent
{
    public Task Handle(TEvent @event, CancellationToken ct)
    {
        jobQueue.Enqueue<IJobEventProcessor<TEvent>>(p => p.ProcessAsync(@event, CancellationToken.None));
        return Task.CompletedTask;
    }
}