using System.Runtime.InteropServices;
using EventStore.Client;
using Eventuous.EventStore;
using Eventuous.TestHelpers;
using MongoDb.Bson.NodaTime;
using MongoDB.Driver;
using Testcontainers.EventStoreDb;
using Testcontainers.MongoDb;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Projections.MongoDB.Fixtures;

public sealed class IntegrationFixture : IAsyncInitializer, IAsyncDisposable {
    public IEventStore      EventStore { get; set; }         = null!;
    public EventStoreClient Client     { get; private set; } = null!;
    public IMongoDatabase   Mongo      { get; private set; } = null!;

    static IEventSerializer Serializer { get; } = new DefaultEventSerializer(TestPrimitives.DefaultOptions);

    public Task<AppendEventsResult> AppendEvent(StreamName streamName, object evt, ExpectedStreamVersion? version = null)
        => EventStore.AppendEvents(
            streamName,
            version ?? ExpectedStreamVersion.Any,
            [new(Guid.NewGuid(), evt, new())],
            CancellationToken.None
        );

    static IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        NodaTimeSerializers.Register();
    }

    EventStoreDbContainer _esdbContainer  = null!;
    MongoDbContainer      _mongoContainer = null!;

    public async Task InitializeAsync() {
        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? "eventstore/eventstore:24.6.0-alpha-arm64v8"
            : "eventstore/eventstore:24.6";
        _esdbContainer = new EventStoreDbBuilder().WithImage(image).Build();
        await _esdbContainer.StartAsync();
        var settings = EventStoreClientSettings.Create(_esdbContainer.GetConnectionString());
        Client          = new(settings);
        EventStore      = new EsdbEventStore(Client);
        _mongoContainer = new MongoDbBuilder().WithImage("mongo:8").Build();
        await _mongoContainer.StartAsync();
        var mongoSettings = MongoClientSettings.FromConnectionString(_mongoContainer.GetConnectionString());
        Mongo = new MongoClient(mongoSettings).GetDatabase("bookings");
    }

    public async ValueTask DisposeAsync() {
        await Client.DisposeAsync();
        await _esdbContainer.DisposeAsync();
    }
}
