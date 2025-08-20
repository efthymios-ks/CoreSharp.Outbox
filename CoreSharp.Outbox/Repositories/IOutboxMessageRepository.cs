namespace CoreSharp.Outbox.Repositories;

internal interface IOutboxMessageRepository
{
    Task<bool> HasPendingMessagesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync(int batchSize, CancellationToken cancellationToken = default);

    Task UpdateMessageAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default);
}
