using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
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
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var branch = Branch.Create(brand.Id, "Downtown", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        _dbContext.Branches.Add(branch);
        var staff = User.CreateStaff("pin-hash", "Staff Member", brand.Id, branch.Id, FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BranchManager, brand.Id, branch.Id);
        var command = new UpdateUserCommand(
            "Renamed Staff", UserRole.Staff, brand.Id, branch.Id, Email: null, NewPassword: null, NewPin: null);

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
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var branch = Branch.Create(brand.Id, "Downtown", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        _dbContext.Branches.Add(branch);
        var manager = User.CreateAdmin(
            "manager@donpicaso.dev", "password-hash", "Branch Manager", UserRole.BranchManager, brand.Id, branch.Id, FixedUtcNow);
        _dbContext.Users.Add(manager);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);
        var command = new UpdateUserCommand(
            "Demoted Staff", UserRole.Staff, brand.Id, branch.Id, Email: null, NewPassword: null, NewPin: null);

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

    [TestMethod]
    public async Task HandleAsync_BrandOwnerAssigningAUserToARealBranchBelongingToADifferentBrand_ReturnsForbidden()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var otherBrand = Brand.Create("Rival Brand", FixedUtcNow);
        var homeBranch = Branch.Create(brand.Id, "Home Branch", FixedUtcNow);
        var otherBrandBranch = Branch.Create(otherBrand.Id, "Rival Branch", FixedUtcNow);
        _dbContext.Brands.AddRange(brand, otherBrand);
        _dbContext.Branches.AddRange(homeBranch, otherBrandBranch);
        var staff = User.CreateStaff("pin-hash", "Staff Member", brand.Id, homeBranch.Id, FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BrandOwner, brand.Id, BranchId: null);
        var command = new UpdateUserCommand(
            "Staff Member", UserRole.Staff, brand.Id, otherBrandBranch.Id, Email: null, NewPassword: null, NewPin: null);

        var result = await _handler.HandleAsync(requester, staff.Id, command);

        result.Error.Should().Be(UserOperationError.Forbidden);
    }
}
