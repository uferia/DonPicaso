using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Features.Users.SetUserActiveState;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Users.SetUserActiveState;

[TestClass]
public sealed class SetUserActiveStateCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private SetUserActiveStateCommandHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new SetUserActiveStateCommandHandler(_dbContext);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_DeactivatingAUserWithinScope_Succeeds()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var staff = User.CreateStaff("pin-hash", "Staff Member", brandId, branchId, FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, brandId, branchId);
        var result = await _handler.HandleAsync(requester, staff.Id, isActive: false);

        result.Error.Should().Be(UserOperationError.None);
        result.User!.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_ReactivatingADeactivatedUser_Succeeds()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var staff = User.CreateStaff("pin-hash", "Staff Member", brandId, branchId, FixedUtcNow);
        staff.Deactivate();
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, brandId, branchId);
        var result = await _handler.HandleAsync(requester, staff.Id, isActive: true);

        result.User!.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_WhenRequesterIsOutOfScope_ReturnsForbidden()
    {
        var staff = User.CreateStaff("pin-hash", "Staff Member", Guid.NewGuid(), Guid.NewGuid(), FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, Guid.NewGuid(), Guid.NewGuid());
        var result = await _handler.HandleAsync(requester, staff.Id, isActive: false);

        result.Error.Should().Be(UserOperationError.Forbidden);
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownUserId_ReturnsNotFound()
    {
        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);
        var result = await _handler.HandleAsync(requester, Guid.NewGuid(), isActive: false);

        result.Error.Should().Be(UserOperationError.NotFound);
    }
}
