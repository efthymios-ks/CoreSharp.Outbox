using CoreSharp.Outbox.Repositories;
using CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;
using Microsoft.EntityFrameworkCore;

namespace CoreSharp.Outbox.Tests.Repository;

[Collection(nameof(OutboxSqlServerCollection))]
public sealed class OutboxMessageRepositoryTests(OutboxSqlServerContainer sqlContainer)
    : OutboxSqlServerTestsBase(sqlContainer)
{
    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Act 
        var exception = Record.Exception(() => new OutboxMessageRepository(
            dbContextFactory: CreateDbContext
        ));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task HasPendingMessagesAsync_WhenHasPendingMessages_ShouldReturnTrue()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = MockCreate<OutboxMessageRepository>();

        await dbContext.Set<OutboxMessage>().AddAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MESSAGE_TYPE",
            Payload = "PAYLOAD",
            DateOccured = DateTimeOffset.UtcNow,
            DateProcessed = null
        });

        await dbContext.SaveChangesAsync();

        // Act
        var hasPendingMessages = await repository.HasPendingMessagesAsync();

        // Assert
        Assert.True(hasPendingMessages);
    }

    [Fact]
    public async Task HasPendingMessagesAsync_WhenHasNoPendingMessages_ShouldReturnFalse()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = MockCreate<OutboxMessageRepository>();

        // Act
        var hasPendingMessages = await repository.HasPendingMessagesAsync();

        // Assert
        Assert.False(hasPendingMessages);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task GetPendingMessagesAsync_WhenBatchSizeIsNegativeOrZero_ShouldThrowArgumentOutOfRangeException(int batchSize)
    {
        // Arrange 
        var repository = MockCreate<OutboxMessageRepository>();

        // Act
        async Task Action()
            => await repository.GetPendingMessagesAsync(batchSize);

        // Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(Action);
    }

    [Fact]
    public async Task GetPendingMessagesAsync_WhenCalled_ShouldReturnPendingMessages()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = MockCreate<OutboxMessageRepository>();

        var now = DateTimeOffset.UtcNow;

        var firstMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MESSAGE_TYPE",
            Payload = "PAYLOAD",
            DateOccured = DateTimeOffset.UtcNow
        };

        var secondMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MESSAGE_TYPE",
            Payload = "PAYLOAD",
            DateOccured = DateTimeOffset.UtcNow.AddMinutes(1)
        };

        var messageToSkip = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MESSAGE_TYPE",
            Payload = "PAYLOAD",
            DateOccured = now.AddMinutes(-10),
            DateProcessed = now.AddMicroseconds(-5)
        };

        await dbContext.Set<OutboxMessage>().AddRangeAsync(
            secondMessage,
            messageToSkip,
            firstMessage
        );

        await dbContext.SaveChangesAsync();

        // Act
        var messages = await repository.GetPendingMessagesAsync(10);

        // Assert
        Assert.Equivalent(
            ValuesToCompare([firstMessage, secondMessage]),
            ValuesToCompare(messages)
        );

        static IEnumerable<object> ValuesToCompare(IEnumerable<OutboxMessage> messages)
            => [.. messages.Select(message => new
            {
                message.Id,
                message.MessageType,
                message.Payload,
                message.DateOccured
            })];
    }

    [Fact]
    public async Task UpdateMessageAsync_WhenMessageIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var repository = MockCreate<OutboxMessageRepository>();

        // Act
        Task Action()
            => repository.UpdateMessageAsync(outboxMessage: null!);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(Action);
    }

    [Fact]
    public async Task UpdateMessageAsync_WhenCalled_ShouldUpdateMessage()
    {
        // Arrange
        using var dbContext = CreateDbContext();
        var repository = MockCreate<OutboxMessageRepository>();

        var now = DateTimeOffset.UtcNow;
        await dbContext.Set<OutboxMessage>().AddAsync(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "MESSAGE_TYPE",
            Payload = "PAYLOAD",
            DateOccured = now
        });

        await dbContext.SaveChangesAsync();

        // Act
        var existingMessage = await dbContext
            .Set<OutboxMessage>()
            .FirstAsync();
        existingMessage.DateProcessed = now.AddMinutes(1);
        existingMessage.Error = "Error";
        await repository.UpdateMessageAsync(existingMessage);

        // Assert
        var dbContextAfterSave = CreateDbContext();
        var updatedMessage = await dbContextAfterSave
            .Set<OutboxMessage>()
            .FindAsync(existingMessage.Id);
        Assert.NotNull(updatedMessage);
        Assert.Equal(existingMessage.DateProcessed, updatedMessage.DateProcessed);
        Assert.Equal(existingMessage.Error, updatedMessage.Error);
    }
}
