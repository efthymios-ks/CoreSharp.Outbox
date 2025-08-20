using Microsoft.EntityFrameworkCore;

namespace CoreSharp.Outbox.Transactions;

public interface IOutboxTransactionFactory
{
    Task<IOutboxTransaction> CreateAsync(DbContext dbContext, CancellationToken cancellationToken = default);
}
