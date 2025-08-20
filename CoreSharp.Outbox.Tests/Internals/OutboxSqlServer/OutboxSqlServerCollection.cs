namespace CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;

[CollectionDefinition(nameof(OutboxSqlServerCollection), DisableParallelization = true)]
public sealed class OutboxSqlServerCollection : ICollectionFixture<OutboxSqlServerContainer>;
