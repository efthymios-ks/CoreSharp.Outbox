namespace CoreSharp.Outbox.Triggers;

internal interface IOutboxTrigger
{
    void TriggerNewMessage();
    Task WaitForNewMessageAsync(CancellationToken cancellationToken);
}
