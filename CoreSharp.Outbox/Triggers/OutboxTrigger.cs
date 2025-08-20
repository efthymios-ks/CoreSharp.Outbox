namespace CoreSharp.Outbox.Triggers;

internal sealed class OutboxTrigger : IOutboxTrigger
{
    private TaskCompletionSource _taskCancellationSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void TriggerNewMessage()
    {
        var previous = Interlocked.Exchange(ref _taskCancellationSource, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        previous.TrySetResult();
    }

    public async Task WaitForNewMessageAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _taskCancellationSource.TrySetCanceled(cancellationToken));
        await _taskCancellationSource.Task.ConfigureAwait(false);
    }
}
