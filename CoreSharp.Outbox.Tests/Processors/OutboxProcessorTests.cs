using CoreSharp.Outbox.Locks;
using CoreSharp.Outbox.Processors;
using CoreSharp.Outbox.Repositories;
using CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace CoreSharp.Outbox.Tests.Processors;

[Collection(nameof(OutboxSqlServerCollection))]
public sealed class OutboxProcessorTests(OutboxSqlServerContainer sqlContainer)
    : OutboxSqlServerTestsBase(sqlContainer)
{
    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Arrange
        var lockFactory = MockCreate<IOutboxDistributedLockFactory>();
        var repository = MockCreate<IOutboxMessageRepository>();
        var serviceScopeFactory = MockCreate<IServiceScopeFactory>();
        var timeProvider = MockCreate<TimeProvider>();
        var logger = NullLogger<OutboxProcessor>.Instance;

        // Act 
        var exception = Record.Exception(() => new OutboxProcessor(
            lockFactory,
            repository,
            serviceScopeFactory,
            timeProvider,
            logger
        ));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task ProcessAsync_WhenLockNotAcquired_ShouldDoNothing()
    {
        // Arrange
        var lockFactory = MockFreeze<IOutboxDistributedLockFactory>();
        var repository = MockFreeze<IOutboxMessageRepository>();
        var processor = MockCreate<OutboxProcessor>();

        lockFactory
            .AcquireAsync(default!, default!, default)
            .ReturnsForAnyArgs(Task.FromResult<IAsyncDisposable?>(null));

        // Act
        await processor.ProcessAsync(default);

        // Assert
        await lockFactory
            .ReceivedWithAnyArgs(1)
            .AcquireAsync(default!, default, default);

        await repository
            .DidNotReceiveWithAnyArgs()
            .GetPendingMessagesAsync(default, default);
    }

    [Fact]
    public async Task ProcessAsync_WhenFinished_ShouldDisposeLock()
    {
        // Arrange
        var lockFactory = MockFreeze<IOutboxDistributedLockFactory>();
        var repository = MockFreeze<IOutboxMessageRepository>();
        var serviceScopeFactory = MockFreeze<IServiceScopeFactory>();
        var processor = MockCreate<OutboxProcessor>();

        var @lock = MockCreate<IAsyncDisposable>();
        lockFactory
            .AcquireAsync(default!, default!, default)
            .ReturnsForAnyArgs(Task.FromResult<IAsyncDisposable?>(@lock));

        repository
            .GetPendingMessagesAsync(default, default)
            .ReturnsForAnyArgs(Task.FromResult<IEnumerable<OutboxMessage>>([]));

        var serviceScope = MockCreate<IServiceScope>();
        serviceScopeFactory
            .CreateAsyncScope()
            .Returns(serviceScope);

        var publisher = MockCreate<IOutboxPublisher>();
        serviceScope
            .ServiceProvider
            .GetService(typeof(IOutboxPublisher))
            .Returns(publisher);

        // Act
        await processor.ProcessAsync(default);

        // Assert
        await lockFactory
            .ReceivedWithAnyArgs(1)
            .AcquireAsync(default!, default, default);

        await @lock
            .Received(1)
            .DisposeAsync();
    }

    [Fact]
    public async Task ProcessAsync_WhenPublishSucceeds_ShouldUpdateMessage()
    {
        // Arrange
        var lockFactory = MockFreeze<IOutboxDistributedLockFactory>();
        var repository = MockFreeze<IOutboxMessageRepository>();
        var serviceScopeFactory = MockFreeze<IServiceScopeFactory>();
        var processor = MockCreate<OutboxProcessor>();

        var @lock = MockCreate<IAsyncDisposable>();
        lockFactory
            .AcquireAsync(default!, default!, default)
            .ReturnsForAnyArgs(Task.FromResult<IAsyncDisposable?>(@lock));

        repository
            .GetPendingMessagesAsync(default, default)
            .ReturnsForAnyArgs(Task.FromResult<IEnumerable<OutboxMessage>>([
                new()
                {
                    Id = Guid.NewGuid(),
                    MessageType = "MESSAGE_TYPE",
                    Payload = "PAYLOAD",
                    DateOccured  = DateTimeOffset.UtcNow,
                    DateProcessed = null,
                    Error = "Error"
                }
            ]));

        OutboxMessage capturedMessage = null!;
        repository
            .UpdateMessageAsync(default!, default)
            .ReturnsForAnyArgs(caller =>
            {
                capturedMessage = (OutboxMessage)caller[0];
                return Task.CompletedTask;
            });

        var serviceScope = MockCreate<IServiceScope>();
        serviceScopeFactory
            .CreateAsyncScope()
            .Returns(serviceScope);

        var publisher = MockCreate<IOutboxPublisher>();
        serviceScope
            .ServiceProvider
            .GetService(typeof(IOutboxPublisher))
            .Returns(publisher);

        // Act
        await processor.ProcessAsync(default);

        // Assert
        await publisher
            .ReceivedWithAnyArgs(1)
            .PublishAsync(default!, default!, default);

        await repository
            .ReceivedWithAnyArgs(1)
            .UpdateMessageAsync(default!, default);

        Assert.NotNull(capturedMessage.DateProcessed);
        Assert.Null(capturedMessage.Error);
    }

    [Fact]
    public async Task ProcessAsync_WhenMessageHandlerFails_ShouldUpdateMessage()
    {
        // Arrange
        var lockFactory = MockFreeze<IOutboxDistributedLockFactory>();
        var repository = MockFreeze<IOutboxMessageRepository>();
        var serviceScopeFactory = MockFreeze<IServiceScopeFactory>();
        var processor = MockCreate<OutboxProcessor>();

        var @lock = MockCreate<IAsyncDisposable>();
        lockFactory
            .AcquireAsync(default!, default!, default)
            .ReturnsForAnyArgs(Task.FromResult<IAsyncDisposable?>(@lock));

        repository
            .GetPendingMessagesAsync(default, default)
            .ReturnsForAnyArgs(Task.FromResult<IEnumerable<OutboxMessage>>([
                new()
                {
                    Id = Guid.NewGuid(),
                    MessageType = "MESSAGE_TYPE",
                    Payload = "PAYLOAD",
                    DateOccured  = DateTimeOffset.UtcNow,
                    DateProcessed = null,
                    Error = null
                }
            ]));

        OutboxMessage capturedMessage = null!;
        repository
            .UpdateMessageAsync(default!, default)
            .ReturnsForAnyArgs(caller =>
            {
                capturedMessage = (OutboxMessage)caller[0];
                return Task.CompletedTask;
            });

        var serviceScope = MockCreate<IServiceScope>();
        serviceScopeFactory
            .CreateAsyncScope()
            .Returns(serviceScope);

        var publisher = MockCreate<IOutboxPublisher>();
        serviceScope
            .ServiceProvider
            .GetService(typeof(IOutboxPublisher))
            .Returns(publisher);

        var exceptionToThrow = new Exception("Error");
        publisher
            .PublishAsync(default!, default!, default)
            .ThrowsAsyncForAnyArgs(exceptionToThrow);

        // Act
        await processor.ProcessAsync(default);

        // Assert
        await publisher
            .ReceivedWithAnyArgs(1)
            .PublishAsync(default!, default!, default);

        await repository
            .ReceivedWithAnyArgs(1)
            .UpdateMessageAsync(default!, default);

        Assert.Equal(exceptionToThrow.ToString(), capturedMessage.Error);
    }
}
