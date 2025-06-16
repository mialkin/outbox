using Npgsql;
using Outbox.Processor;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
    configuration.WriteTo.Console();
});

var services = builder.Services;

services.AddSingleton<DatabaseInitializer>();

builder.Services.AddHostedService<OutboxBackgroundService>();
builder.Services.AddScoped<OutboxProcessor>();

services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Database");
    return new NpgsqlDataSourceBuilder(connectionString).Build();
});

var application = builder.Build();

var initializer = application.Services.GetRequiredService<DatabaseInitializer>();
// await initializer.Execute();

application.MapGet("/", () => "Hello World!");

application.Run();
