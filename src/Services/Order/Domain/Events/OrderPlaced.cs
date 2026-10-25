using Order.Domain.Aggregates;
using Order.Domain.VO;

namespace Order.Domain.Events;

public record OrderPlaced(
    Guid OrderId,
    Guid UserId,
    Symbols Symbol,
    OrderSide Side,
    Quantity Quantity,
    OrderTerms OrderTerms,
    DateTimeOffset CreatedAt) : DomainEvent(CreatedAt);