using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth.Login;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Auth.Login;

[TestClass]
public sealed class LoginCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
    private const string Email = "corporate@donpicaso.dev";
    private const string Password = "Password123!";

    private IdentityDbContext _dbContext = null!;
    private PasswordHasher<User> _passwordHasher = null!;
    private LoginCommandHandler _handler = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _passwordHasher = new PasswordHasher<User>();

        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        // PasswordHasher<TUser>.HashPassword doesn't read the user instance
        // (it only exists for generic dispatch), so a throwaway is safe here.
        var user = User.CreateAdmin(
            Email, _passwordHasher.HashPassword(null!, Password), "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _handler = new LoginCommandHandler(
            _dbContext,
            new LoginCommandValidator(),
            _passwordHasher,
            new JwtTokenService(new JwtOptions
            {
                Issuer = "DonPicaso.Tests",
                Audience = "DonPicaso.Tests.Pos",
                SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
            }),
            timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithCorrectCredentials_ReturnsTokensAndPersistsARefreshToken()
    {
        var result = await _handler.HandleAsync(new LoginCommand(Email, Password));

        result.IsSuccess.Should().BeTrue();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        (await _dbContext.RefreshTokens.CountAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task HandleAsync_WithWrongPassword_FailsWithoutPersistingARefreshToken()
    {
        var result = await _handler.HandleAsync(new LoginCommand(Email, "wrong-password"));

        result.IsSuccess.Should().BeFalse();
        (await _dbContext.RefreshTokens.AnyAsync()).Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WithUnknownEmail_FailsTheSameShapeAsAWrongPassword()
    {
        // Same shape either way, so a caller can't use this endpoint to
        // learn whether an email address has an account.
        var knownEmailResult = await _handler.HandleAsync(new LoginCommand(Email, "wrong-password"));
        var unknownEmailResult = await _handler.HandleAsync(new LoginCommand("nobody@donpicaso.dev", "wrong-password"));

        knownEmailResult.IsSuccess.Should().Be(unknownEmailResult.IsSuccess);
        knownEmailResult.AccessToken.Should().Be(unknownEmailResult.AccessToken);
    }

    [TestMethod]
    public void DummyUserAndDummyPasswordHash_AreWellFormed()
    {
        // The "user not found" path verifies against these fixed dummy
        // values instead of short-circuiting, so the not-found and
        // wrong-password paths cost the same amount of hashing work (a
        // timing side-channel fix - see HandleAsync). This asserts the
        // static fields the fix depends on are actually usable: a real
        // user instance and a hash produced by the real hasher, not an
        // empty/placeholder string that would make the "equal work" claim
        // false.
        var dummyUserField = typeof(LoginCommandHandler)
            .GetField("DummyUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var dummyHashField = typeof(LoginCommandHandler)
            .GetField("DummyPasswordHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var dummyUser = dummyUserField!.GetValue(null).Should().BeOfType<User>().Subject;
        var dummyHash = dummyHashField!.GetValue(null).Should().BeOfType<string>().Subject;

        dummyUser.Role.Should().Be(UserRole.Corporate);
        dummyHash.Should().NotBeNullOrWhiteSpace();
        _passwordHasher.VerifyHashedPassword(dummyUser, dummyHash, "dummy-password-for-timing-safety")
            .Should().Be(PasswordVerificationResult.Success);
    }

    [TestMethod]
    public async Task HandleAsync_WhenUserIsDeactivated_FailsTheSameShapeAsAWrongPassword()
    {
        var user = await _dbContext.Users.SingleAsync(u => u.Email == Email);
        user.Deactivate();
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new LoginCommand(Email, Password));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenAssignedBranchIsDeactivated_Fails()
    {
        var branch = Branch.Create(Guid.NewGuid(), "Downtown", FixedUtcNow);
        _dbContext.Branches.Add(branch);
        var user = User.CreateAdmin(
            "manager@donpicaso.dev", _passwordHasher.HashPassword(null!, Password), "Branch Manager",
            UserRole.BranchManager, branch.BrandId, branch.Id, FixedUtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        branch.Deactivate();
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new LoginCommand("manager@donpicaso.dev", Password));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenAssignedBrandIsDeactivated_Fails()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        var user = User.CreateAdmin(
            "owner@donpicaso.dev", _passwordHasher.HashPassword(null!, Password), "Brand Owner",
            UserRole.BrandOwner, brand.Id, branchId: null, FixedUtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        brand.Deactivate();
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new LoginCommand("owner@donpicaso.dev", Password));

        result.IsSuccess.Should().BeFalse();
    }
}
