using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Features.Users.GetUser;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Users.GetUser;

[TestClass]
public sealed class GetUserQueryHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private GetUserQueryHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new GetUserQueryHandler(_dbContext);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_BranchManagerFetchingAStaffMemberInOwnBranch_Succeeds()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var staff = User.CreateStaff("pin-hash", "Staff Member", brandId, branchId, FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, brandId, branchId);
        var result = await _handler.HandleAsync(requester, staff.Id);

        result.Error.Should().Be(UserOperationError.None);
        result.User!.DisplayName.Should().Be("Staff Member");
    }

    [TestMethod]
    public async Task HandleAsync_BranchManagerFetchingAStaffMemberInAnotherBranch_ReturnsForbidden()
    {
        var staff = User.CreateStaff("pin-hash", "Staff Member", Guid.NewGuid(), Guid.NewGuid(), FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, Guid.NewGuid(), Guid.NewGuid());
        var result = await _handler.HandleAsync(requester, staff.Id);

        result.Error.Should().Be(UserOperationError.Forbidden);
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownUserId_ReturnsNotFound()
    {
        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);
        var result = await _handler.HandleAsync(requester, Guid.NewGuid());

        result.Error.Should().Be(UserOperationError.NotFound);
    }
}
