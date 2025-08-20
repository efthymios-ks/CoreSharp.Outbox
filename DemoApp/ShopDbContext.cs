using CoreSharp.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DemoApp;

public sealed class ShopDbContext(DbContextOptions<ShopDbContext> options)
    : DbContext(options)
{
    public DbSet<Purchase> Purchases { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigureOutbox();
    }
}
