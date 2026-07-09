using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Menu.Persistence;

namespace Modules.Menu.Tests.Persistence;

[TestClass]
public sealed class MenuSeederTests
{
    private MenuDbContext _dbContext = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<MenuDbContext>()
            .UseInMemoryDatabase($"menu-seeder-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new MenuDbContext(options);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task SeedAsync_PopulatesCategoriesAndProductsForTheBrand()
    {
        var brandId = Guid.NewGuid();

        await MenuSeeder.SeedAsync(_dbContext, brandId);

        var categories = await _dbContext.Categories.ToListAsync();
        var products = await _dbContext.Products.ToListAsync();

        categories.Should().NotBeEmpty();
        categories.Should().OnlyContain(c => c.BrandId == brandId && c.IsActive);
        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.BrandId == brandId && p.IsActive && p.Price > 0);
        products.Select(p => p.CategoryId).Distinct()
            .Should().BeSubsetOf(categories.Select(c => c.Id));
    }

    [TestMethod]
    public async Task SeedAsync_IsIdempotent()
    {
        var brandId = Guid.NewGuid();

        await MenuSeeder.SeedAsync(_dbContext, brandId);
        var countAfterFirstRun = await _dbContext.Products.CountAsync();

        await MenuSeeder.SeedAsync(_dbContext, brandId);

        (await _dbContext.Products.CountAsync()).Should().Be(countAfterFirstRun);
    }
}
