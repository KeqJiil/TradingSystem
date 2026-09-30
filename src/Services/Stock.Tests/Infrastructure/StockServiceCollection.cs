using Xunit;

namespace Stock.Tests.Infrastructure;

[CollectionDefinition(Name)]
public class StockServiceCollection : ICollectionFixture<StockServiceHostFixture>
{
    public const string Name = "StockServiceHost";
}
