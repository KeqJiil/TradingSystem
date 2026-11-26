namespace Matcher.Core.Events;

public readonly record struct MatcherEvent(
    Guid OrderId,
    Guid CounterOrderId,
    long Price,
    int Quantity,
    ushort TradeIndex,
    EventKind EventKind,
    Reason Reason)
;
