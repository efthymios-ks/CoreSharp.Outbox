using CoreSharp.Outbox.Triggers;

namespace CoreSharp.Outbox.Tests.Triggers;

public sealed class OutboxTriggerTests
{
    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Arrange 
        var exception = Record.Exception(() => new OutboxTrigger());

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task TriggerNewMessage_WhenCalled_ShouldTriggerNewMessage()
    {
        // Arrange
        var trigger = new OutboxTrigger();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var waitTask = trigger.WaitForNewMessageAsync(cts.Token);
        trigger.TriggerNewMessage();

        // Assert
        await waitTask; // Should complete before timeout
        Assert.True(waitTask.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WaitForNewMessagesAsync_WhenCanceled_ShouldReturnTimeout()
    {
        // Arrange
        var trigger = new OutboxTrigger();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        // Act
        var waitTask = trigger.WaitForNewMessageAsync(cts.Token);

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => waitTask);
    }

    [Fact]
    public async Task WaitForNewMessagesAsync_WhenTriggered_ShouldReturnFromTrigger()
    {
        // Arrange
        var trigger = new OutboxTrigger();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var waitTask = trigger.WaitForNewMessageAsync(cts.Token);
        trigger.TriggerNewMessage();

        // Assert
        await waitTask; // Should complete successfully
        Assert.True(waitTask.IsCompletedSuccessfully);
    }
}
