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
        var staff = User.CreateStaff("pin-hash", "Staff Member", UserRole.Staff, brandId, branchId, FixedUtcNow);

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
}
