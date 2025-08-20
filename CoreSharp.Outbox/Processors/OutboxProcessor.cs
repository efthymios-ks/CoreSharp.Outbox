using CoreSharp.Outbox.Locks;
using CoreSharp.Outbox.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreSharp.Outbox.Processors;

internal sealed class OutboxProcessor(
    IOutboxDistributedLockFactory outboxDistributedLockFactory,
    IOutboxMessageRepository outboxMessageRepository,
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger
    ) : IOutboxProcessor
{
    private const string LockId = nameof(OutboxProcessor);
    private readonly IOutboxDistributedLockFactory _outboxDistributedLockFactory = outboxDistributedLockFactory;
    private readonly IOutboxMessageRepository _outboxMessageRepository = outboxMessageRepository;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger _logger = logger;

    private static readonly TimeSpan _lockDuration = TimeSpan.FromMinutes(30);

    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        await using var @lock = await _outboxDistributedLockFactory.AcquireAsync(LockId, _lockDuration, cancellationToken);
        if (@lock is null)
        {
            _logger.LogWarning("Could not acquire lock '{LockId}', skipping this cycle", LockId);
            return;
        }

        var pendingMessagesDb = await _outboxMessageRepository.GetPendingMessagesAsync(
            batchSize: 20,
            cancellationToken
        );

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var outboxPublisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();

        foreach (var messageDb in pendingMessagesDb)
        {
            try
            {
                await outboxPublisher.PublishAsync(messageDb.MessageType, messageDb.Payload, cancellationToken);
                messageDb.DateProcessed = _timeProvider.GetUtcNow();
                messageDb.Error = null;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to process '{MessageType}' outbox message with id '{MessageId}'",
                    messageDb.MessageType,
                    messageDb.Id
                );

                messageDb.Error = exception.ToString();
            }

            await _outboxMessageRepository.UpdateMessageAsync(messageDb, cancellationToken);
        }
    }
}
