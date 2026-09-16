using Dapper;

namespace Stock.Tests.Infrastructure;

public static class TestDatabase
{
    private const string ResetSql = """
                                    DELETE FROM events_store;
                                    DELETE FROM stock_data;
                                    DELETE FROM stock_data_projection;
                                    DELETE FROM daily_stock_data_projection;
                                    DELETE FROM outbox;
                                    """;

    public static Task ResetAsync(TestDbContext dbContext)
    {
        return dbContext.Connection.ExecuteAsync(ResetSql);
    }
}
