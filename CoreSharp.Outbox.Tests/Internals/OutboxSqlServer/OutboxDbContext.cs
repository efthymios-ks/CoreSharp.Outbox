using Microsoft.EntityFrameworkCore;

namespace CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;

public sealed class OutboxDbContext(DbContextOptions<OutboxDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigureOutbox();
    }
}
