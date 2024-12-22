using Bogus;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Hypothesist;
using Hypothesist.Builders;

// ReSharper disable NotAccessedPositionalProperty.Global
// ReSharper disable MethodHasAsyncOverload

namespace Eventuous.Tests.Subscriptions.Base;

[EventType(TypeName)]
// ReSharper disable once ClassNeverInstantiated.Global
public record TestEvent(string Data, int Number) {
    public const string TypeName = "test-event";

    static readonly Faker<TestEvent> Faker = new Faker<TestEvent>().CustomInstantiator(f => new TestEvent(f.Lorem.Sentence(), f.Random.Int()));
    
    public static TestEvent Create() => Faker.Generate();
    
    public static List<TestEvent> CreateMany(int count) => Faker.Generate(count);
}

public class TestEventHandler(TestEventHandlerOptions? options) : BaseEventHandler {
    public TestEventHandler() : this(null) { }

    readonly TimeSpan _delay = options?.Delay ?? TimeSpan.Zero;

    public int Count { get; private set; }

    readonly Observer<object> _observer = new();

    public On<object> AssertThat() => Hypothesis.On(_observer);

    public Hypothesis<object> AssertCollection(TimeSpan deadline, List<object> collection)
        => Hypothesis.On(_observer).Timebox(deadline).Exactly(collection.Count).Match(collection.Contains);

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        await Task.Delay(_delay);
        await _observer.Add(context.Message!, context.CancellationToken);
        Count++;

        return EventHandlingStatus.Success;
    }

    public void Reset() => Count = 0;
}

public record TestEventHandlerOptions(TimeSpan? Delay = null);
