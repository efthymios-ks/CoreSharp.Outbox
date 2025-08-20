using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoreSharp.Outbox.Repositories;

internal sealed class OutboxMessageRepository(
    [FromKeyedServices(Constants.Domain)] Func<DbContext> dbContextFactory
    ) : IOutboxMessageRepository
{
    private readonly Func<DbContext> _dbContextFactory = dbContextFactory;

    public async Task<bool> HasPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = _dbContextFactory();
        return await dbContext
            .Set<OutboxMessage>()
            .AnyAsync(message => message.DateProcessed == null, cancellationToken);
    }

    public async Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        await using var dbContext = _dbContextFactory();
        var messageSet = dbContext.Set<OutboxMessage>();

        return await messageSet
            .Where(message => message.DateProcessed == null)
            .OrderBy(message => message.DateOccured)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
    }

    public async Task UpdateMessageAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outboxMessage);

        await using var dbContext = _dbContextFactory();
        var messageSet = dbContext.Set<OutboxMessage>();

        messageSet.Update(outboxMessage);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
