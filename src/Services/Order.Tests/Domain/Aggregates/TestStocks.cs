using Order.Domain.Aggregates;

namespace Order.Tests.Domain.Aggregates;

public static class TestStocks
{
    public static readonly TimeOnly Open = new(9, 30);
    public static readonly TimeOnly Close = new(16, 0);

    public static Stock Create(Guid? id = null) =>
        Stock.Create(id ?? Guid.CreateVersion7(), "AAPL", true, Open, Close).Value;
}
