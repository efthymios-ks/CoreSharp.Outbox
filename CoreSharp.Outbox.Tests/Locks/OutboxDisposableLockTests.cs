using CoreSharp.Outbox.Locks;
using CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;
using Microsoft.EntityFrameworkCore;

namespace CoreSharp.Outbox.Tests.Locks;

[Collection(nameof(OutboxSqlServerCollection))]
public sealed class OutboxDisposableLockTests(OutboxSqlServerContainer sqlContainer)
    : OutboxSqlServerTestsBase(sqlContainer)
{
    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Act 
        var exception = Record.Exception(() => new OutboxDisposableLock(
            dbContextFactory: CreateDbContext,
            lockId: "LOCK",
            owner: "OWNER"
        ));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_WhenLockNotFound_ShouldDoNothing()
    {
        // Arrange 
        var lockSet = DbContext.Set<OutboxLock>();

        // Act
        var disposableLock = new OutboxDisposableLock(
            dbContextFactory: CreateDbContext,
            lockId: "LOCK",
            owner: "OWNER"
        );

        await disposableLock.DisposeAsync();

        // Assert
        var locksAfterDispose = await lockSet.ToArrayAsync();
        Assert.Empty(locksAfterDispose);
    }

    [Fact]
    public async Task DisposeAsync_WhenLockExists_ShouldRemove()
    {
        // Arrange  
        var lockSet = DbContext.Set<OutboxLock>();
        var existingLock = new OutboxLock
        {
            Id = Guid.NewGuid(),
            Name = "LOCK",
            AcquiredBy = "OWNER",
            DateAcquired = DateTimeOffset.UtcNow,
            DateToExpire = DateTimeOffset.UtcNow.AddMinutes(5),
            RowVersion = []
        };

        lockSet.Add(existingLock);
        await DbContext.SaveChangesAsync();

        // Act
        var disposableLock = new OutboxDisposableLock(
            dbContextFactory: CreateDbContext,
            lockId: "LOCK",
            owner: "OWNER"
        );

        await disposableLock.DisposeAsync();

        // Assert
        var locksAfterDispose = await lockSet.ToArrayAsync();
        Assert.Empty(locksAfterDispose);
    }
}
