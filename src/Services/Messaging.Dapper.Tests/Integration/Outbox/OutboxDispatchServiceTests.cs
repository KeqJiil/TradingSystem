using Messaging.Dapper.Outbox;
using Messaging.Dapper.Tests.Integration.Infrastructure;
using Messaging.Dapper.Transactions;
using Messaging.Outbox;
using Microsoft.Data.SqlClient;

namespace Messaging.Dapper.Tests.Integration.Outbox;

public class OutboxDispatchServiceTests(MssqlFixture db, KafkaFixture kafka)
    : OutboxDispatchServiceTestsBase(db, kafka), IClassFixture<MssqlFixture>, IClassFixture<KafkaFixture>
{
    protected override IOutboxTable CreateTable(string connectionString) => new OutboxTable(connectionString);

    protected override void AddPersistence(IServiceCollection services, string connectionString) =>
        services.AddUnitOfWork(_ => new SqlConnection(connectionString));

    protected override void AddOutbox(IMessagingBuilder builder, Action<OutboxRelayOptions> configure) =>
        builder.AddOutbox(configure);
}
