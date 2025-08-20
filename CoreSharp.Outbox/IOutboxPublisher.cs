namespace CoreSharp.Outbox;

public interface IOutboxPublisher
{
    Task PublishAsync(string messageType, string payload, CancellationToken cancellationToken = default);
}
