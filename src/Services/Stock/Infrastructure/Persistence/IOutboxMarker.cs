namespace Stock.Infrastructure.Persistence;

public interface IOutboxMarker
{
    ValueTask MarkCompletedAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
}