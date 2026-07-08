using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Brands.ListBrands;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Brands.ListBrands;

[TestClass]
public sealed class ListBrandsQueryHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private ListBrandsQueryHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new ListBrandsQueryHandler(_dbContext);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_ReturnsAllBrandsOrderedByName_IncludingDeactivatedOnes()
    {
        var zed = Brand.Create("Zed Brand", FixedUtcNow);
        var alpha = Brand.Create("Alpha Brand", FixedUtcNow);
        alpha.Deactivate();
        _dbContext.Brands.AddRange(zed, alpha);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync();

        result.Select(b => b.Name).Should().Equal("Alpha Brand", "Zed Brand");
        result.Single(b => b.Name == "Alpha Brand").IsActive.Should().BeFalse();
    }
}
