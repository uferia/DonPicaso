using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Sales.Features.Orders;
using Modules.Sales.Features.Orders.CreateOrder;
using Modules.Sales.Persistence;
using Moq;

namespace Modules.Sales.Tests.Features.Orders.CreateOrder;

[TestClass]
public sealed class CreateOrderCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);

    private SalesDbContext _dbContext = null!;
    private Mock<TimeProvider> _timeProviderMock = null!;
    private CreateOrderCommandHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        // A brand-new database name per test guarantees full isolation:
        // no seed data, no state leaking between tests.
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new SalesDbContext(options);

        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        _handler = new CreateOrderCommandHandler(
            _dbContext,
            new CreateOrderCommandValidator(),
            _timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithValidCommand_PersistsOrderAndReturnsItsId()
    {
        var command = BuildValidCommand();

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        result.OrderId.Should().NotBeEmpty();
        result.WasAlreadyProcessed.Should().BeFalse();

        var savedOrder = await _dbContext.Orders
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == result.OrderId);

        savedOrder.ClientOrderId.Should().Be(command.ClientOrderId);
        savedOrder.BranchId.Should().Be(command.BranchId);
        savedOrder.BrandId.Should().Be(command.BrandId);
        savedOrder.Subtotal.Should().Be(35.00m);
        savedOrder.DiscountPercent.Should().Be(10m);
        savedOrder.DiscountAmount.Should().Be(3.50m);
        savedOrder.TaxRatePercent.Should().Be(1.5m);
        savedOrder.TaxAmount.Should().Be(0.47m);
        savedOrder.TotalAmount.Should().Be(31.97m);
        savedOrder.PaymentMethod.Should().Be(PaymentMethod.Cash);
        savedOrder.CashTendered.Should().Be(40.00m);
        savedOrder.ChangeDue.Should().Be(8.03m);
        savedOrder.Items.Sum(i => i.LineTotal).Should().Be(savedOrder.Subtotal);
        savedOrder.CreatedAtUtc.Should().Be(FixedUtcNow);
        savedOrder.Items.Should().HaveCount(2);

        _timeProviderMock.Verify(t => t.GetUtcNow(), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_WhenSameClientOrderIdIsReplayed_ReturnsOriginalIdWithoutDuplicating()
    {
        var command = BuildValidCommand();

        var first = await _handler.HandleAsync(command, CancellationToken.None);
        var replay = await _handler.HandleAsync(command, CancellationToken.None);

        replay.OrderId.Should().Be(first.OrderId);
        replay.WasAlreadyProcessed.Should().BeTrue();
        first.WasAlreadyProcessed.Should().BeFalse();

        (await _dbContext.Orders.CountAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task HandleAsync_WhenSubtotalDoesNotMatchItems_ThrowsValidationExceptionAndSavesNothing()
    {
        var command = BuildValidCommand() with { Subtotal = 99.99m };

        var act = () => _handler.HandleAsync(command, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.Subtotal));

        (await _dbContext.Orders.AnyAsync()).Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WithEmptyBranchAndNonPositiveQuantity_ThrowsValidationException()
    {
        var command = BuildValidCommand() with
        {
            BranchId = Guid.Empty,
            Items = [new OrderItemDto(Guid.NewGuid(), "Quattro Formaggi", Quantity: 0, UnitPrice: 10.00m)],
        };

        var act = () => _handler.HandleAsync(command, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Select(e => e.PropertyName).Should().Contain(
            [
                nameof(CreateOrderCommand.BranchId),
                "Items[0].Quantity",
            ]);

        (await _dbContext.Orders.AnyAsync()).Should().BeFalse();
    }

    private static CreateOrderCommand BuildValidCommand() =>
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
}
