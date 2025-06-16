using System.Text.Json;
using Dapper;
using Npgsql;
using Outbox.Api;
using Outbox.Api.Models;
using Outbox.Contracts;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
    configuration.WriteTo.Console();
});

var services = builder.Services;

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
services.AddSingleton<DatabaseInitializer>();

services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database");
    return new NpgsqlDataSourceBuilder(connectionString).Build();
});

var application = builder.Build();

var initializer = application.Services.GetRequiredService<DatabaseInitializer>();
await initializer.Execute();

application.UseSwagger();
application.UseSwaggerUI();

application.MapPost("orders", async (CreateOrderDto orderDto, NpgsqlDataSource dataSource) =>
{
    var order = new Order(
        Id: Guid.NewGuid(),
        orderDto.CustomerName,
        orderDto.ProductName,
        orderDto.Quantity,
        orderDto.TotalPrice,
        OrderDate: DateTime.UtcNow);

    const string sql =
        """
        INSERT INTO orders (id, customer_name, product_name, quantity, total_price, order_date)
        VALUES (@Id, @CustomerName, @ProductName, @Quantity, @TotalPrice, @OrderDate);
        """;

    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    await connection.ExecuteAsync(sql, order, transaction: transaction);

    var orderCreatedEvent = new OrderCreatedIntegrationEvent(order.Id);

    await InsertOutboxMessage(orderCreatedEvent, connection, transaction);

    await transaction.CommitAsync();

    return Results.Created($"orders/{order.Id}", order);

    async Task InsertOutboxMessage(
        OrderCreatedIntegrationEvent message,
        NpgsqlConnection theConnection,
        NpgsqlTransaction theTransaction)
    {
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = message.GetType().FullName!,
            Content = JsonSerializer.Serialize(message),
            OccurredOnUtc = DateTime.UtcNow
        };

        const string theSql =
            """
            INSERT INTO outbox_messages (id, type, content, occurred_on_utc)
            VALUES (@Id, @Type, @Content::jsonb, @OccurredOnUtc);
            """;

        await theConnection.ExecuteAsync(theSql, outboxMessage, theTransaction);
    }
});

application.MapGet("orders/{id:guid}", async (Guid id, NpgsqlDataSource dataSource) =>
{
    const string sql = "SELECT * FROM orders WHERE Id = @Id";

    await using var connection = await dataSource.OpenConnectionAsync();
    var order = await connection.QuerySingleOrDefaultAsync<Order>(sql, new { Id = id });

    return order is null ? Results.NotFound() : Results.Ok(order);
});

application.Run();
