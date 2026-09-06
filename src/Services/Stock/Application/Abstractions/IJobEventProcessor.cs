using Stock.Application.Events;

namespace Stock.Application.Abstractions;

public interface IJobEventProcessor<in TEvent> where TEvent : JobEvent
{
    Task ProcessAsync(TEvent @event, CancellationToken ct);
}
