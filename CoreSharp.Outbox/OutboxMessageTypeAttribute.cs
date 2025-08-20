using System.Diagnostics.CodeAnalysis;

namespace CoreSharp.Outbox;

[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class OutboxMessageTypeAttribute(string type) : Attribute
{
    public string Type { get; } = type;
}
