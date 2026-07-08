using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Brands.GetBrand;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Brands.GetBrand;

[TestClass]
public sealed class GetBrandQueryHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private GetBrandQueryHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new GetBrandQueryHandler(_dbContext);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithAnExistingBrandId_ReturnsIt()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brand.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Don Picaso Original");
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownBrandId_ReturnsNull()
    {
        var result = await _handler.HandleAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
