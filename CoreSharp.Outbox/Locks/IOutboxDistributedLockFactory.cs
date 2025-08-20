namespace CoreSharp.Outbox.Locks;

internal interface IOutboxDistributedLockFactory
{
    Task<IAsyncDisposable?> AcquireAsync(string lockId, TimeSpan duration, CancellationToken cancellationToken);
}
