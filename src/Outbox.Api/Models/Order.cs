namespace Outbox.Api.Models;

public record Order(
    Guid Id,
    string CustomerName,
    string ProductName,
    int Quantity,
    decimal TotalPrice,
    DateTime OrderDate);
