using Eventuous.SqlServer.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.MsSql;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.SqlServer.Subscriptions;

public class SubscribeToAll()
    : SubscribeToAllBase<MsSqlContainer, SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, SqlServerCheckpointStore>(
        new SubscriptionFixture<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, TestEventHandler>(_ => { }, false)
    ) {
    [Test]
    public async Task SqlServer_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEvents(cancellationToken);
    }

    [Test]
    public async Task SqlServer_ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEventsWhenRestarting(cancellationToken);
    }

    [Test]
    public async Task SqlServer_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        await ShouldUseExistingCheckpoint(cancellationToken);
    }
}

[ClassDataSource<StreamNameFixture>(Shared = SharedType.None)]
public class SubscribeToStream(StreamNameFixture streamNameFixture)
    : SubscribeToStreamBase<MsSqlContainer, SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions, SqlServerCheckpointStore>(
        streamNameFixture.StreamName,
        new SubscriptionFixture<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions, TestEventHandler>(
            opt => ConfigureOptions(opt, streamNameFixture),
            false
        )
    ) {
    [Test]
    public async Task SqlServer_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEvents(cancellationToken);
    }

    [Test]
    public async Task SqlServer_ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEventsWhenRestarting(cancellationToken);
    }

    [Test]
    public async Task SqlServer_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        await ShouldUseExistingCheckpoint(cancellationToken);
    }

    static void ConfigureOptions(SqlServerStreamSubscriptionOptions options, StreamNameFixture streamNameFixture) {
        options.Stream = streamNameFixture.StreamName;
    }
}

public class StreamNameFixture {
    public StreamName StreamName = new(Guid.NewGuid().ToString());
}
