# Outbox pattern

## Prerequisites

- [↑ .NET 9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- [↑ Docker Desktop](https://docs.docker.com/desktop/)

## Run infrastructure

```bash
docker compose --file infrastructure.yaml up --detach
```

```bash
docker compose --file infrastructure.yaml down
```

## Run projects

Run API project:

```bash
dotnet run --project src/Outbox.Api
```

Run processor project:

```bash
dotnet run --project src/Outbox.Processor
```
