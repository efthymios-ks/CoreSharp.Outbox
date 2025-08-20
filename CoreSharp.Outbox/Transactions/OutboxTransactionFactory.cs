using CoreSharp.Outbox.Registry;
using CoreSharp.Outbox.Triggers;
using Microsoft.EntityFrameworkCore;

namespace CoreSharp.Outbox.Transactions;

internal sealed class OutboxTransactionFactory(
    IOutboxMessageRegistry outboxMessageRegistry,
    IOutboxTrigger outboxTrigger,
    TimeProvider timeProvider
    ) : IOutboxTransactionFactory
{
    private readonly IOutboxMessageRegistry _outboxMessageRegistry = outboxMessageRegistry;
    private readonly IOutboxTrigger _outboxTrigger = outboxTrigger;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IOutboxTransaction> CreateAsync(DbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException(message: "Transaction is already started");
        }

        await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new OutboxTransaction(dbContext, _outboxMessageRegistry, _outboxTrigger, _timeProvider);
    }
}
