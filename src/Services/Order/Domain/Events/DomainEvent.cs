namespace Order.Domain.Events;

public abstract record DomainEvent
{
    public Guid Id { get; init; }
    public DateTimeOffset OccurredOn { get; init; }
    public string EventType { get; init; }

    protected DomainEvent(DateTimeOffset occurredOn)
    {
        Id = Guid.CreateVersion7(occurredOn);
        OccurredOn = occurredOn;
        EventType = GetType().Name;
    }
}