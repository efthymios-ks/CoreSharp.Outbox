# CoreSharp.Outbox

[![Nuget](https://img.shields.io/nuget/v/CoreSharp.Outbox)](https://www.nuget.org/packages/CoreSharp.Outbox/)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=efthymios-ks_CoreSharp.Outbox&metric=coverage)](https://sonarcloud.io/summary/new_code?id=efthymios-ks_CoreSharp.Outbox)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=efthymios-ks_CoreSharp.Outbox&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=efthymios-ks_CoreSharp.Outbox)
![GitHub License](https://img.shields.io/github/license/efthymios-ks/CoreSharp.Outbox)

> Outbox pattern implemented with EF Core

## Features

- Queues outbox messages in DB using EF Core
- Processes messages **one by one** in the order they were enqueued.
- Marks messages as processed on success
- Marks messages with error and logs on failure
- Processes all messages sequentially without skipping

## Installation

- Install the package with [Nuget](https://www.nuget.org/packages/CoreSharp.Outbox/).
- Or via CLI

```
dotnet add package CoreSharp.Outbox
```

## Setup

### 1. Implement your publisher

```CSharp
// Omitting Azure Service Bus configuration for simplicity
public sealed class MyAppOutboxPublisher(
    ServiceBusClient serviceBusClient
    ) : IOutboxPublisher
{
    private readonly ServiceBusClient _serviceBusClient = serviceBusClient;

    public Task PublishAsync(
        string messageType,
        string payload,
        CancellationToken cancellationToken = default
    )
    {
        var sender = client.CreateSender("MY_APP_QUEUE");
        var message = new ServiceBusMessage(payload)
        {
            ContentType = MediaTypeNames.Application.Json,
            Subject = messageType // Optional, but may help in routing/filters
        };

        message.ApplicationProperties["MessageType"] = messageType;
        await sender.SendMessageAsync(message);
    }
}
```

### 2. Configure DI

```CSharp
// 1. Register your app DbContext as usual
services.AddDbContext<MyAppDbContext>();

// 2. Register outbox services
services.AddOutbox<MyAppDbContext, MyAppOutboxPublisher>();
```

### 3. Configure DbContext

```CSharp
public sealed class MyAppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure outbox
        modelBuilder.ConfigureOutbox();
    }
}
```

### 4. Migrations

- After configuration, run EF Core migrations (`Add-Migration`, `Update-Database` etc.)

## Use cases

```CSharps
[OutboxMessageType("send_email_v1")]
public sealed class SendEmailOutboxMessage
{
    public required string Sender { get; init; }
    public required string Recipient { get; init; }
    public required string Body { get; init; }
}
```

```CSharp
public sealed class PurchaseEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder endpointRouteBuilder)
        => endpointRouteBuilder.MapPost("api/purchases", HandleAsync);

    private static async Task<IResult> HandleAsync(
        PurchaseDto purchaseDto,
        ShopDbContext dbContext,
        IOutboxTransactionFactory outboxTransactionFactory
    )
    {
        // 1. Start transaction via outbox
        await using var outboxTransaction = await outboxTransactionFactory.CreateAsync(dbContext);

        // 2. Process your business flow as usual
        var purchase = awat PurchaseAsync(purchaseDto);

        // Of course your business involves some DbContext operations
        await dbContext.Purchases.AddAsync(purchase);

        // 3. Add as many outbox messages as needed
        await outboxTransaction.AddMessageAsync(new SendEmailOutboxMessage
        {
            Sender = "sender@mail.com",
            Recipient = "recipient@mail.com",
            Body = "Your purchase is complete!"
        });

        // 4. Save DbContext
        await dbContext.SaveChangesAsync();

        // 5. Commit outbox transaction
        await outboxTransaction.CommitAsync();

        return Results.Ok();
    }
}
```
