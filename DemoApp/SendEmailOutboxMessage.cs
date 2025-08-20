using CoreSharp.Outbox;

namespace DemoApp;

[OutboxMessageType("send_email_v1")]
public sealed class SendEmailOutboxMessage
{
    public required string Sender { get; init; }
    public required string Recipient { get; init; }
    public required string Body { get; init; }
}
