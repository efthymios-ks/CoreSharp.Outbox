using CoreSharp.Outbox.Registry;
using CoreSharp.Outbox.Repositories;
using CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;
using CoreSharp.Outbox.Transactions;
using CoreSharp.Outbox.Triggers;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CoreSharp.Outbox.Tests.Transactions;

[Collection(nameof(OutboxSqlServerCollection))]
public sealed class OutboxTransactionTests(OutboxSqlServerContainer sqlContainer)
    : OutboxSqlServerTestsBase(sqlContainer)
{
    protected override void ConfigureFixture(IFixture fixture)
    {
        base.ConfigureFixture(fixture);

        fixture.Customize<TimeProvider>(config => config.FromFactory(() => Substitute.For<TimeProvider>()));
    }

    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Arrange
        var registry = MockCreate<IOutboxMessageRegistry>();
        var trigger = MockCreate<IOutboxTrigger>();
        var timeProvider = MockCreate<TimeProvider>();

        // Act 
        var exception = Record.Exception(() => new OutboxTransaction(
            DbContext,
            registry,
            trigger,
            timeProvider
        ));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task AddMessageAsync_WhenPayloadIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange 
        await using var transaction = MockCreate<OutboxTransaction>();

        // Act
        Task Action()
            => transaction.AddMessageAsync<object>(payload: null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Action);
    }

    [Fact]
    public async Task AddMessageAsync_WhenCalled_ShouldAddMessageToDbContext()
    {
        // Arrange
        await using var dbContext = MockFreeze<DbContext>();
        var registry = MockFreeze<IOutboxMessageRegistry>();
        var timeProvider = MockFreeze<TimeProvider>();
        await using var transaction = MockCreate<OutboxTransaction>();

        registry
            .GetMessageType(default!)
            .ReturnsForAnyArgs("MESSAGE_TYPE");

        var now = DateTimeOffset.UtcNow;
        timeProvider
            .GetUtcNow()
            .Returns(now);

        // Act 
        await using (var _ = await dbContext.Database.BeginTransactionAsync())
        {
            await transaction.AddMessageAsync(new
            {
                Key = "VALUE"
            });
            await dbContext.SaveChangesAsync();
            await dbContext.Database.CommitTransactionAsync();
        }

        // Assert
        var dbContextAfterSave = CreateDbContext();
        var messageDbAfterSave = await dbContextAfterSave
            .Set<OutboxMessage>()
            .FirstAsync();

        Assert.NotEqual(Guid.Empty, messageDbAfterSave.Id);
        Assert.Equal(now, messageDbAfterSave.DateOccured);
        Assert.Equal("MESSAGE_TYPE", messageDbAfterSave.MessageType);
        Assert.Contains("VALUE", messageDbAfterSave.Payload);
    }

    [Fact]
    public async Task CommitAsync_WhenCalled_ShouldSaveAndCommitTransaction()
    {
        // Arrange
        await using var dbContext = MockFreeze<DbContext>();
        await using var transaction = MockCreate<OutboxTransaction>();

        // Act 
        await using (var _ = await dbContext.Database.BeginTransactionAsync())
        {
            await transaction.AddMessageAsync(new
            {
                Key = "VALUE"
            });
            await transaction.CommitAsync();
        }

        // Assert
        var dbContextAfterSave = CreateDbContext();
        var messagesDbAfterSave = await dbContextAfterSave
            .Set<OutboxMessage>()
            .ToArrayAsync();

        Assert.Single(messagesDbAfterSave);
    }

    [Fact]
    public async Task CommitAsync_WhenCalled_ShouldNotifyOutboxTrigger()
    {
        // Arrange
        await using var dbContext = MockFreeze<DbContext>();
        var trigger = MockFreeze<IOutboxTrigger>();
        await using var transaction = MockCreate<OutboxTransaction>();

        // Act 
        await using (var _ = await dbContext.Database.BeginTransactionAsync())
        {
            await transaction.AddMessageAsync(new
            {
                Key = "VALUE"
            });
            await transaction.CommitAsync();
        }

        // Assert
        trigger
            .Received(1)
            .TriggerNewMessage();
    }

    [Fact]
    public async Task RollbackAsync_WhenCalled_ShouldRollbackTransaction()
    {
        // Arrange
        await using var dbContext = MockFreeze<DbContext>();
        await using var transaction = MockCreate<OutboxTransaction>();

        // Act 
        await using (var _ = await dbContext.Database.BeginTransactionAsync())
        {
            await transaction.AddMessageAsync(new
            {
                Key = "VALUE"
            });
            await transaction.RollbackAsync();
        }

        // Assert
        var dbContextAfterSave = CreateDbContext();
        var messagesDbAfterSave = await dbContextAfterSave
            .Set<OutboxMessage>()
            .ToArrayAsync();
        Assert.Empty(messagesDbAfterSave);
    }

    [Fact]
    public async Task DisposeAsync_WhenInnerTransactionAlreadyDisposed_ShouldNotThrow()
    {
        // Arrange
        await using var dbContext = MockFreeze<DbContext>();
        await using var transaction = MockCreate<OutboxTransaction>();

        // Act
        var exception = await Record.ExceptionAsync(() => transaction.DisposeAsync().AsTask());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_WhenInnerTransactionNotDisposed_ShouldDisposeTransaction()
    {
        // Arrange
        await using var dbContext = MockFreeze<DbContext>();
        await using var transaction = MockCreate<OutboxTransaction>();

        // Act
        await dbContext.Database.BeginTransactionAsync();
        await transaction.DisposeAsync();

        // Assert 
        Assert.Null(dbContext.Database.CurrentTransaction);
    }
}
