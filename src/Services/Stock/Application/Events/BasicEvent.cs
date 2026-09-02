using MediatR;

namespace Stock.Application.Events;

public abstract record BasicEvent;

public abstract record StockEvent(Guid AggregateId) : BasicEvent, INotification;

public abstract record JobEvent(Guid AggregateId) : BasicEvent, INotification;