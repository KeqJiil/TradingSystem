using System.Data.Common;
using Messaging.Dapper.DbTasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;

namespace Messaging.Dapper.Transactions;

public static class RegisterUnitOfWork
{
    public static IServiceCollection AddUnitOfWork(this IServiceCollection services,
        Func<IServiceProvider, DbConnection> createConnection)
    {
        services.AddScoped(sp => new UnitOfWork(createConnection(sp), sp.GetServices<ITransactionHook>()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<ITransactionContext>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<IUnitOfWorkDecorator, UnitOfWorkDecorator>();
        services.TryAddSingleton(sp => SqlTransientRetry.CreateDefault(
            (sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance).CreateLogger(nameof(ResiliencePipeline))));

        return services;
    }
}
