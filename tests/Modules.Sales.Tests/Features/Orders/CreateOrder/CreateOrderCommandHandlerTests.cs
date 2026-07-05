using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
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

        var orderId = await _handler.HandleAsync(command, CancellationToken.None);

        orderId.Should().NotBeEmpty();

        var savedOrder = await _dbContext.Orders
            .Include(o => o.Items)
            .SingleAsync(o => o.Id == orderId);

        savedOrder.BranchId.Should().Be(command.BranchId);
        savedOrder.BrandId.Should().Be(command.BrandId);
        savedOrder.TotalAmount.Should().Be(35.00m);
        savedOrder.CreatedAtUtc.Should().Be(FixedUtcNow);
        savedOrder.Items.Should().HaveCount(2);
        savedOrder.Items.Sum(i => i.LineTotal).Should().Be(savedOrder.TotalAmount);

        _timeProviderMock.Verify(t => t.GetUtcNow(), Times.Once);
    }

    [TestMethod]
    public async Task HandleAsync_WhenTotalDoesNotMatchItems_ThrowsValidationExceptionAndSavesNothing()
    {
        var command = BuildValidCommand() with { TotalAmount = 99.99m };

        var act = () => _handler.HandleAsync(command, CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().Contain(e => e.PropertyName == nameof(CreateOrderCommand.TotalAmount));

        (await _dbContext.Orders.AnyAsync()).Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WithEmptyBranchAndNonPositiveQuantity_ThrowsValidationException()
    {
        var command = new CreateOrderCommand(
            BranchId: Guid.Empty,
            BrandId: Guid.NewGuid(),
            TotalAmount: 10.00m,
            Items: [new OrderItemDto(Guid.NewGuid(), "Quattro Formaggi", Quantity: 0, UnitPrice: 10.00m)]);

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
            BranchId: Guid.NewGuid(),
            BrandId: Guid.NewGuid(),
            TotalAmount: 35.00m,
            Items:
            [
                new OrderItemDto(Guid.NewGuid(), "Margherita Pizza", Quantity: 2, UnitPrice: 12.50m),
                new OrderItemDto(Guid.NewGuid(), "Tiramisu", Quantity: 1, UnitPrice: 10.00m),
            ]);
}
