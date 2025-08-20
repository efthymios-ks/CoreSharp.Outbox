using CoreSharp.Outbox.Locks;
using CoreSharp.Outbox.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;

public abstract class OutboxSqlServerTestsBase(OutboxSqlServerContainer sqlContainer)
    : TestsBase, IAsyncLifetime
{
    private readonly OutboxSqlServerContainer _sqlContainer = sqlContainer;

    protected OutboxDbContext DbContext { get; private set; } = null!;

    protected string ConnectionString
        => _sqlContainer.ConnectionString;

    public async Task InitializeAsync()
    {
        DbContext ??= CreateDbContext();
        await DbContext.Database.EnsureCreatedAsync();
        await CleanUpAsync(DbContext);
    }

    public Task DisposeAsync()
        => Task.CompletedTask;

    protected override void ConfigureFixture(IFixture fixture)
    {
        base.ConfigureFixture(fixture);

        fixture.Customize<Func<DbContext>>(builder => builder.FromFactory(() => CreateDbContext));
        fixture.Customize<DbContext>(builder => builder.FromFactory(() => CreateDbContext()));
    }

    protected OutboxDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OutboxDbContext>()
            .UseSqlServer(_sqlContainer.ConnectionString)
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .Options;
        return new OutboxDbContext(options);
    }

    [SuppressMessage("Security", "EF1002:Risk of vulnerability to SQL injection.", Justification = "<Pending>")]
    private static async Task CleanUpAsync(DbContext dbContext)
    {
        var entitiesToTruncate = new Type[]
        {
            typeof(OutboxLock),
            typeof(OutboxMessage)
        };

        var tablesToTruncate = dbContext
            .Model
            .GetEntityTypes()
            .Where(entityType => entitiesToTruncate.Contains(entityType.ClrType))
            .Select(entityType =>
            {
                var schema = entityType.GetSchema() ?? "dbo";
                var tableName = entityType.GetTableName();
                return $"[{schema}].[{tableName}]";
            })
            .ToArray();

        foreach (var table in tablesToTruncate)
        {
            await dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {table}");
        }

        dbContext.ChangeTracker.DetectChanges();
        dbContext.ChangeTracker.Clear();
    }
}
