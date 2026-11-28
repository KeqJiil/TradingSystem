using Matcher.Core.Enums;

namespace Matcher.Core.Books;

internal struct RestingOrder(Guid orderId, Guid userId, long price, int remaining, OrderSide side)
{
    public Guid OrderId { get; } = orderId;
    public Guid UserId { get; } = userId;
    public long Price { get; } = price;
    private int _remaining = remaining;
    public OrderSide Side { get; } = side;
    
    public int Remaining => _remaining;
    
    public Errors Reduce(int quantity)
    {
        if (quantity <= 0 || quantity > _remaining)
        {
            return Errors.InvalidOperation;
        }
        
        _remaining -= quantity;
        
        return Errors.None;
    }
}