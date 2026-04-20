namespace labyItems.Infrastructure;

public interface IEventSubscriber<T>
{
    Task OnEventAsync(T eventData);
}

public interface IEventPublisher
{
    void Subscribe<T>(IEventSubscriber<T> subscriber) where T : notnull;
    void Unsubscribe<T>(IEventSubscriber<T> subscriber) where T : notnull;
    Task PublishAsync<T>(T eventData) where T : notnull;
}
