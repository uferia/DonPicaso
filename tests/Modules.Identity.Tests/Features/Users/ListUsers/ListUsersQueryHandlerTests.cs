using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Users;
using Modules.Identity.Features.Users.ListUsers;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Users.ListUsers;

[TestClass]
public sealed class ListUsersQueryHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private ListUsersQueryHandler _handler = null!;
    private Guid _brandId;
    private Guid _branchAId;
    private Guid _branchBId;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new ListUsersQueryHandler(_dbContext);

        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var branchA = Branch.Create(brand.Id, "Branch A", FixedUtcNow);
        var branchB = Branch.Create(brand.Id, "Branch B", FixedUtcNow);
        _brandId = brand.Id;
        _branchAId = branchA.Id;
        _branchBId = branchB.Id;
        _dbContext.Brands.Add(brand);
        _dbContext.Branches.AddRange(branchA, branchB);

        _dbContext.Users.AddRange(
            User.CreateStaff("pin-hash-1", "Zed Staff", brand.Id, branchA.Id, FixedUtcNow),
            User.CreateStaff("pin-hash-2", "Alpha Staff", brand.Id, branchA.Id, FixedUtcNow),
            User.CreateStaff("pin-hash-3", "Branch B Staff", brand.Id, branchB.Id, FixedUtcNow));
        await _dbContext.SaveChangesAsync();
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_BranchManager_OnlySeesOwnBranchRegardlessOfFilters()
    {
        var requester = new RequestingUserContext(UserRole.BranchManager, _brandId, _branchAId);

        var result = await _handler.HandleAsync(requester, brandIdFilter: null, branchIdFilter: _branchBId);

        result.IsForbidden.Should().BeFalse();
        result.Users.Should().OnlyContain(u => u.BranchId == _branchAId);
    }

    [TestMethod]
    public async Task HandleAsync_BrandOwner_WithNoBranchFilter_SeesEveryUserInOwnBrand()
    {
        var requester = new RequestingUserContext(UserRole.BrandOwner, _brandId, BranchId: null);

        var result = await _handler.HandleAsync(requester, brandIdFilter: null, branchIdFilter: null);

        result.Users.Should().HaveCount(3);
    }

    [TestMethod]
    public async Task HandleAsync_BrandOwner_FilteringByAKnownOwnBranch_ScopesToThatBranch()
    {
        var requester = new RequestingUserContext(UserRole.BrandOwner, _brandId, BranchId: null);

        var result = await _handler.HandleAsync(requester, brandIdFilter: null, branchIdFilter: _branchAId);

        result.IsForbidden.Should().BeFalse();
        result.Users.Should().OnlyContain(u => u.BranchId == _branchAId);
    }

    [TestMethod]
    public async Task HandleAsync_BrandOwner_FilteringByABranchOutsideOwnBrand_ReturnsForbidden()
    {
        var requester = new RequestingUserContext(UserRole.BrandOwner, _brandId, BranchId: null);

        var result = await _handler.HandleAsync(requester, brandIdFilter: null, branchIdFilter: Guid.NewGuid());

        result.IsForbidden.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_Corporate_CanSeeEveryoneOrFilterByAnyBrandOrBranch()
    {
        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);

        var all = await _handler.HandleAsync(requester, brandIdFilter: null, branchIdFilter: null);
        var scoped = await _handler.HandleAsync(requester, brandIdFilter: null, branchIdFilter: _branchBId);

        all.Users.Should().HaveCount(3);
        scoped.Users.Should().ContainSingle(u => u.BranchId == _branchBId);
    }

    [TestMethod]
    public async Task HandleAsync_Staff_ReturnsForbidden()
    {
        var requester = new RequestingUserContext(UserRole.Staff, _brandId, _branchAId);

        var result = await _handler.HandleAsync(requester, brandIdFilter: null, branchIdFilter: null);

        result.IsForbidden.Should().BeTrue();
    }
}
