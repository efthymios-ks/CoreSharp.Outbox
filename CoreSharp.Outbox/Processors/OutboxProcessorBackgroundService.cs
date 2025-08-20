using CoreSharp.Outbox.Repositories;
using CoreSharp.Outbox.Triggers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreSharp.Outbox.Processors;

internal sealed class OutboxProcessorBackgroundService(
    IOutboxMessageRepository outboxMessageRepository,
    IOutboxProcessor outboxProcessor,
    IOutboxTrigger outboxTrigger,
    ILogger<OutboxProcessorBackgroundService> logger
    ) : BackgroundService
{
    private readonly IOutboxMessageRepository _outboxMessageRepository = outboxMessageRepository;
    private readonly IOutboxProcessor _outboxProcessor = outboxProcessor;
    private readonly IOutboxTrigger _outboxTrigger = outboxTrigger;
    private readonly ILogger _logger = logger;
    private static readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(5);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Factory.StartNew(
            () => ExecuteInternalAsync(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );

    private async Task ExecuteInternalAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting {ServiceName}", nameof(OutboxProcessorBackgroundService));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                while (await _outboxMessageRepository.HasPendingMessagesAsync(cancellationToken))
                {
                    await _outboxProcessor.ProcessAsync(cancellationToken);
                }

                var notifyTask = _outboxTrigger.WaitForNewMessageAsync(cancellationToken);
                var delayTask = Task.Delay(_pollingInterval, cancellationToken);
                await Task.WhenAny(notifyTask, delayTask);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown, no logging needed
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "{ServiceName} encountered an error", nameof(OutboxProcessorBackgroundService));
            }
        }

        _logger.LogInformation("Stopped {ServiceName}", nameof(OutboxProcessorBackgroundService));
    }
}
