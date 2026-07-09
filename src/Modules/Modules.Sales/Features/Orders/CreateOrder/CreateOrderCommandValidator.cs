using FluentValidation;

namespace Modules.Sales.Features.Orders.CreateOrder;

/// <summary>
/// The client (POS cart) computes the money breakdown; this validator
/// re-derives every figure and rejects any drift. Rounding is half-up to
/// 2 decimals, mirrored exactly by roundMoney() in the Angular cart.
/// </summary>
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

        RuleFor(c => c.Subtotal)
            .Must((command, subtotal) => subtotal == command.Items.Sum(i => i.Quantity * i.UnitPrice))
            .When(c => c.Items is { Count: > 0 })
            .WithMessage("Subtotal must equal the sum of Quantity * UnitPrice across all items.");

        RuleFor(c => c.DiscountPercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("DiscountPercent must be between 0 and 100.");

        RuleFor(c => c.DiscountAmount)
            .Must((c, discount) => discount == RoundMoney(c.Subtotal * c.DiscountPercent / 100m))
            .WithMessage("DiscountAmount must equal Subtotal * DiscountPercent / 100, rounded half-up to 2 decimals.");

        RuleFor(c => c.TaxRatePercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("TaxRatePercent must be between 0 and 100.");

        RuleFor(c => c.TaxAmount)
            .Must((c, tax) => tax == RoundMoney((c.Subtotal - c.DiscountAmount) * c.TaxRatePercent / 100m))
            .WithMessage("TaxAmount must equal (Subtotal - DiscountAmount) * TaxRatePercent / 100, rounded half-up to 2 decimals.");

        RuleFor(c => c.TotalAmount)
            .GreaterThan(0)
            .WithMessage("TotalAmount must be positive.");

        RuleFor(c => c.TotalAmount)
            .Must((c, total) => total == c.Subtotal - c.DiscountAmount + c.TaxAmount)
            .WithMessage("TotalAmount must equal Subtotal - DiscountAmount + TaxAmount.");

        RuleFor(c => c.PaymentMethod)
            .IsInEnum()
            .WithMessage("PaymentMethod must be Cash or Card.");

        When(c => c.PaymentMethod == PaymentMethod.Cash, () =>
        {
            RuleFor(c => c.CashTendered)
                .NotNull()
                .WithMessage("CashTendered is required for cash payments.")
                .GreaterThanOrEqualTo(c => c.TotalAmount)
                .WithMessage("CashTendered must cover the total.");

            RuleFor(c => c.ChangeDue)
                .NotNull()
                .WithMessage("ChangeDue is required for cash payments.")
                .Must((c, change) => change == c.CashTendered - c.TotalAmount)
                .When(c => c.CashTendered is not null, ApplyConditionTo.CurrentValidator)
                .WithMessage("ChangeDue must equal CashTendered - TotalAmount.");
        });

        When(c => c.PaymentMethod == PaymentMethod.Card, () =>
        {
            RuleFor(c => c.CashTendered)
                .Null()
                .WithMessage("CashTendered must be null for card payments.");

            RuleFor(c => c.ChangeDue)
                .Null()
                .WithMessage("ChangeDue must be null for card payments.");
        });
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
