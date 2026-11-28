using Matcher.Core.Enums;

namespace Matcher.Core.Commands;

public readonly record struct IncomingEvent(
    Guid OrderId,
    Guid UserId, // 32 bytes
    long Offset,
    long Price, // 16 bytes
    int StockIndex,
    int Quantity, // 8 bytes
    OrderType OrderType,
    OrderSide OrderSide,
    TimeInForce TimeInForce,
    IncomingEventKind IncomingEventKind); // 4 bytes + 4 bytes padding = 64 bytes.