using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoreSharp.Outbox.Locks;

internal sealed class OutboxDistributedLockFactory(
    [FromKeyedServices(Constants.Domain)] Func<DbContext> dbContextFactory,
    TimeProvider timeProvider
    ) : IOutboxDistributedLockFactory
{
    private static readonly string _owner = $"{Environment.MachineName}_{Environment.ProcessId}_{Guid.NewGuid()}";

    private readonly Func<DbContext> _dbContextFactory = dbContextFactory;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IAsyncDisposable?> AcquireAsync(string lockId, TimeSpan duration, CancellationToken cancellationToken)
    {
        await using var dbContext = _dbContextFactory();
        var lockSet = dbContext.Set<OutboxLock>();

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(duration);
        var existingLock = await lockSet.FirstOrDefaultAsync(@lock => @lock.Name == lockId, cancellationToken);

        // Lock does not exist
        if (existingLock is null)
        {
            await lockSet.AddAsync(new()
            {
                Id = Guid.NewGuid(),
                Name = lockId,
                AcquiredBy = _owner,
                DateAcquired = now,
                DateToExpire = expiresAt
            }, cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return new OutboxDisposableLock(_dbContextFactory, lockId, _owner);
            }
            catch (DbUpdateException)
            {
                // Conflict on insert, someone else acquired the lock
                return null;
            }
        }

        // If lock exists and expired, "steal" it
        if (now >= existingLock.DateToExpire)
        {
            existingLock.AcquiredBy = _owner;
            existingLock.DateAcquired = now;
            existingLock.DateToExpire = expiresAt;

            try
            {
                lockSet.Update(existingLock);
                await dbContext.SaveChangesAsync(cancellationToken);
                return new OutboxDisposableLock(_dbContextFactory, lockId, _owner);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another process stole the lock
                return null;
            }
        }

        // Lock exists and not expired
        return null;
    }
}
