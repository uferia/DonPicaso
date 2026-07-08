using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Branches.SetBranchActiveState;
using Modules.Identity.Features.Brands;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Branches.SetBranchActiveState;

[TestClass]
public sealed class SetBranchActiveStateCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private SetBranchActiveStateCommandHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new SetBranchActiveStateCommandHandler(_dbContext);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_DeactivatingABranchUnderItsBrand_PersistsIsActiveFalse()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var branch = Branch.Create(brand.Id, "Downtown", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brand.Id, branch.Id, isActive: false);

        result!.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenBranchBelongsToADifferentBrand_ReturnsNull()
    {
        var brandA = Brand.Create("Brand A", FixedUtcNow);
        var brandB = Brand.Create("Brand B", FixedUtcNow);
        var branch = Branch.Create(brandA.Id, "Downtown", FixedUtcNow);
        _dbContext.Brands.AddRange(brandA, brandB);
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brandB.Id, branch.Id, isActive: false);

        result.Should().BeNull();
    }

    [TestMethod]
    public async Task HandleAsync_ReactivatingADeactivatedBranch_PersistsIsActiveTrue()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var branch = Branch.Create(brand.Id, "Downtown", FixedUtcNow);
        branch.Deactivate();
        _dbContext.Brands.Add(brand);
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brand.Id, branch.Id, isActive: true);

        result!.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownBranchId_ReturnsNull()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brand.Id, Guid.NewGuid(), isActive: false);

        result.Should().BeNull();
    }
}
