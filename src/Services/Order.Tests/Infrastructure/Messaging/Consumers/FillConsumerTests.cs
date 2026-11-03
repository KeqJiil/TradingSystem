using Order.Tests.Infrastructure;
using Xunit;

namespace Order.Tests.Infrastructure.Messaging.Consumers;

public class FillConsumerTests(MssqlFixture fixture) : IClassFixture<MssqlFixture>, IAsyncDisposable
{
    private readonly OrderTestScope _scope = OrderTestScope.Create(fixture);

    public ValueTask DisposeAsync() => _scope.DisposeAsync();
}
