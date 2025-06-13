namespace Outbox.Contracts;

public sealed record OrderCreatedIntegrationEvent(Guid OrderId);
