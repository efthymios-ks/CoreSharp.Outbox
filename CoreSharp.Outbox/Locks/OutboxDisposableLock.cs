using Microsoft.EntityFrameworkCore;

namespace CoreSharp.Outbox.Locks;

internal sealed class OutboxDisposableLock(
    Func<DbContext> dbContextFactory,
    string lockId,
    string owner
    ) : IAsyncDisposable
{
    private readonly Func<DbContext> _dbContextFactory = dbContextFactory;
    private readonly string _lockId = lockId;
    private readonly string _owner = owner;

    public async ValueTask DisposeAsync()
    {
        using var dbContext = _dbContextFactory();
        var lockSet = dbContext.Set<OutboxLock>();

        var existingLock = await lockSet.FirstOrDefaultAsync(@lock
            => @lock.Name == _lockId
            && @lock.AcquiredBy == _owner
        );

        if (existingLock is null)
        {
            return;
        }

        lockSet.Remove(existingLock);
        await dbContext.SaveChangesAsync();
    }
}
