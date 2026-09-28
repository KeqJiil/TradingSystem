using System.Text.RegularExpressions;
using Dapper;
using DbUp;
using Microsoft.Data.SqlClient;
using Stock.Application.Events;
using Stock.Infrastructure.Persistence;
using Stock.Infrastructure.Persistence.Implementations;
using Xunit;

namespace Stock.Tests.Infrastructure.Persistence;

public class DbMigratorTests : IClassFixture<MssqlFixture>, IAsyncLifetime
{
    private const int DuplicateKeyConstraint = 2627;
    private const int DuplicateKeyIndex = 2601;
    private const int ObjectAlreadyExists = 2714;

    private readonly MssqlFixture _fixture;
    private string _connectionString = null!;
    private TestDbContext _dbContext = null!;

    public DbMigratorTests(MssqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _connectionString = await _fixture.CreateEmptyDatabaseAsync();
        _dbContext = new TestDbContext(_connectionString);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task ApplyMigrations_OnEmptyDatabase_CreatesFinalSchema()
    {
        DbMigrator.ApplyMigrations(_connectionString);

        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        foreach (var table in new[]
                 {
                     "events_store", "stock_data", "stock_data_projection", "daily_stock_data_projection",
                     "hourly_stock_data_projection", "outbox"
                 })
            Assert.True(await ObjectExists($"SELECT 1 FROM sys.tables WHERE name = '{table}'"), table);

        Assert.True(await ObjectExists("SELECT 1 FROM sys.types WHERE is_table_type = 1 AND name = 'OutboxEventTvp'"));
        foreach (var index in new[]
                 {
                     "idx_event_store_version", "idx_daily_stock_projection_aggregate_id_date",
                     "idx_hourly_stock_projection_aggregate_id_date_time"
                 })
            Assert.True(await ObjectExists($"SELECT 1 FROM sys.indexes WHERE name = '{index}' AND is_unique = 1"), index);

        Assert.True(await ColumnExists("stock_data", "metadata_version"));
        Assert.False(await ColumnExists("stock_data", "status_version"));
        Assert.True(await ColumnExists("events_store", "position"));
    }

    [Fact]
    public async Task ApplyMigrations_RunTwice_IsNoOp_AndJournalHasEachScriptOnce()
    {
        DbMigrator.ApplyMigrations(_connectionString);
        DbMigrator.ApplyMigrations(_connectionString);

        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        var journal = (await _dbContext.Connection.QueryAsync<string>("SELECT ScriptName FROM SchemaVersions"))
            .ToList();
        var scripts = typeof(DbMigrator).Assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql")).ToList();

        Assert.Equal(scripts.Count, journal.Count);
        Assert.Equal(scripts.Order(), journal.Order());
    }

    [Fact]
    public async Task ApplyMigrations_WhenScriptFails_ThrowsWithOriginalSqlError()
    {
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await _dbContext.Connection.ExecuteAsync("CREATE TABLE events_store (id INT)");

        var exception = Assert.Throws<InvalidOperationException>(() => DbMigrator.ApplyMigrations(_connectionString));

        var sqlException = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Equal(ObjectAlreadyExists, sqlException.Number);
    }

    [Fact]
    public async Task Migration011_OnExistingProjections_StartsNewVersionsAtZero_AndKeepsData()
    {
        ApplyMigrationsBefore(11);
        var aggregateId = Guid.NewGuid();
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await _dbContext.Connection.ExecuteAsync("""
            INSERT INTO stock_data_projection
                (aggregate_id, name, is_open_to_trade, price, version, trading_start_time, trading_end_time, currency, status_version)
            VALUES (@Id, 'OldName', 1, 42, 7, '09:00', '17:00', 'USD', 5)
            """, new { Id = aggregateId });

        DbMigrator.ApplyMigrations(_connectionString);

        var row = await _dbContext.Connection.QuerySingleAsync<(string Name, decimal Price, long Version,
            long StatusVersion, long NameVersion, long TradingTimeVersion)>("""
            SELECT name, price, version, status_version, name_version, trading_time_version
            FROM stock_data_projection WHERE aggregate_id = @Id
            """, new { Id = aggregateId });
        Assert.Equal(("OldName", 42m, 7L, 5L, 0L, 0L), row);

        await new StockReadModelWriter(_dbContext).SetNewNameAsync(aggregateId, "NewName", 1, CancellationToken.None);
        Assert.Equal("NewName", await _dbContext.Connection.ExecuteScalarAsync<string>(
            "SELECT name FROM stock_data_projection WHERE aggregate_id = @Id", new { Id = aggregateId }));
    }

    [Fact]
    public async Task Migration012_OnExistingEvents_KeepsRowsAndUniqueness_AndEventStoreStillWorks()
    {
        ApplyMigrationsBefore(12);
        var aggregateId = Guid.NewGuid();
        var existingEventId = Guid.NewGuid();
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await InsertEvent(existingEventId, aggregateId, 1);
        await InsertEvent(Guid.NewGuid(), aggregateId, 2);
        await InsertEvent(Guid.NewGuid(), Guid.NewGuid(), 1);

        DbMigrator.ApplyMigrations(_connectionString);

        var positions = (await _dbContext.Connection.QueryAsync<long>("SELECT position FROM events_store")).ToList();
        Assert.Equal(3, positions.Count);
        Assert.Equal(3, positions.Distinct().Count());

        var duplicateId = await Assert.ThrowsAsync<SqlException>(() => InsertEvent(existingEventId, aggregateId, 3));
        Assert.Equal(DuplicateKeyConstraint, duplicateId.Number);
        var duplicateVersion = await Assert.ThrowsAsync<SqlException>(() => InsertEvent(Guid.NewGuid(), aggregateId, 2));
        Assert.Equal(DuplicateKeyIndex, duplicateVersion.Number);

        var store = new StockEventStore(_dbContext);
        Assert.Null(await store.AppendAsync(
            new PriceChangeRequested(existingEventId, aggregateId, 1m, DateTimeOffset.UtcNow), CancellationToken.None));
        var appended = await store.AppendAsync(
            new PriceChangeRequested(Guid.NewGuid(), aggregateId, 1m, DateTimeOffset.UtcNow), CancellationToken.None);
        Assert.Equal(3, appended!.Version);
    }

    [Fact]
    public async Task Migration014_OnExistingStocks_RenamesStatusVersionToMetadataVersion_KeepingValues()
    {
        ApplyMigrationsBefore(14);
        var stockId = Guid.NewGuid();
        await _dbContext.EnsureConnectionOpenAsync(CancellationToken.None);
        await _dbContext.Connection.ExecuteAsync("""
            INSERT INTO stock_data (id, name, is_open_to_trade, trading_start_time, trading_end_time, currency, status_version)
            VALUES (@Id, 'Stock', 1, '09:00', '17:00', 'USD', 7)
            """, new { Id = stockId });

        DbMigrator.ApplyMigrations(_connectionString);

        Assert.False(await ColumnExists("stock_data", "status_version"));
        Assert.Equal(7L, await _dbContext.Connection.ExecuteScalarAsync<long>(
            "SELECT metadata_version FROM stock_data WHERE id = @Id", new { Id = stockId }));
        Assert.Equal(8L, await new StockDataWriter(_dbContext).SetOpenToTrade(stockId, false, CancellationToken.None));
    }

    private void ApplyMigrationsBefore(int scriptNumber)
    {
        var result = DeployChanges.To
            .SqlDatabase(_connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DbMigrator).Assembly, name => ScriptNumber(name) < scriptNumber)
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build()
            .PerformUpgrade();

        Assert.True(result.Successful, result.Error?.ToString());
    }

    private static int ScriptNumber(string resourceName)
    {
        return int.Parse(Regex.Match(resourceName, @"Migrations\.(\d{3})_").Groups[1].Value);
    }

    private Task InsertEvent(Guid eventId, Guid aggregateId, long version)
    {
        return _dbContext.Connection.ExecuteAsync("""
            INSERT INTO events_store (event_id, aggregate_id, version, event_type, payload, price_change)
            VALUES (@EventId, @AggregateId, @Version, 'PriceChangedEvent', '{}', 1)
            """, new { EventId = eventId, AggregateId = aggregateId, Version = version });
    }

    private async Task<bool> ObjectExists(string sql)
    {
        return await _dbContext.Connection.ExecuteScalarAsync<int?>(sql) == 1;
    }

    private Task<bool> ColumnExists(string table, string column)
    {
        return ObjectExists(
            $"SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.{table}') AND name = '{column}'");
    }
}
