using FluentValidation;
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
    public async Task<Guid> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var order = Order.Create(
            command.BranchId,
            command.BrandId,
            command.TotalAmount,
            command.Items.Select(i => OrderItem.Create(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)),
            timeProvider.GetUtcNow());

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}
