namespace Matcher.Core.Events;

public enum Reason : byte
{
    None = 0,
    CapacityExceeded = 1,
    InvalidPrice = 2,
    InvalidQuantity = 3,
    TradingClosed = 4,
    DuplicateOrderId = 5,
    UserRequested = 6,
    IocRemainder = 7,
    NoLiquidity = 8,
    FokNotFilled = 9,
    SelfTrade = 10,
    NotFound = 11
}
