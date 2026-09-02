namespace Stock.Application.Abstractions;

public interface IStockDataReader
{
    public IAsyncEnumerable<Guid> GetAllIdsAsync(int limit, CancellationToken cancellationToken = default);
}