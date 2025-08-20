namespace CoreSharp.Outbox.Registry;

internal interface IOutboxMessageRegistry
{
    string GetMessageType(Type payloadType);
}
