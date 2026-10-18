using Xunit;

namespace Stock.Tests.Infrastructure;

[CollectionDefinition(Name)]
public class HangfireCollection
{
    public const string Name = "Hangfire";
}
