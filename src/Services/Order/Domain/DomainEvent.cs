namespace Order.Domain;

public abstract record DomainEvent
{
    public Guid Id { get; init; }
    public DateTime OccurredOn { get; init; }
    public string EventType { get; init; }

    protected DomainEvent()
    {
        Id = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        EventType = GetType().Name;
    }
}