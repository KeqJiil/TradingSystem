using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Application.UseCases;

public class GetListByUserIdQueryHandlerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();
}
