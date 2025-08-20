using Testcontainers.MsSql;

namespace CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;

public sealed class OutboxSqlServerContainer : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();

    internal string ConnectionString
        => _sqlContainer.GetConnectionString();

    public async Task InitializeAsync()
        => await _sqlContainer.StartAsync();

    public async Task DisposeAsync()
    {
        await _sqlContainer.StopAsync();
        await _sqlContainer.DisposeAsync();
    }
}
