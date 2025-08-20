using CoreSharp.Outbox.Locks;
using CoreSharp.Outbox.Processors;
using CoreSharp.Outbox.Registry;
using CoreSharp.Outbox.Repositories;
using CoreSharp.Outbox.Tests.Internals.OutboxSqlServer;
using CoreSharp.Outbox.Transactions;
using CoreSharp.Outbox.Triggers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace CoreSharp.Outbox.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddOutbox_WhenServicesIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act 
        void Action()
            => services.AddOutbox<OutboxDbContext, DummyOutboxPublisher>();

        // Assert
        Assert.Throws<ArgumentNullException>(Action);
    }

    [Fact]
    public void AddOutbox_WhenScanAssembliesIsNull_ShouldNotThrowArgumentNullException()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // Act 
        var exception = Record.Exception(() => serviceCollection.AddOutbox<OutboxDbContext, DummyOutboxPublisher>(scanAssemblies: null));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void AddOutbox_WhenCalled_ShouldRegisterServices()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();

        // Act
        serviceCollection.AddOutbox<OutboxDbContext, DummyOutboxPublisher>(scanAssemblies: [typeof(DummyOutboxMessage).Assembly]);
        serviceCollection.AddDbContext<OutboxDbContext>();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        // Assert 
        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Singleton
            && service.ServiceType == typeof(TimeProvider)
            && service.ImplementationInstance == TimeProvider.System
        );

        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Singleton
            && service.ServiceType == typeof(Func<DbContext>)
            && service.ServiceKey is string serviceKey
            && serviceKey == Constants.Domain
            && service.KeyedImplementationFactory is not null
            && service.KeyedImplementationFactory.Invoke(serviceProvider, serviceKey) is Func<DbContext> factory
            && factory() is OutboxDbContext
        );

        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Singleton
            && service.ServiceType == typeof(IOutboxDistributedLockFactory)
            && service.ImplementationType == typeof(OutboxDistributedLockFactory)
        );

        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Singleton
            && service.ServiceType == typeof(IOutboxProcessor)
            && service.ImplementationType == typeof(OutboxProcessor)
        );

        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Singleton
            && service.ServiceType == typeof(IOutboxMessageRegistry)
            && service.ImplementationInstance is OutboxMessageRegistry outboxMessageRegistry
            && outboxMessageRegistry.GetMessageType(typeof(DummyOutboxMessage)) == "DUMMY-MESSAGE"
        );

        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Singleton
            && service.ServiceType == typeof(IOutboxMessageRepository)
            && service.ImplementationType == typeof(OutboxMessageRepository)
        );

        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Scoped
            && service.ServiceType == typeof(IOutboxTransactionFactory)
            && service.ImplementationType == typeof(OutboxTransactionFactory)
        );

        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Singleton
            && service.ServiceType == typeof(IOutboxTrigger)
            && service.ImplementationType == typeof(OutboxTrigger)
        );

        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Transient
            && service.ServiceType == typeof(IOutboxPublisher)
            && service.ImplementationType == typeof(DummyOutboxPublisher)
        );

        Assert.Contains(serviceCollection, service
            => service.Lifetime is ServiceLifetime.Singleton
            && service.ServiceType == typeof(IHostedService)
            && service.ImplementationType == typeof(OutboxProcessorBackgroundService)
        );
    }

    [Fact]
    public void ConfigureOutbox_WhenModelBuilderIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        ModelBuilder modelBuilder = null!;

        // Act 
        void Action()
            => modelBuilder.ConfigureOutbox();

        // Assert
        Assert.Throws<ArgumentNullException>(Action);
    }

    [Fact]
    public void ConfigureOutbox_WhenModelBuilderIsNotNull_ShouldConfigureOutbox()
    {
        // Arrange
        var modelBuilder = new ModelBuilder(new ConventionSet());

        // Act
        modelBuilder.ConfigureOutbox();

        // Assert
        var entityTypes = modelBuilder.Model
            .GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .ToList();

        Assert.Contains(typeof(OutboxLock), entityTypes);
        Assert.Contains(typeof(OutboxMessage), entityTypes);
    }

    private sealed class DummyOutboxPublisher : IOutboxPublisher
    {
        public Task PublishAsync(string messageType, string payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [OutboxMessageType("DUMMY-MESSAGE")]
    public sealed class DummyOutboxMessage;
}
