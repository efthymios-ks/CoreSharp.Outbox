using CoreSharp.Outbox.Registry;

namespace CoreSharp.Outbox.Tests.Registry;

public sealed class OutboxMessageRegistryTests
{
    [Fact]
    public void Constructor_WhenCalled_ShouldNotThrow()
    {
        // Act
        var exception = Record.Exception(() => new OutboxMessageRegistry(payloadTypes: []));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void GetMessageType_WhenPayloadTypeIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var registry = new OutboxMessageRegistry(payloadTypes: []);

        // Act
        void Action()
            => registry.GetMessageType(payloadType: null!);

        // Assert
        Assert.Throws<ArgumentNullException>(Action);
    }

    [Fact]
    public void GetMessageType_WhenPayloadTypeNotFound_ShouldThrowInvalidOperationException()
    {
        // Arrange 
        var registry = new OutboxMessageRegistry(payloadTypes: []);

        // Act
        void Action()
            => registry.GetMessageType(typeof(OutboxMessageRegistryTests));

        // Assert
        Assert.Throws<InvalidOperationException>(Action);
    }

    [Fact]
    public void GetMessageType_WhenPayloadTypeFound_ShouldReturnMessageType()
    {
        // Arrange 
        var registry = new OutboxMessageRegistry(new()
        {
            [typeof(OutboxMessageRegistryTests)] = "CUSTOM_MESSAGE_TYPE"
        });

        // Act
        var messageType = registry.GetMessageType(typeof(OutboxMessageRegistryTests));

        // Assert
        Assert.Equal("CUSTOM_MESSAGE_TYPE", messageType);
    }
}
