using CoreSharp.Outbox;
using CoreSharp.Outbox.Transactions;
using DemoApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;

// Setup
using var host = await ConfigureHostAsync();
var services = host.Services;
var dbContext = services.GetRequiredService<ShopDbContext>();
await dbContext.Database.EnsureCreatedAsync();

// Start outbox transaction
var outboxTransactionFactory = services.GetRequiredService<IOutboxTransactionFactory>();
var outboxTransaction = await outboxTransactionFactory.CreateAsync(dbContext);

// Business processing
await dbContext.Purchases.AddAsync(new());

// Enqueue outbox messages
await outboxTransaction.AddMessageAsync(new SendEmailOutboxMessage
{
    Sender = "sender@mail.com",
    Recipient = "recipient@mail.com",
    Body = "Your purchase was successful!"
});

// Save and commit
await dbContext.SaveChangesAsync();
await outboxTransaction.CommitAsync();

// Since we are not in WebApi project, we need to start the outbox processor manually
await host.RunAsync();
Console.ReadLine();

static async Task<IHost> ConfigureHostAsync()
{
    // Run Docker MS SQL server
    var sqlContainer = new MsSqlBuilder().Build();
    await sqlContainer.StartAsync();

    // Configure DI
    return Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddDbContext<ShopDbContext>(options =>
            {
                options.UseSqlServer(sqlContainer.GetConnectionString());
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            });

            services.AddOutbox<ShopDbContext, DemoAppOutboxPublisher>();
        })
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddFilter("Microsoft", LogLevel.Warning);
        })
        .Build();
}
