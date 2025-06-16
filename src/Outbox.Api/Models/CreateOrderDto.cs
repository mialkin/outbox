namespace Outbox.Api.Models;

public record CreateOrderDto(string CustomerName, string ProductName, int Quantity, decimal TotalPrice);
