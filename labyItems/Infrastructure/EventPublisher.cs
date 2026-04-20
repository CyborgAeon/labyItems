namespace labyItems.Infrastructure;

public sealed class EventPublisher : IEventPublisher
{
    private readonly Dictionary<Type, List<object>> _subscribers = new();

    public void Subscribe<T>(IEventSubscriber<T> subscriber) where T : notnull
    {
        var type = typeof(T);
        if (!_subscribers.ContainsKey(type))
            _subscribers[type] = new List<object>();

        _subscribers[type].Add(subscriber);
    }

    public void Unsubscribe<T>(IEventSubscriber<T> subscriber) where T : notnull
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var list))
            list.Remove(subscriber);
    }

    public async Task PublishAsync<T>(T eventData) where T : notnull
    {
        var type = typeof(T);
        if (!_subscribers.TryGetValue(type, out var list))
            return;

        var tasks = new List<Task>();
        foreach (var subscriber in list.OfType<IEventSubscriber<T>>())
            tasks.Add(subscriber.OnEventAsync(eventData));

        await Task.WhenAll(tasks);
    }
}
