using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Brands.SetBrandActiveState;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Brands.SetBrandActiveState;

[TestClass]
public sealed class SetBrandActiveStateCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private SetBrandActiveStateCommandHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new SetBrandActiveStateCommandHandler(_dbContext);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_DeactivatingAnExistingBrand_PersistsIsActiveFalse()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brand.Id, isActive: false);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
        (await _dbContext.Brands.SingleAsync(b => b.Id == brand.Id)).IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_ReactivatingADeactivatedBrand_PersistsIsActiveTrue()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        brand.Deactivate();
        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brand.Id, isActive: true);

        result!.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownBrandId_ReturnsNull()
    {
        var result = await _handler.HandleAsync(Guid.NewGuid(), isActive: false);

        result.Should().BeNull();
    }
}
