namespace Modules.Sales.Features.Orders;

/// <summary>
/// How the customer settled the order. "Card" records the method only —
/// actual card processing is explicitly out of scope.
/// </summary>
public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
}
