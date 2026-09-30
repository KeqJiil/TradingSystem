using System.Net.Sockets;
using Confluent.Kafka;
using Microsoft.Data.SqlClient;
using Stock.Application.Exceptions;
using Stock.Infrastructure.Messaging.Publishers;
using Xunit;

namespace Stock.Tests.Infrastructure.Messaging.Publishers;

public class RetryableErrorsTests : IClassFixture<MssqlFixture>
{
    private readonly MssqlFixture _fixture;

    public RetryableErrorsTests(MssqlFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<Exception, bool> NonSqlErrors => new()
    {
        { new TimeoutException(), true },
        { new SocketException(), true },
        { new IOException("connection reset", new SocketException()), true },
        { new IOException("disk"), false },
        { new ReadModelNotFoundException(Guid.NewGuid()), true },
        { new OperationCanceledException(CancellationToken.None), true },
        { new OperationCanceledException(new CancellationToken(true)), false },
        { new KafkaException(ErrorCode.RequestTimedOut), true },
        { new KafkaException(ErrorCode.Local_AllBrokersDown), true },
        { new KafkaException(ErrorCode.RebalanceInProgress), true },
        { new KafkaException(new Error(ErrorCode.RequestTimedOut, "fatal", true)), false },
        { new KafkaException(ErrorCode.TopicAuthorizationFailed), false },
        { new KafkaException(ErrorCode.MsgSizeTooLarge), false },
        { new InvalidOperationException(), false },
        { new FormatException(), false }
    };

    [Theory]
    [MemberData(nameof(NonSqlErrors))]
    public void IsRetryable_ClassifiesNonSqlErrors(Exception exception, bool retryable)
    {
        Assert.Equal(retryable, RetryableErrors.IsRetryable(exception));
    }

    [Fact]
    public async Task IsRetryable_CommandTimeout_IsRetryable()
    {
        var exception = await CaptureAsync(async connection =>
        {
            await using var command = new SqlCommand("WAITFOR DELAY '00:00:05'", connection) { CommandTimeout = 1 };
            await command.ExecuteNonQueryAsync();
        });

        Assert.Equal(-2, exception.Number);
        Assert.True(RetryableErrors.IsRetryable(exception));
    }

    [Fact]
    public async Task IsRetryable_CannotOpenDatabase_IsRetryable()
    {
        var connectionString = new SqlConnectionStringBuilder(_fixture.ConnectionString)
        {
            InitialCatalog = $"missing_{Guid.NewGuid():N}",
            Pooling = false
        }.ConnectionString;
        await using var connection = new SqlConnection(connectionString);

        var exception = await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync());

        Assert.Equal(4060, exception.Number);
        Assert.True(RetryableErrors.IsRetryable(exception));
    }

    [Theory]
    [InlineData("INSERT INTO stock_data (id, name, is_open_to_trade, currency) VALUES ('00000000-0000-0000-0000-000000000001', 'A', 1, 'USD'); INSERT INTO stock_data (id, name, is_open_to_trade, currency) VALUES ('00000000-0000-0000-0000-000000000001', 'A', 1, 'USD')", 2627)]
    [InlineData("SELECT * FROM table_that_does_not_exist", 208)]
    [InlineData("THROW 51000, 'business rule', 1", 51000)]
    public async Task IsRetryable_DataAndLogicErrors_AreNotRetryable(string sql, int number)
    {
        var exception = await CaptureAsync(async connection =>
        {
            await using var command = new SqlCommand($"BEGIN TRAN; {sql}; ROLLBACK", connection);
            await command.ExecuteNonQueryAsync();
        });

        Assert.Equal(number, exception.Number);
        Assert.False(RetryableErrors.IsRetryable(exception));
    }

    private async Task<SqlException> CaptureAsync(Func<SqlConnection, Task> action)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        return await Assert.ThrowsAsync<SqlException>(() => action(connection));
    }
}
