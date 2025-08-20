using CoreSharp.Outbox.Registry;
using CoreSharp.Outbox.Repositories;
using CoreSharp.Outbox.Triggers;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreSharp.Outbox.Transactions;

internal sealed class OutboxTransaction(
    DbContext dbContext,
    IOutboxMessageRegistry outboxMessageRegistry,
    IOutboxTrigger outboxTrigger,
    TimeProvider timeProvider
    ) : IOutboxTransaction
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly DbContext _dbContext = dbContext;
    private readonly IOutboxMessageRegistry _outboxMessageRegistry = outboxMessageRegistry;
    private readonly IOutboxTrigger _outboxTrigger = outboxTrigger;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task AddMessageAsync<TPayload>(TPayload payload, CancellationToken cancellationToken = default)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(payload);

        var payloadType = payload.GetType();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            DateOccured = _timeProvider.GetUtcNow(),
            MessageType = _outboxMessageRegistry.GetMessageType(payloadType),
            Payload = JsonSerializer.Serialize(payload, payloadType, _jsonOptions),
        };

        await _dbContext
            .Set<OutboxMessage>()
            .AddAsync(message, cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _dbContext.Database.CommitTransactionAsync(cancellationToken);
        _outboxTrigger.TriggerNewMessage();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Database.RollbackTransactionAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        var transaction = _dbContext.Database.CurrentTransaction;
        if (transaction is null)
        {
            return;
        }

        await transaction.DisposeAsync();
    }
}
