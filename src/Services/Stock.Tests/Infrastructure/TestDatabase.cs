using Dapper;

namespace Stock.Tests.Infrastructure;

public static class TestDatabase
{
    private const string ResetSql = """
                                    DELETE FROM events_store;
                                    DELETE FROM stock_data;
                                    DELETE FROM stock_data_projection;
                                    DELETE FROM daily_stock_data_projection;
                                    DELETE FROM hourly_stock_data_projection;
                                    DELETE FROM outbox;
                                    """;

    public static Task ResetAsync(TestDbContext dbContext)
    {
        return dbContext.Connection.ExecuteAsync(ResetSql);
    }

    public static async Task WithTableOfflineAsync(TestDbContext dbContext, string table, Func<Task> action)
    {
        await dbContext.Connection.ExecuteAsync($"EXEC sp_rename '{table}', '{table}_offline'");
        try
        {
            await action();
        }
        finally
        {
            await dbContext.Connection.ExecuteAsync($"EXEC sp_rename '{table}_offline', '{table}'");
        }
    }
}
