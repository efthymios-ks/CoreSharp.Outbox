using CoreSharp.Outbox;
using Microsoft.Extensions.Logging;

namespace DemoApp;

public sealed class DemoAppOutboxPublisher(ILogger<DemoAppOutboxPublisher> logger) : IOutboxPublisher
{
    private readonly ILogger _logger = logger;

    public Task PublishAsync(string messageType, string payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Publishing message of type {MessageType} with payload: {Payload}", messageType, payload);

        return Task.CompletedTask;
    }
}
