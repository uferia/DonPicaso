namespace Modules.Sales.Features.Orders.CreateOrder;

/// <summary>
/// Payload posted by the POS tablet (directly when online, or replayed from
/// IndexedDB by the offline sync service after reconnection).
/// <see cref="ClientOrderId"/> is generated on the device and acts as an
/// idempotency key: replaying the same order after a lost response must not
/// create a duplicate.
/// </summary>
public sealed record CreateOrderCommand(
    Guid ClientOrderId,
    Guid BranchId,
    Guid BrandId,
    decimal Subtotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxRatePercent,
    decimal TaxAmount,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    decimal? CashTendered,
    decimal? ChangeDue,
    IReadOnlyList<OrderItemDto> Items);

public sealed record OrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);
