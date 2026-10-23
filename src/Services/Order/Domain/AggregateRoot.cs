namespace Order.Domain;

public abstract class AggregateRoot
{
    private List<DomainEvent> _domainEvents = new();
    
    public virtual Guid Id { get; protected init; }
    
    protected void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
    
    public IReadOnlyCollection<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    
    public void ClearDomainEvents() => _domainEvents.Clear();
}