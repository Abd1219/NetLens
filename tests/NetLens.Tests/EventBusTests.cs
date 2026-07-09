using FluentAssertions;
using NetLens.Domain.Events;
using NetLens.Application.Abstractions;
using NetLens.Application.Services;
using Xunit;

namespace NetLens.Tests;

public class EventBusTests
{
    private record TestDomainEvent(string Data) : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }

    private class TestEventHandler : IEventHandler<TestDomainEvent>
    {
        public List<TestDomainEvent> HandledEvents { get; } = [];
        private readonly TaskCompletionSource<bool> _tcs = new();

        public Task CompletionTask => _tcs.Task;

        public Task HandleAsync(TestDomainEvent @event, CancellationToken cancellationToken)
        {
            lock (HandledEvents)
            {
                HandledEvents.Add(@event);
                _tcs.TrySetResult(true);
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EventBus_ShouldPublishAndDispatchEventToSubscribers()
    {
        // Arrange
        using var bus = new EventBus();
        var handler = new TestEventHandler();
        bus.Subscribe(handler);

        var ev = new TestDomainEvent("NetLens Engine Telemetry sample");

        // Act
        await bus.PublishAsync(ev);
        
        // Wait for background worker to dispatch
        await Task.WhenAny(handler.CompletionTask, Task.Delay(2000));

        // Assert
        handler.HandledEvents.Should().ContainSingle();
        handler.HandledEvents[0].Data.Should().Be("NetLens Engine Telemetry sample");
    }
}
