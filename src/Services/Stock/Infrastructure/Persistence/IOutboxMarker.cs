namespace Stock.Infrastructure.Persistence;

public interface IOutboxMarker
{
    Task MarkCompletedAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}