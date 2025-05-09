// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Shouldly;

namespace Eventuous.Testing;

public static class CommandServiceFixture {
    /// <summary>
    /// Creates a new instance of the command service fixture
    /// </summary>
    /// <param name="serviceFactory">Function to create a service</param>
    /// <param name="store">Event store used by the service</param>
    /// <typeparam name="TState">State on which the service operates</typeparam>
    /// <returns></returns>
    public static IServiceFixtureGiven<TState> ForService<TState>(Func<ICommandService<TState>> serviceFactory, IEventStore store) where TState : State<TState>, new()
        => new CommandServiceFixture<TState>(serviceFactory, store);
}

public interface IServiceFixtureGiven<TState> where TState : State<TState>, new() {
    /// <summary>
    /// Specify pre-existing events in the stream
    /// </summary>
    /// <param name="streamName">Stream name for the existing entity</param>
    /// <param name="events">Events that should be in the existing stream</param>
    /// <returns></returns>
    IServiceFixtureWhen<TState> Given(StreamName streamName, params object[] events);

    /// <summary>
    /// Specify pre-existing events in the stream
    /// </summary>
    /// <param name="id">Entity id, the stream name will be generated by convention</param>
    /// <param name="events">Events that should be in the existing stream</param>
    /// <returns></returns>
    IServiceFixtureWhen<TState> Given(string id, params object[] events);
}

public interface IServiceFixtureWhen<TState> where TState : State<TState>, new() {
    /// <summary>
    /// Specify the command to execute
    /// </summary>
    /// <param name="command">Command to execute</param>
    /// <typeparam name="TCommand">Command type</typeparam>
    /// <returns></returns>
    IServiceFixtureTHen<TState> When<TCommand>(TCommand command) where TCommand : class;
}

public interface IServiceFixtureTHen<TState> where TState : State<TState>, new() {
    /// <summary>
    /// Execute the assertions
    /// </summary>
    /// <param name="assert">Assertions function</param>
    /// <returns></returns>
    Task Then(Action<CommandServiceFixture<TState>.FixtureResult> assert);
}

