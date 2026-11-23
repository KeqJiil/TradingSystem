using Order.Domain.Aggregates;

namespace Order.Tests.Domain.Aggregates;

public static class TestStocks
{
    public static readonly TimeOnly Open = new(9, 30);
    public static readonly TimeOnly Close = new(16, 0);

    public static readonly Stock AlwaysOpenStock = AlwaysOpen();

    public static Stock Create(Guid? id = null) =>
        Stock.Create(id ?? Guid.CreateVersion7(), "AAPL", true, Open, Close).Value;

    public static Stock AlwaysOpen(Guid? id = null) =>
        Stock.Create(id ?? Guid.CreateVersion7(), "AAPL", true, TimeOnly.MinValue, TimeOnly.MaxValue).Value;

    public static Stock Closed(Guid? id = null) =>
        Stock.Create(id ?? Guid.CreateVersion7(), "AAPL", false, TimeOnly.MinValue, TimeOnly.MaxValue).Value;

    public static Stock WithHours(TimeOnly start, TimeOnly end, Guid? id = null) =>
        Stock.Create(id ?? Guid.CreateVersion7(), "AAPL", true, start, end).Value;
}
