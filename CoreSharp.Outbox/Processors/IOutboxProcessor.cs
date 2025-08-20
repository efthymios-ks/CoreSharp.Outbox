namespace CoreSharp.Outbox.Processors;

internal interface IOutboxProcessor
{
    Task ProcessAsync(CancellationToken cancellationToken = default);
}
