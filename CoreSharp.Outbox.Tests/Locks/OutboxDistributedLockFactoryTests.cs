using CoreSharp.Outbox.Locks;
using CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;
using Microsoft.EntityFrameworkCore;

namespace CoreSharp.Outbox.Tests.Locks;

[Collection(nameof(OutboxSqlServerCollection))]
public sealed class OutboxDistributedLockFactoryTests(OutboxSqlServerContainer sqlContainer)
    : OutboxSqlServerTestsBase(sqlContainer)
{
    protected override void ConfigureFixture(IFixture fixture)
    {
        base.ConfigureFixture(fixture);

        fixture.Customize<TimeProvider>(builder => builder.FromFactory(() => Substitute.For<TimeProvider>()));
    }

    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Arrange
        var timeProvider = MockCreate<TimeProvider>();

        // Act 
        var exception = Record.Exception(() => new OutboxDistributedLockFactory(
            dbContextFactory: CreateDbContext,
            timeProvider
        ));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task AcquireAsync_WhenLockNotFound_ShouldReturnLock()
    {
        // Arrange
        var timeProvider = MockFreeze<TimeProvider>();
        var factory = MockCreate<OutboxDistributedLockFactory>();

        var now = DateTimeOffset.UtcNow;
        timeProvider
            .GetUtcNow()
            .Returns(now);

        // Act
        var disposableLock = await factory.AcquireAsync(
            lockId: "LOCK",
            duration: TimeSpan.FromMinutes(15),
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(disposableLock);
        Assert.IsType<OutboxDisposableLock>(disposableLock);

        var dbContextAfterSave = CreateDbContext();
        var lockDb = await dbContextAfterSave
            .Set<OutboxLock>()
            .FirstAsync();
        Assert.NotEqual(Guid.Empty, lockDb.Id);
        Assert.Equal("LOCK", lockDb.Name);
        Assert.Contains(Environment.MachineName, lockDb.AcquiredBy);
        Assert.Equal(now, lockDb.DateAcquired);
        Assert.Equal(now.AddMinutes(15), lockDb.DateToExpire);
        Assert.NotNull(lockDb.RowVersion);
        Assert.NotEmpty(lockDb.RowVersion);
    }

    [Fact]
    public async Task AcquireAsync_WhenLockExistsAndExpired_ShouldReturnLock()
    {
        // Arrange
        var timeProvider = MockFreeze<TimeProvider>();
        var factory = MockCreate<OutboxDistributedLockFactory>();

        var now = DateTimeOffset.UtcNow;
        timeProvider
            .GetUtcNow()
            .Returns(now);

        var existingLock = new OutboxLock
        {
            Id = Guid.NewGuid(),
            Name = "LOCK",
            AcquiredBy = "OTHER_OWNER",
            DateAcquired = now.AddMinutes(-20),
            DateToExpire = now.AddMinutes(-5),
        };

        await DbContext.Set<OutboxLock>().AddAsync(existingLock);
        await DbContext.SaveChangesAsync();

        // Act
        var disposableLock = await factory.AcquireAsync(
            lockId: "LOCK",
            duration: TimeSpan.FromMinutes(15),
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(disposableLock);
        Assert.IsType<OutboxDisposableLock>(disposableLock);

        var dbContextAfterSave = CreateDbContext();
        var lockDb = await dbContextAfterSave
            .Set<OutboxLock>()
            .FirstAsync();
        Assert.Equal(existingLock.Id, lockDb.Id);
        Assert.Equal("LOCK", lockDb.Name);
        Assert.Contains(Environment.MachineName, lockDb.AcquiredBy);
        Assert.Equal(now, lockDb.DateAcquired);
        Assert.Equal(now.AddMinutes(15), lockDb.DateToExpire);
    }

    [Fact]
    public async Task AcquireAsync_WhenLockExistsAndNotExpired_ShouldReturnNull()
    {
        // Arrange
        var timeProvider = MockFreeze<TimeProvider>();
        var factory = MockCreate<OutboxDistributedLockFactory>();

        var now = DateTimeOffset.UtcNow;
        timeProvider
           .GetUtcNow()
           .Returns(now);

        var existingLock = new OutboxLock
        {
            Id = Guid.NewGuid(),
            Name = "LOCK",
            AcquiredBy = "OTHER_OWNER",
            DateAcquired = now.AddMinutes(-5),
            DateToExpire = now.AddMinutes(10),
        };

        await DbContext.Set<OutboxLock>().AddAsync(existingLock);
        await DbContext.SaveChangesAsync();

        // Act
        var disposableLock = await factory.AcquireAsync(
            lockId: "LOCK",
            duration: TimeSpan.FromMinutes(15),
            CancellationToken.None
        );

        // Assert
        Assert.Null(disposableLock);
    }
}
