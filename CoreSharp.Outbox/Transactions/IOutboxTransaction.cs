namespace CoreSharp.Outbox.Transactions;

public interface IOutboxTransaction : IAsyncDisposable
{
    Task AddMessageAsync<TPayload>(TPayload payload, CancellationToken cancellationToken = default)
        where TPayload : class;

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