public class CommandServiceFixture<TState> : IServiceFixtureGiven<TState>, IServiceFixtureWhen<TState>, IServiceFixtureTHen<TState>
    where TState : State<TState>, new() {
    readonly IEventStore             _store;
    readonly ICommandService<TState> _service;

    StreamName            _streamName;
    object[]              _events = [];
    Task<Result<TState>>? _result;
    long                  _nextExpectedVersion = ExpectedStreamVersion.NoStream.Value;

    public CommandServiceFixture(Func<IEventStore, ITypeMapper, ICommandService<TState>> serviceFactory) {
        _store = new InMemoryEventStore();
        TypeMapper typeMap = new();
        _service = serviceFactory(_store, typeMap);
    }

    internal CommandServiceFixture(Func<ICommandService<TState>> serviceFactory, IEventStore store) {
        _store   = store;
        _service = serviceFactory();
    }

    public IServiceFixtureWhen<TState> Given(StreamName streamName, params object[] events) {
        _streamName = streamName;
        _events     = events;

        return this;
    }

    public IServiceFixtureWhen<TState> Given(string id, params object[] events) {
        _streamName = StreamName.ForState<TState>(id);
        _events     = events;

        return this;
    }

    public IServiceFixtureTHen<TState> When<TCommand>(TCommand command) where TCommand : class {
        _result = Execute(command);

        return this;
    }

    public async Task Then(Action<FixtureResult> assert) {
        if (_result == null) {
            throw new InvalidOperationException("You need to call When() before calling Then()");
        }

        if (_streamName == default) {
            throw new InvalidOperationException("You need to call Given() before calling Then()");
        }

        if (_result.IsFaulted) {
            throw _result.Exception!;
        }

        var result = _result.IsCompletedSuccessfully ? _result.Result : await _result;
        var stream = await _store.ReadStream(_streamName, StreamReadPosition.Start, false);

        assert(new(result, stream, _nextExpectedVersion + 1));
    }

    async Task<Result<TState>> Execute<TCommand>(TCommand command) where TCommand : class {
        if (_events.Length > 0) {
            var result = await _store.Store(_streamName, ExpectedStreamVersion.NoStream, _events);
            _nextExpectedVersion = result.NextExpectedVersion;
        }

        return await _service.Handle(command, default);
    }

    public class FixtureResult {
        readonly StreamEvent[] _streamEvents;
        readonly long          _version;

        public Result<TState> Result { get; }

        internal FixtureResult(Result<TState> result, StreamEvent[] streamEvents, long version) {
            _streamEvents = streamEvents;
            _version      = version;
            Result        = result;
        }

        /// <summary>
        /// Asserts if the result is Ok and executes the provided assertions
        /// </summary>
        /// <param name="assert">Assertion function for successful result</param>
        /// <returns></returns>
        /// <exception cref="ShouldAssertException">Thrown if the result is not ok</exception>
        [StackTraceHidden]
        public FixtureResult ResultIsOk(Action<Result<TState>.Ok>? assert = null) {
            if (Result.TryGet(out var ok)) {
                assert?.Invoke(ok);

                return this;
            }
            
            if (Result.TryGetError(out var error)) {
                throw new ShouldAssertException($"Expected the result to be Ok, but it was Error \"{error.ErrorMessage}\"", error.Exception);
            }

            throw new ShouldAssertException("Expected the result to be Ok, but it was not");
        }

        /// <summary>
        /// Asserts if the result is Error and executes the provided assertions
        /// </summary>
        /// <param name="assert">Assertion function for error result</param>
        /// <returns></returns>
        /// <exception cref="ShouldAssertException">Thrown if the result is not an error</exception>
        [StackTraceHidden]
        public FixtureResult ResultIsError(Action<Result<TState>.Error>? assert = null) {
            if (!Result.TryGetError(out var error)) {
                throw new ShouldAssertException("Expected the result to be Error, but it was Ok");
            }

            assert?.Invoke(error);

            return this;
        }

        /// <summary>
        /// Asserts if the result is Error and the exception is of type T and executes the provided assertions
        /// </summary>
        /// <param name="assert">Assertion function for error result</param>
        /// <typeparam name="T">Type of exception that is expected</typeparam>
        /// <returns></returns>
        /// <exception cref="ShouldAssertException">Thrown if the result is not an error or if the exception is not of type T</exception>
        [StackTraceHidden]
        public FixtureResult ResultIsError<T>(Action<T>? assert = null) where T : Exception {
            if (!Result.TryGetError(out var error)) {
                throw new ShouldAssertException("Expected the result to be Error, but it was Ok");
            }

            error.Exception.ShouldBeOfType<T>();

            assert?.Invoke((T)error.Exception);

            return this;
        }

        /// <summary>
        /// Asserts if the whole stream is identical to the provided events
        /// </summary>
        /// <param name="events">Events to be found in the stream</param>
        /// <returns></returns>
        [StackTraceHidden]
        public FixtureResult FullStreamEventsAre(params object[] events) {
            var stream = _streamEvents.Select(x => x.Payload);
            stream.ShouldBe(events);

            return this;
        }

        /// <summary>
        /// Asserts if newly appended events are identical to the provided events
        /// </summary>
        /// <param name="events">Events that are expected to be new</param>
        /// <returns></returns>
        [StackTraceHidden]
        public FixtureResult NewStreamEventsAre(params object[] events) {
            var stream = _streamEvents.Where(x => x.Position >= _version).Select(x => x.Payload);
            stream.ShouldBe(events);

            return this;
        }

        /// <summary>
        /// Asserts stream events
        /// </summary>
        /// <param name="assert">Assertion function to check StreamEvent collection</param>
        /// <returns></returns>
        [StackTraceHidden]
        public FixtureResult StreamIs(Action<StreamEvent[]> assert) {
            assert(_streamEvents);

            return this;
        }
    }
}
