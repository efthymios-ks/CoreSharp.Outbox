using CoreSharp.Outbox.Registry;
using CoreSharp.Outbox.Repositories;
using CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;
using CoreSharp.Outbox.Transactions;
using CoreSharp.Outbox.Triggers;

namespace CoreSharp.Outbox.Tests.Transactions;

[Collection(nameof(OutboxSqlServerCollection))]
public sealed class OutboxTransactionFactoryTests(OutboxSqlServerContainer sqlContainer)
    : OutboxSqlServerTestsBase(sqlContainer)
{
    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Arrange
        var registry = MockCreate<IOutboxMessageRegistry>();
        var trigger = MockCreate<IOutboxTrigger>();
        var timeProvider = MockCreate<TimeProvider>();

        // Act 
        var exception = Record.Exception(() => new OutboxTransactionFactory(
            registry,
            trigger,
            timeProvider
        ));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task CreateAsync_WhenDbContextIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange 
        var factory = MockCreate<OutboxTransactionFactory>();

        // Act
        Task Action()
            => factory.CreateAsync(dbContext: null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Action);
    }

    [Fact]
    public async Task CreateAsync_WhenTransactionIsAlreadyStarted_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var factory = MockCreate<OutboxTransactionFactory>();

        // Act 
        await DbContext.Database.BeginTransactionAsync();

        Task Action()
            => factory.CreateAsync(DbContext);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(Action);
    }

    [Fact]
    public async Task CreateAsync_WhenCalled_ShouldBeginSqlTransactionAndReturnOutboxTransaction()
    {
        // Arrange
        var factory = MockCreate<OutboxTransactionFactory>();

        // Act 
        await using var transaction = await factory.CreateAsync(DbContext);

        // Assert
        Assert.NotNull(transaction);
        Assert.IsType<OutboxTransaction>(transaction);
        Assert.NotNull(DbContext.Database.CurrentTransaction);
    }
}
