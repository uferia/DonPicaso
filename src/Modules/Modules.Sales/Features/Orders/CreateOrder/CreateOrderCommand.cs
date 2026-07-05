namespace Modules.Sales.Features.Orders.CreateOrder;

/// <summary>
/// Payload posted by the POS tablet (directly when online, or replayed from
/// IndexedDB by the offline sync service after reconnection).
/// </summary>
public sealed record CreateOrderCommand(
    Guid BranchId,
    Guid BrandId,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
