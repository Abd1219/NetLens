using NetLens.Domain.Events;

namespace NetLens.Application.Abstractions;

/// <summary>
/// Defines the interface for a pub/sub event bus handling network and domain telemetry events.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event asynchronously to all registered subscribers.
    /// </summary>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IDomainEvent;

    /// <summary>
    /// Subscribes an event handler to a specific event type.
    /// </summary>
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) 
        where TEvent : IDomainEvent;

    /// <summary>
    /// Unsubscribes an event handler from a specific event type.
    /// </summary>
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) 
        where TEvent : IDomainEvent;
}

/// <summary>
/// Defines a handler for events of type <typeparamref name="TEvent"/>.
/// </summary>
public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the event asynchronously.
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
