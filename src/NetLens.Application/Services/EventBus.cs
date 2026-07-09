using System.Collections.Concurrent;
using System.Threading.Channels;
using NetLens.Domain.Events;
using NetLens.Application.Abstractions;

namespace NetLens.Application.Services;

/// <summary>
/// A high-performance, non-blocking Event Bus using System.Threading.Channels.
/// </summary>
public sealed class EventBus : IEventBus, IDisposable
{
    private readonly Channel<IDomainEvent> _channel;
    private readonly ConcurrentDictionary<Type, List<object>> _handlers = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;

    public EventBus()
    {
        // Setup an unbounded channel optimized for a single background reader to preserve event order.
        _channel = Channel.CreateUnbounded<IDomainEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false
        });

        _processingTask = Task.Run(ProcessEventsAsync);
    }

    public async ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IDomainEvent
    {
        await _channel.Writer.WriteAsync(@event, cancellationToken);
    }

    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) 
        where TEvent : IDomainEvent
    {
        var type = typeof(TEvent);
        _handlers.AddOrUpdate(type, 
            _ => [handler], 
            (_, list) => 
            {
                lock (list)
                {
                    if (!list.Contains(handler))
                    {
                        list.Add(handler);
                    }
                }
                return list;
            });
    }

    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) 
        where TEvent : IDomainEvent
    {
        var type = typeof(TEvent);
        if (_handlers.TryGetValue(type, out var list))
        {
            lock (list)
            {
                list.Remove(handler);
            }
        }
    }

    private async Task ProcessEventsAsync()
    {
        var reader = _channel.Reader;
        var token = _cts.Token;

        try
        {
            while (await reader.WaitToReadAsync(token))
            {
                while (reader.TryRead(out var @event))
                {
                    await DispatchEventAsync(@event, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposing
        }
    }

    private async Task DispatchEventAsync(IDomainEvent @event, CancellationToken cancellationToken)
    {
        var eventType = @event.GetType();
        
        // Find handlers registered for this exact type or base interfaces
        foreach (var registeredType in _handlers.Keys)
        {
            if (registeredType.IsAssignableFrom(eventType))
            {
                if (_handlers.TryGetValue(registeredType, out var list))
                {
                    object[] handlersToInvoke;
                    lock (list)
                    {
                        handlersToInvoke = [.. list];
                    }

                    foreach (var handler in handlersToInvoke)
                    {
                        try
                        {
                            // Invoke HandleAsync dynamically
                            var method = handler.GetType().GetMethod(nameof(IEventHandler<IDomainEvent>.HandleAsync));
                            if (method != null)
                            {
                                var task = (Task)method.Invoke(handler, [@event, cancellationToken])!;
                                await task;
                            }
                        }
                        catch (Exception)
                        {
                            // In production, log via ILogger. 
                            // For v0.1 we suppress to maintain zero infrastructure dependency in Application.
                        }
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.Complete();
        try
        {
            _processingTask.GetAwaiter().GetResult();
        }
        catch
        {
            // Suppress background thread cancel exceptions
        }
        _cts.Dispose();
    }
}
