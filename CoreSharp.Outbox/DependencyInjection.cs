using CoreSharp.Outbox.Locks;
using CoreSharp.Outbox.Processors;
using CoreSharp.Outbox.Registry;
using CoreSharp.Outbox.Repositories;
using CoreSharp.Outbox.Transactions;
using CoreSharp.Outbox.Triggers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace CoreSharp.Outbox;

public static class DependencyInjection
{
    public static IServiceCollection AddOutbox<TDbContext, TOutboxPublisher>(
        this IServiceCollection services,
        IEnumerable<Assembly>? scanAssemblies = null
    )
        where TDbContext : DbContext
        where TOutboxPublisher : class, IOutboxPublisher
    {
        ArgumentNullException.ThrowIfNull(services);

        scanAssemblies ??= [Assembly.GetEntryAssembly()!];

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddKeyedSingleton<Func<DbContext>>(Constants.Domain, (serviceProvider, _) => () =>
        {
            // Make sure to dispose the DbContext after use
            var serviceScope = serviceProvider.CreateScope();
            return serviceScope.ServiceProvider.GetRequiredService<TDbContext>();
        });

        services.TryAddSingleton<IOutboxDistributedLockFactory, OutboxDistributedLockFactory>();
        services.TryAddSingleton<IOutboxProcessor, OutboxProcessor>();
        services.TryAddSingleton<IOutboxMessageRegistry>(new OutboxMessageRegistry(GetOutboxPayloads(scanAssemblies)));
        services.TryAddSingleton<IOutboxMessageRepository, OutboxMessageRepository>();
        services.TryAddScoped<IOutboxTransactionFactory, OutboxTransactionFactory>();
        services.TryAddSingleton<IOutboxTrigger, OutboxTrigger>();
        services.TryAddTransient<IOutboxPublisher, TOutboxPublisher>();
        services.AddHostedService<OutboxProcessorBackgroundService>();

        return services;
    }

    public static ModelBuilder ConfigureOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxLock>();
        modelBuilder.Entity<OutboxMessage>();

        return modelBuilder;
    }

    private static Dictionary<Type, string> GetOutboxPayloads(IEnumerable<Assembly> assemblies)
    {
        var payloads = new Dictionary<Type, string>();
        foreach (var type in assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract)
        )
        {
            var messageTypeKey = type
                .GetCustomAttribute<OutboxMessageTypeAttribute>()?
                .Type;

            if (messageTypeKey is null)
            {
                continue;
            }

            payloads[type] = messageTypeKey;
        }

        return payloads;
    }
}
