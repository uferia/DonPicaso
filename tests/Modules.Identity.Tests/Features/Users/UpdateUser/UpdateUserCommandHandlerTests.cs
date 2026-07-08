using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Features.Users.UpdateUser;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Users.UpdateUser;

[TestClass]
public sealed class UpdateUserCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private UpdateUserCommandHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new UpdateUserCommandHandler(_dbContext, new UpdateUserCommandValidator(), new PasswordHasher<User>());
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_RenamingAUserWithinScope_Succeeds()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var staff = User.CreateStaff("pin-hash", "Staff Member", brandId, branchId, FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, brandId, branchId);
        var command = new UpdateUserCommand("Renamed Staff", UserRole.Staff, brandId, branchId, Email: null, NewPassword: null, NewPin: null);

        var result = await _handler.HandleAsync(requester, staff.Id, command);

        result.Error.Should().Be(UserOperationError.None);
        result.User!.DisplayName.Should().Be("Renamed Staff");
    }

    [TestMethod]
    public async Task HandleAsync_PromotingStaffToBrandOwnerWithEmailAndPassword_SwapsCredentialType()
    {
        var brandId = Guid.NewGuid();
        var staff = User.CreateStaff("pin-hash", "Staff Member", brandId, Guid.NewGuid(), FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);
        var command = new UpdateUserCommand(
            "Promoted Owner", UserRole.BrandOwner, brandId, null, "promoted@donpicaso.dev", "Password123!", NewPin: null);

        var result = await _handler.HandleAsync(requester, staff.Id, command);

        result.Error.Should().Be(UserOperationError.None);
        result.User!.Role.Should().Be(UserRole.BrandOwner);
        result.User!.Email.Should().Be("promoted@donpicaso.dev");
    }

    [TestMethod]
    public async Task HandleAsync_PromotingStaffToBrandOwnerWithoutANewPassword_ReturnsInvalidRoleAssignment()
    {
        var brandId = Guid.NewGuid();
        var staff = User.CreateStaff("pin-hash", "Staff Member", brandId, Guid.NewGuid(), FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);
        var command = new UpdateUserCommand(
            "Promoted Owner", UserRole.BrandOwner, brandId, null, "promoted@donpicaso.dev", NewPassword: null, NewPin: null);

        var result = await _handler.HandleAsync(requester, staff.Id, command);

        result.Error.Should().Be(UserOperationError.InvalidRoleAssignment);
    }

    [TestMethod]
    public async Task HandleAsync_DemotingABranchManagerToStaffWithoutANewPin_ReturnsInvalidRoleAssignment()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var manager = User.CreateAdmin(
            "manager@donpicaso.dev", "password-hash", "Branch Manager", UserRole.BranchManager, brandId, branchId, FixedUtcNow);
        _dbContext.Users.Add(manager);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);
        var command = new UpdateUserCommand(
            "Demoted Staff", UserRole.Staff, brandId, branchId, Email: null, NewPassword: null, NewPin: null);

        var result = await _handler.HandleAsync(requester, manager.Id, command);

        result.Error.Should().Be(UserOperationError.InvalidRoleAssignment);
    }

    [TestMethod]
    public async Task HandleAsync_BranchManagerEditingAUserOutsideOwnBranch_ReturnsForbidden()
    {
        var staff = User.CreateStaff("pin-hash", "Staff Member", Guid.NewGuid(), Guid.NewGuid(), FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, Guid.NewGuid(), Guid.NewGuid());
        var command = new UpdateUserCommand("Renamed", UserRole.Staff, staff.BrandId, staff.BranchId, null, null, null);

        var result = await _handler.HandleAsync(requester, staff.Id, command);

        result.Error.Should().Be(UserOperationError.Forbidden);
    }

    [TestMethod]
    public async Task HandleAsync_BranchManagerPromotingOwnStaffToBrandOwner_ReturnsForbidden()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var staff = User.CreateStaff("pin-hash", "Staff Member", brandId, branchId, FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, brandId, branchId);
        var command = new UpdateUserCommand(
            "Promoted", UserRole.BrandOwner, brandId, null, "promoted@donpicaso.dev", "Password123!", null);

        var result = await _handler.HandleAsync(requester, staff.Id, command);

        result.Error.Should().Be(UserOperationError.Forbidden);
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownUserId_ReturnsNotFound()
    {
        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);
        var command = new UpdateUserCommand("Anyone", UserRole.Corporate, null, null, null, null, null);

        var result = await _handler.HandleAsync(requester, Guid.NewGuid(), command);

        result.Error.Should().Be(UserOperationError.NotFound);
    }
}
