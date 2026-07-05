using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Sales.Persistence;

namespace Modules.Sales.Features.Orders.CreateOrder;

/// <summary>
/// CQRS write handler for order creation. Validates defensively so that
/// non-HTTP callers (e.g. in-process replays) get the same guarantees as
/// the Minimal API endpoint.
/// </summary>
public sealed class CreateOrderCommandHandler(
    SalesDbContext dbContext,
    IValidator<CreateOrderCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<CreateOrderResult> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        // Idempotency: the offline sync service may replay an order whose
        // response was lost. If the key is already known, acknowledge with the
        // original order instead of inserting a duplicate. The unique index on
        // client_order_id backstops the rare concurrent-replay race.
        var existingOrderId = await dbContext.Orders
            .Where(o => o.ClientOrderId == command.ClientOrderId)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingOrderId is not null)
        {
            return new CreateOrderResult(existingOrderId.Value, WasAlreadyProcessed: true);
        }

        var order = Order.Create(
            command.ClientOrderId,
            command.BranchId,
            command.BrandId,
            command.TotalAmount,
            command.Items.Select(i => OrderItem.Create(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)),
            timeProvider.GetUtcNow());

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult(order.Id, WasAlreadyProcessed: false);
    }
}

public sealed record CreateOrderResult(Guid OrderId, bool WasAlreadyProcessed);
