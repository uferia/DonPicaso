using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Users;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Persistence;

[TestClass]
public sealed class UserPersistenceTests
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
    public async Task StaffUser_WhenSaved_ReloadsWithBrandAndBranchScopeAndNoEmail()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var staff = User.CreateStaff("pin-hash", "Staff Member", brandId, branchId, FixedUtcNow);

        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _dbContext.Users.SingleAsync(u => u.Id == staff.Id);

        reloaded.Email.Should().BeNull();
        reloaded.PasswordHash.Should().BeNull();
        reloaded.PinHash.Should().Be("pin-hash");
        reloaded.Role.Should().Be(UserRole.Staff);
        reloaded.BrandId.Should().Be(brandId);
        reloaded.BranchId.Should().Be(branchId);
    }

    [TestMethod]
    public async Task RefreshToken_WhenRevoked_PersistsRevokedAtUtc()
    {
        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);
        var token = RefreshToken.Create(user.Id, "token-hash", FixedUtcNow.AddDays(7));

        _dbContext.Users.Add(user);
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        token.Revoke(FixedUtcNow.AddHours(1));
        await _dbContext.SaveChangesAsync();

        var reloaded = await _dbContext.RefreshTokens.SingleAsync(t => t.Id == token.Id);
        reloaded.RevokedAtUtc.Should().Be(FixedUtcNow.AddHours(1));
    }

    [TestMethod]
    public void CreateAdmin_WithStaffRole_ThrowsArgumentException()
    {
        var act = () => User.CreateAdmin(
            "someone@donpicaso.dev", "password-hash", "Someone",
            UserRole.Staff, brandId: Guid.NewGuid(), branchId: Guid.NewGuid(), FixedUtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public async Task User_WhenCreated_IsActiveByDefault()
    {
        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        (await _dbContext.Users.SingleAsync(u => u.Id == user.Id)).IsActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task User_WhenDeactivatedThenReactivated_PersistsBothTransitions()
    {
        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        user.Deactivate();
        await _dbContext.SaveChangesAsync();
        (await _dbContext.Users.SingleAsync(u => u.Id == user.Id)).IsActive.Should().BeFalse();

        user.Reactivate();
        await _dbContext.SaveChangesAsync();
        (await _dbContext.Users.SingleAsync(u => u.Id == user.Id)).IsActive.Should().BeTrue();
    }

    [TestMethod]
    public void ChangeRole_FromStaffToBrandOwnerWithoutAnEmail_Throws()
    {
        var staff = User.CreateStaff("pin-hash", "Staff Member", Guid.NewGuid(), Guid.NewGuid(), FixedUtcNow);

        var act = () => staff.ChangeRole(UserRole.BrandOwner, staff.BrandId, null, email: null, newCredentialHash: "new-password-hash");

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void ChangeRole_FromStaffToBrandOwnerWithoutAnEmail_LeavesUserCompletelyUnchanged()
    {
        var staff = User.CreateStaff("pin-hash", "Staff Member", Guid.NewGuid(), Guid.NewGuid(), FixedUtcNow);

        var act = () => staff.ChangeRole(UserRole.BrandOwner, staff.BrandId, null, email: null, newCredentialHash: "new-password-hash");

        act.Should().Throw<ArgumentException>();
        staff.Role.Should().Be(UserRole.Staff);
        staff.PinHash.Should().Be("pin-hash");
        staff.PasswordHash.Should().BeNull();
    }

    [TestMethod]
    public void ChangeRole_FromStaffToBrandOwnerWithEmailAndPassword_SwapsCredentialType()
    {
        var staff = User.CreateStaff("pin-hash", "Staff Member", Guid.NewGuid(), Guid.NewGuid(), FixedUtcNow);

        staff.ChangeRole(UserRole.BrandOwner, staff.BrandId, null, email: "promoted@donpicaso.dev", newCredentialHash: "new-password-hash");

        staff.Role.Should().Be(UserRole.BrandOwner);
        staff.Email.Should().Be("promoted@donpicaso.dev");
        staff.PasswordHash.Should().Be("new-password-hash");
        staff.PinHash.Should().BeNull();
        staff.BranchId.Should().BeNull();
    }

    [TestMethod]
    public void ChangeRole_FromBrandOwnerToStaffWithoutANewPin_Throws()
    {
        var owner = User.CreateAdmin(
            "owner@donpicaso.dev", "password-hash", "Owner", UserRole.BrandOwner, Guid.NewGuid(), null, FixedUtcNow);

        var act = () => owner.ChangeRole(UserRole.Staff, owner.BrandId, Guid.NewGuid(), email: null, newCredentialHash: null);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void ChangeRole_BetweenTwoAdminTierRoles_KeepsExistingPasswordHash()
    {
        var manager = User.CreateAdmin(
            "manager@donpicaso.dev", "password-hash", "Manager", UserRole.BranchManager, Guid.NewGuid(), Guid.NewGuid(), FixedUtcNow);

        manager.ChangeRole(UserRole.BrandOwner, manager.BrandId, null, email: null, newCredentialHash: null);

        manager.Role.Should().Be(UserRole.BrandOwner);
        manager.PasswordHash.Should().Be("password-hash");
    }

    [TestMethod]
    public void ResetPassword_OnAStaffUser_Throws()
    {
        var staff = User.CreateStaff("pin-hash", "Staff Member", Guid.NewGuid(), Guid.NewGuid(), FixedUtcNow);

        var act = () => staff.ResetPassword("new-password-hash");

        act.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void ResetPin_OnAnAdminUser_Throws()
    {
        var owner = User.CreateAdmin(
            "owner@donpicaso.dev", "password-hash", "Owner", UserRole.BrandOwner, Guid.NewGuid(), null, FixedUtcNow);

        var act = () => owner.ResetPin("new-pin-hash");

        act.Should().Throw<InvalidOperationException>();
    }
}
