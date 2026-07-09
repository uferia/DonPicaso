using FluentAssertions;
using Modules.Sales.Features.Orders;
using Modules.Sales.Features.Orders.CreateOrder;

namespace Modules.Sales.Tests.Features.Orders.CreateOrder;

[TestClass]
public sealed class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator = new();

    // 2 × 12.50 + 1 × 10.00 = 35.00 subtotal; 10% discount = 3.50;
    // 1.5% tax on 31.50 = 0.4725 -> 0.47 (half-up); total 31.97;
    // cash 40.00 tendered -> 8.03 change.
    private static CreateOrderCommand BuildValidCashCommand() =>
        new(
            ClientOrderId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            BrandId: Guid.NewGuid(),
            Subtotal: 35.00m,
            DiscountPercent: 10m,
            DiscountAmount: 3.50m,
            TaxRatePercent: 1.5m,
            TaxAmount: 0.47m,
            TotalAmount: 31.97m,
            PaymentMethod: PaymentMethod.Cash,
            CashTendered: 40.00m,
            ChangeDue: 8.03m,
            Items:
            [
                new OrderItemDto(Guid.NewGuid(), "Margherita Pizza", Quantity: 2, UnitPrice: 12.50m),
                new OrderItemDto(Guid.NewGuid(), "Tiramisu", Quantity: 1, UnitPrice: 10.00m),
            ]);

    [TestMethod]
    public void Validate_WithConsistentCashCommand_Passes()
    {
        _validator.Validate(BuildValidCashCommand()).IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void Validate_WithConsistentCardCommand_Passes()
    {
        var command = BuildValidCashCommand() with
        {
            PaymentMethod = PaymentMethod.Card,
            CashTendered = null,
            ChangeDue = null,
        };

        _validator.Validate(command).IsValid.Should().BeTrue();
    }

    [TestMethod]
    public void Validate_RoundsDiscountHalfUp()
    {
        // 1 × 12.35 subtotal; 10% discount = 1.235 -> 1.24 (half-up);
        // 1.5% tax on 11.11 = 0.16665 -> 0.17; total 11.28.
        var command = BuildValidCashCommand() with
        {
            Subtotal = 12.35m,
            DiscountPercent = 10m,
            DiscountAmount = 1.24m,
            TaxAmount = 0.17m,
            TotalAmount = 11.28m,
            CashTendered = 20.00m,
            ChangeDue = 8.72m,
            Items = [new OrderItemDto(Guid.NewGuid(), "Combo Plate", Quantity: 1, UnitPrice: 12.35m)],
        };

        _validator.Validate(command).IsValid.Should().BeTrue();

        var truncatedInsteadOfRounded = command with { DiscountAmount = 1.23m };
        _validator.Validate(truncatedInsteadOfRounded).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.DiscountAmount));
    }

    [TestMethod]
    public void Validate_WhenSubtotalDoesNotMatchItems_Fails()
    {
        var command = BuildValidCashCommand() with { Subtotal = 99.99m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.Subtotal));
    }

    [TestMethod]
    public void Validate_WhenTaxAmountIsWrong_Fails()
    {
        var command = BuildValidCashCommand() with { TaxAmount = 1.00m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.TaxAmount));
    }

    [TestMethod]
    public void Validate_WhenTotalIsNotSubtotalMinusDiscountPlusTax_Fails()
    {
        var command = BuildValidCashCommand() with { TotalAmount = 35.00m, CashTendered = 40.00m, ChangeDue = 5.00m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.TotalAmount));
    }

    [TestMethod]
    public void Validate_CashWithoutTenderedAmount_Fails()
    {
        var command = BuildValidCashCommand() with { CashTendered = null, ChangeDue = null };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.CashTendered));
    }

    [TestMethod]
    public void Validate_CashTenderedBelowTotal_Fails()
    {
        var command = BuildValidCashCommand() with { CashTendered = 30.00m, ChangeDue = -1.97m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.CashTendered));
    }

    [TestMethod]
    public void Validate_CashWithWrongChange_Fails()
    {
        var command = BuildValidCashCommand() with { ChangeDue = 9.00m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.ChangeDue));
    }

    [TestMethod]
    public void Validate_CardWithCashFields_Fails()
    {
        var command = BuildValidCashCommand() with { PaymentMethod = PaymentMethod.Card };

        var errors = _validator.Validate(command).Errors.Select(e => e.PropertyName);

        errors.Should().Contain(nameof(CreateOrderCommand.CashTendered));
        errors.Should().Contain(nameof(CreateOrderCommand.ChangeDue));
    }

    [TestMethod]
    public void Validate_DiscountPercentOutOfRange_Fails()
    {
        var command = BuildValidCashCommand() with { DiscountPercent = 101m };

        _validator.Validate(command).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.DiscountPercent));
    }
}
