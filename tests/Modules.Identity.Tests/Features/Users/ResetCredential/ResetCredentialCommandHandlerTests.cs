using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Features.Users.ResetCredential;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Users.ResetCredential;

[TestClass]
public sealed class ResetCredentialCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private ResetCredentialCommandHandler _handler = null!;
    private PasswordHasher<User> _passwordHasher = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _passwordHasher = new PasswordHasher<User>();
        _handler = new ResetCredentialCommandHandler(_dbContext, new ResetCredentialCommandValidator(), _passwordHasher);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_ResettingAStaffMembersPin_Succeeds()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var staff = User.CreateStaff("old-pin-hash", "Staff Member", brandId, branchId, FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, brandId, branchId);
        var result = await _handler.HandleAsync(requester, staff.Id, new ResetCredentialCommand(NewPassword: null, NewPin: "5678"));

        result.Error.Should().Be(UserOperationError.None);
        var reloaded = await _dbContext.Users.SingleAsync(u => u.Id == staff.Id);
        _passwordHasher.VerifyHashedPassword(reloaded, reloaded.PinHash!, "5678").Should().Be(PasswordVerificationResult.Success);
    }

    [TestMethod]
    public async Task HandleAsync_ResettingAStaffMembersPinWithoutSupplyingAPin_ReturnsInvalidRoleAssignment()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var staff = User.CreateStaff("old-pin-hash", "Staff Member", brandId, branchId, FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, brandId, branchId);
        var result = await _handler.HandleAsync(requester, staff.Id, new ResetCredentialCommand(NewPassword: "irrelevant", NewPin: null));

        result.Error.Should().Be(UserOperationError.InvalidRoleAssignment);
    }

    [TestMethod]
    public async Task HandleAsync_ResettingAnAdminsPassword_Succeeds()
    {
        var owner = User.CreateAdmin(
            "owner@donpicaso.dev", "old-password-hash", "Brand Owner", UserRole.BrandOwner, Guid.NewGuid(), null, FixedUtcNow);
        _dbContext.Users.Add(owner);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);
        var result = await _handler.HandleAsync(requester, owner.Id, new ResetCredentialCommand(NewPassword: "NewPassword123!", NewPin: null));

        result.Error.Should().Be(UserOperationError.None);
    }

    [TestMethod]
    public async Task HandleAsync_WhenRequesterIsOutOfScope_ReturnsForbidden()
    {
        var staff = User.CreateStaff("old-pin-hash", "Staff Member", Guid.NewGuid(), Guid.NewGuid(), FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, Guid.NewGuid(), Guid.NewGuid());
        var result = await _handler.HandleAsync(requester, staff.Id, new ResetCredentialCommand(NewPassword: null, NewPin: "5678"));

        result.Error.Should().Be(UserOperationError.Forbidden);
    }
}
