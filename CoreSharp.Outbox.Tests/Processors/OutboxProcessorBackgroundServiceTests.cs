using CoreSharp.Outbox.Processors;
using CoreSharp.Outbox.Repositories;
using CoreSharp.Outbox.Tests.Internals;
using CoreSharp.Outbox.Triggers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;
using System.Reflection;

namespace CoreSharp.Outbox.Tests.Processors;

public sealed class OutboxProcessorBackgroundServiceTests : TestsBase
{
    private static readonly TimeSpan _executeWaitTimeout = TimeSpan.FromSeconds(10);

    private static readonly MethodInfo _executeAsyncMethod = typeof(BackgroundService)
        .GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly MethodInfo _executeInternalAsyncMethod = typeof(OutboxProcessorBackgroundService)
        .GetMethod("ExecuteInternalAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Arrange
        var repository = MockCreate<IOutboxMessageRepository>();
        var trigger = MockCreate<IOutboxTrigger>();
        var processor = MockCreate<IOutboxProcessor>();
        var logger = NullLogger<OutboxProcessorBackgroundService>.Instance;

        // Act 
        var exception = Record.Exception(() => new OutboxProcessorBackgroundService(
            repository,
            processor,
            trigger,
            logger
        ));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ShouldNotThrow()
    {
        // Arrange
        var repository = MockFreeze<IOutboxMessageRepository>();
        var service = MockCreate<OutboxProcessorBackgroundService>();

        using var cts = new CancellationTokenSource();
        repository
            .HasPendingMessagesAsync(default)
            .ReturnsForAnyArgs(async _ =>
            {
                cts.Cancel();

                // Run indefinitely, so that we can cancel the service
                await Task.Delay(Timeout.Infinite, cts.Token);

                // Won't be reached, but needed to satisfy the method signature
                return false;
            });

        // Act
        var exception = await Record.ExceptionAsync(() => (Task)_executeAsyncMethod.Invoke(
            service,
            [cts.Token]
        )!);

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenHasPendingMessages_ShouldProcess()
    {
        // Arrange
        var repository = MockFreeze<IOutboxMessageRepository>();
        var processor = MockFreeze<IOutboxProcessor>();
        var trigger = MockFreeze<IOutboxTrigger>();
        var service = MockCreate<OutboxProcessorBackgroundService>();

        repository
            .HasPendingMessagesAsync(default)
            .ReturnsForAnyArgs(true, false);

        using var cts = new CancellationTokenSource();
        trigger
            .WaitForNewMessageAsync(default)
            .ReturnsForAnyArgs(caller =>
            {
                cts.Cancel();

                // Never completes. Service will stop via cancellation
                return Task.Delay(Timeout.Infinite);
            });

        // Act
        await (Task)_executeInternalAsyncMethod.Invoke(
            service,
            [cts.Token]
        )!;

        var ctsCanceled = cts.Token.WaitHandle.WaitOne(_executeWaitTimeout);

        Assert.True(ctsCanceled);

        await repository
            .ReceivedWithAnyArgs()
            .HasPendingMessagesAsync(default);

        await processor
            .ReceivedWithAnyArgs(1)
            .ProcessAsync(default);
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenCanceled_ShouldExitGracefuly()
    {
        // Arrange
        var repository = MockFreeze<IOutboxMessageRepository>();
        var trigger = MockFreeze<IOutboxTrigger>();
        var service = MockCreate<OutboxProcessorBackgroundService>();

        using var cts = new CancellationTokenSource();
        repository
            .HasPendingMessagesAsync(default)
            .ReturnsForAnyArgs(_ =>
            {
                cts.Cancel();
                return false;
            });

        // Act
        await (Task)_executeInternalAsyncMethod.Invoke(
            service,
            [cts.Token]
        )!;

        var ctsCanceled = cts.Token.WaitHandle.WaitOne(_executeWaitTimeout);

        Assert.True(ctsCanceled);

        await repository
            .ReceivedWithAnyArgs()
            .HasPendingMessagesAsync(default);
    }

    [Fact]
    public async Task ExecuteInternalAsync_WhenCrashes_ShouldContinueIteration()
    {
        // Arrange
        var repository = MockFreeze<IOutboxMessageRepository>();
        var processor = MockFreeze<IOutboxProcessor>();
        var trigger = MockFreeze<IOutboxTrigger>();
        var service = MockCreate<OutboxProcessorBackgroundService>();

        using var cts = new CancellationTokenSource();
        repository
            .HasPendingMessagesAsync(default)
            .ReturnsForAnyArgs(
                _ => true, // #1 - Run once
                _ => // #2 - Cancel after the first run
                {
                    cts.Cancel();
                    return false;
                }
            );

        processor
            .ProcessAsync(default)
            .ThrowsForAnyArgs(new Exception("Error"));

        // Act
        await (Task)_executeInternalAsyncMethod.Invoke(
            service,
            [cts.Token]
        )!;

        var ctsCanceled = cts.Token.WaitHandle.WaitOne(_executeWaitTimeout);

        // Assert
        Assert.True(ctsCanceled);

        await repository
            .ReceivedWithAnyArgs(2)
            .HasPendingMessagesAsync(default);
    }
}
