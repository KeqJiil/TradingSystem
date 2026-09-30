using MediatR;

namespace Stock.Application.Events;

public abstract record BasicEvent(Guid AggregateId);

public abstract record StockEvent(Guid AggregateId) : BasicEvent(AggregateId), INotification;

public abstract record JobEvent(Guid AggregateId) : BasicEvent(AggregateId), INotification;