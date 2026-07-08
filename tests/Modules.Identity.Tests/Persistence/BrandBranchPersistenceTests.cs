using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Persistence;

[TestClass]
public sealed class BrandBranchPersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task Branch_WhenSavedUnderABrand_ReloadsWithTheSameBrandId()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var branch = Branch.Create(brand.Id, "Downtown", FixedUtcNow);

        _dbContext.Brands.Add(brand);
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();

        var reloadedBranch = await _dbContext.Branches.SingleAsync(b => b.Id == branch.Id);

        reloadedBranch.BrandId.Should().Be(brand.Id);
        reloadedBranch.Name.Should().Be("Downtown");
    }

    [TestMethod]
    public async Task Brand_WhenCreated_IsActiveByDefault()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);

        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _dbContext.Brands.SingleAsync(b => b.Id == brand.Id);
        reloaded.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task Brand_WhenDeactivatedThenReactivated_PersistsBothTransitions()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync();

        brand.Deactivate();
        await _dbContext.SaveChangesAsync();
        (await _dbContext.Brands.SingleAsync(b => b.Id == brand.Id)).IsActive.Should().BeFalse();

        brand.Reactivate();
        await _dbContext.SaveChangesAsync();
        (await _dbContext.Brands.SingleAsync(b => b.Id == brand.Id)).IsActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task Branch_WhenDeactivated_PersistsIndependentlyOfItsBrand()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var branch = Branch.Create(brand.Id, "Downtown", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();

        branch.Deactivate();
        await _dbContext.SaveChangesAsync();

        var reloadedBrand = await _dbContext.Brands.SingleAsync(b => b.Id == brand.Id);
        var reloadedBranch = await _dbContext.Branches.SingleAsync(b => b.Id == branch.Id);
        reloadedBranch.IsActive.Should().BeFalse();
        reloadedBrand.IsActive.Should().BeTrue();
    }
}
