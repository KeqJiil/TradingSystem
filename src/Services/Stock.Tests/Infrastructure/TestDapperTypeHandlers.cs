using System.Runtime.CompilerServices;
using Stock.Infrastructure.Persistence;

namespace Stock.Tests.Infrastructure;

internal static class TestDapperTypeHandlers
{
    [ModuleInitializer]
    internal static void Register() => DapperTypeHandlers.Register();
}
