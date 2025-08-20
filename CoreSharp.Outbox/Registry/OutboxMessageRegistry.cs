namespace CoreSharp.Outbox.Registry;

internal sealed class OutboxMessageRegistry(
     Dictionary<Type, string> payloadTypes
    ) : IOutboxMessageRegistry
{
    private readonly Dictionary<Type, string> _messageTypeMap = payloadTypes;

    public string GetMessageType(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        if (_messageTypeMap.TryGetValue(payloadType, out var messageType))
        {
            return messageType;
        }

        throw new InvalidOperationException(
            $"Payload type '{payloadType.FullName}' is not registered as an outbox message." +
            $" Make sure it is decorated with the '{nameof(OutboxMessageTypeAttribute)}' attribute."
        );
    }
}
