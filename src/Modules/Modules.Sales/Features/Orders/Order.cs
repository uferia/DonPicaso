namespace Modules.Sales.Features.Orders;

/// <summary>
/// Aggregate root for a placed order. Owned by the Orders feature area and
/// shared by its vertical slices (CreateOrder today, GetOrder/RefundOrder later).
/// </summary>
public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }

    /// <summary>
    /// Device-generated idempotency key; unique across all orders so offline
    /// replays of an already-received order are detected instead of duplicated.
    /// </summary>
    public Guid ClientOrderId { get; private set; }

    public Guid BranchId { get; private set; }

    public Guid BrandId { get; private set; }

    public decimal TotalAmount { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order()
    {
        // EF Core materialization.
    }

    public static Order Create(
        Guid clientOrderId,
        Guid branchId,
        Guid brandId,
        decimal totalAmount,
        IEnumerable<OrderItem> items,
        DateTimeOffset createdAtUtc)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            ClientOrderId = clientOrderId,
            BranchId = branchId,
            BrandId = brandId,
            TotalAmount = totalAmount,
            CreatedAtUtc = createdAtUtc,
        };

        order._items.AddRange(items);
        return order;
    }
}
