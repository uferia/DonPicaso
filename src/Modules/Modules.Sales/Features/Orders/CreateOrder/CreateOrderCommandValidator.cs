using FluentValidation;

namespace Modules.Sales.Features.Orders.CreateOrder;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(c => c.ClientOrderId)
            .NotEmpty()
            .WithMessage("ClientOrderId is required (device-generated idempotency key).");

        RuleFor(c => c.BranchId)
            .NotEmpty()
            .WithMessage("BranchId is required.");

        RuleFor(c => c.BrandId)
            .NotEmpty()
            .WithMessage("BrandId is required.");

        RuleFor(c => c.Items)
            .NotEmpty()
            .WithMessage("An order must contain at least one item.");

        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId)
                .NotEmpty()
                .WithMessage("ProductId is required.");

            item.RuleFor(i => i.ProductName)
                .NotEmpty()
                .MaximumLength(200);

            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .WithMessage("Quantity must be a positive number.");

            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("UnitPrice cannot be negative.");
        });

        RuleFor(c => c.TotalAmount)
            .GreaterThan(0)
            .WithMessage("TotalAmount must be positive.");

        RuleFor(c => c.TotalAmount)
            .Must((command, total) => total == command.Items.Sum(i => i.Quantity * i.UnitPrice))
            .When(c => c.Items is { Count: > 0 })
            .WithMessage("TotalAmount must equal the sum of Quantity * UnitPrice across all items.");
    }
}
