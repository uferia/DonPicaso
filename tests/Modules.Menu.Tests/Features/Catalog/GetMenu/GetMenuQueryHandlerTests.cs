using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Menu;
using Modules.Menu.Features.Catalog;
using Modules.Menu.Features.Catalog.GetMenu;
using Modules.Menu.Persistence;

namespace Modules.Menu.Tests.Features.Catalog.GetMenu;

[TestClass]
public sealed class GetMenuQueryHandlerTests
{
    private MenuDbContext _dbContext = null!;
    private GetMenuQueryHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<MenuDbContext>()
            .UseInMemoryDatabase($"menu-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new MenuDbContext(options);
        _handler = new GetMenuQueryHandler(_dbContext, new MenuOptions(TaxRatePercent: 1.5m, CurrencyCode: "PHP"));
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_ReturnsActiveCategoriesAndProductsForBrandInDisplayOrder()
    {
        var brandId = Guid.NewGuid();

        var snacks = Category.Create(brandId, "Snacks", displayOrder: 2);
        var coffee = Category.Create(brandId, "Coffee", displayOrder: 1);
        var espresso = Product.Create(brandId, coffee.Id, "Espresso", 2.50m, displayOrder: 2);
        var latte = Product.Create(brandId, coffee.Id, "Caffe Latte", 4.25m, displayOrder: 1);

        _dbContext.Categories.AddRange(snacks, coffee);
        _dbContext.Products.AddRange(espresso, latte);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brandId, CancellationToken.None);

        result.TaxRatePercent.Should().Be(1.5m);
        result.CurrencyCode.Should().Be("PHP");
        result.Categories.Select(c => c.Name).Should().ContainInOrder("Coffee", "Snacks");
        result.Categories[0].Products.Select(p => p.Name).Should().ContainInOrder("Caffe Latte", "Espresso");
        result.Categories[0].Products[1].Price.Should().Be(2.50m);
        result.Categories[1].Products.Should().BeEmpty();
    }

    [TestMethod]
    public async Task HandleAsync_ExcludesInactiveEntriesAndOtherBrands()
    {
        var brandId = Guid.NewGuid();
        var otherBrandId = Guid.NewGuid();

        var coffee = Category.Create(brandId, "Coffee", 1);
        var inactiveCategory = Category.Create(brandId, "Retired Section", 2);
        typeof(Category).GetProperty(nameof(Category.IsActive))!.SetValue(inactiveCategory, false);

        var activeProduct = Product.Create(brandId, coffee.Id, "Espresso", 2.50m, 1);
        var inactiveProduct = Product.Create(brandId, coffee.Id, "Retired Drink", 1.00m, 2);
        typeof(Product).GetProperty(nameof(Product.IsActive))!.SetValue(inactiveProduct, false);

        var foreignCategory = Category.Create(otherBrandId, "Foreign Menu", 1);

        _dbContext.Categories.AddRange(coffee, inactiveCategory, foreignCategory);
        _dbContext.Products.AddRange(activeProduct, inactiveProduct);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brandId, CancellationToken.None);

        result.Categories.Should().HaveCount(1);
        result.Categories[0].Name.Should().Be("Coffee");
        result.Categories[0].Products.Should().HaveCount(1);
        result.Categories[0].Products[0].Name.Should().Be("Espresso");
    }
}
