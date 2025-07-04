using System.Text.Json;
using Dapper;
using Npgsql;
using NpgsqlTypes;
using Outbox.Contracts;

namespace Outbox.Processor;

public class DatabaseInitializer(
    NpgsqlDataSource dataSource,
    IConfiguration configuration,
    ILogger<DatabaseInitializer> logger)
{
    public async Task Execute()
    {
        logger.LogInformation("Start database initialization");

        await EnsureDatabaseExists();
        await InitializeDatabase();
        await SeedInitialData();

        logger.LogInformation("Success of database initialization");
    }

    private async Task EnsureDatabaseExists()
    {
        var connectionString = configuration.GetConnectionString("Database")!;
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        builder.Database = "postgres"; // Connect to the default 'postgres' database

        await using var connection = new NpgsqlConnection(builder.ToString());
        await connection.OpenAsync();

        var databaseExists = await connection.ExecuteScalarAsync<bool>(
            sql: "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @databaseName)",
            param: new { databaseName });

        if (!databaseExists)
        {
            logger.LogInformation("Creating database {DatabaseName}", databaseName);
            await connection.ExecuteAsync($"CREATE DATABASE {databaseName}");
        }
    }

    private async Task InitializeDatabase()
    {
        const string sql =
            """
            -- Create outbox_messages table if it doesn't exist
            CREATE TABLE IF NOT EXISTS public.outbox_messages (
                id UUID PRIMARY KEY,
                type VARCHAR(255) NOT NULL,
                content JSONB NOT NULL,
                occurred_on_utc TIMESTAMP WITH TIME ZONE NOT NULL,
                processed_on_utc TIMESTAMP WITH TIME ZONE NULL,
                error TEXT NULL
            );

            -- Create a filtered index on unprocessed messages, including all necessary columns
            CREATE INDEX IF NOT EXISTS idx_outbox_messages_unprocessed
            ON public.outbox_messages (occurred_on_utc, processed_on_utc)
            INCLUDE (id, type, content)
            WHERE processed_on_utc IS NULL;
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync(sql);
    }

    private async Task SeedInitialData()
    {
        await using var connection = await dataSource.OpenConnectionAsync();

        logger.LogInformation("Deleting existing records from outbox_messages table");
        await connection.ExecuteAsync("TRUNCATE TABLE public.outbox_messages");

        logger.LogInformation("Seeding 2 million records to outbox_messages table");

        const int batchSize = 500_000;
        const int totalRecords = 2_000_000;

        await using var writer = await connection.BeginBinaryImportAsync(
            "COPY public.outbox_messages (id, type, content, occurred_on_utc) FROM STDIN (FORMAT BINARY)");

        for (var i = 0; i < totalRecords; i++)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(Guid.NewGuid(), NpgsqlDbType.Uuid);
            await writer.WriteAsync(typeof(OrderCreatedIntegrationEvent).FullName, NpgsqlDbType.Varchar);
            await writer.WriteAsync(
                JsonSerializer.Serialize(new OrderCreatedIntegrationEvent(Guid.NewGuid())), NpgsqlDbType.Jsonb);
            await writer.WriteAsync(DateTime.UtcNow, NpgsqlDbType.TimestampTz);

            if ((i + 1) % batchSize == 0)
            {
                logger.LogInformation("Inserted {Count} records", i + 1);
            }
        }

        await writer.CompleteAsync();

        logger.LogInformation("Success seeding 2 million records to outbox_messages table");
    }
}
