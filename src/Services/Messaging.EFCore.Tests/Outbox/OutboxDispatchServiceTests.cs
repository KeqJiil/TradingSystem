using Messaging.EFCore.Outbox;
using Messaging.EFCore.Tests.Infrastructure;
using Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Messaging.EFCore.Tests.Outbox;

public class OutboxDispatchServiceTests(MssqlFixture db, KafkaFixture kafka)
    : OutboxDispatchServiceTestsBase(db, kafka), IClassFixture<MssqlFixture>, IClassFixture<KafkaFixture>
{
    protected override IOutboxTable CreateTable(string connectionString) => new OutboxTable(connectionString);

    protected override void AddPersistence(IServiceCollection services, string connectionString) =>
        services.AddDbContext<TestDbContext>(o => o.UseSqlServer(connectionString));

    protected override void AddOutbox(IMessagingBuilder builder, Action<OutboxRelayOptions> configure) =>
        builder.AddOutbox<TestDbContext>(configure);
}
