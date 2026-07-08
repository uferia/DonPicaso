using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Auth.Refresh;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Auth.Refresh;

[TestClass]
public sealed class RefreshCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private Mock<TimeProvider> _timeProviderMock = null!;
    private RefreshCommandHandler _handler = null!;
    private JwtTokenService _tokenService = null!;
    private string _refreshTokenValue = null!;
    private Guid _userId;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _tokenService = new JwtTokenService(new JwtOptions
        {
            Issuer = "DonPicaso.Tests",
            Audience = "DonPicaso.Tests.Pos",
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
        });

        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);
        _dbContext.Users.Add(user);
        _userId = user.Id;

        _refreshTokenValue = _tokenService.GenerateRefreshTokenValue();
        _dbContext.RefreshTokens.Add(RefreshToken.Create(
            user.Id, _tokenService.HashRefreshToken(_refreshTokenValue), FixedUtcNow.AddDays(7)));
        await _dbContext.SaveChangesAsync();

        _handler = new RefreshCommandHandler(_dbContext, new RefreshCommandValidator(), _tokenService, _timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithAnUnexpiredKnownToken_ReturnsANewAccessToken()
    {
        var result = await _handler.HandleAsync(new RefreshCommand(_refreshTokenValue));

        result.IsSuccess.Should().BeTrue();
        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownToken_Fails()
    {
        var result = await _handler.HandleAsync(new RefreshCommand("not-a-real-token"));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WithAnExpiredToken_Fails()
    {
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow.AddDays(8));

        var result = await _handler.HandleAsync(new RefreshCommand(_refreshTokenValue));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WithARevokedToken_Fails()
    {
        var token = await _dbContext.RefreshTokens.SingleAsync();
        token.Revoke(FixedUtcNow);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new RefreshCommand(_refreshTokenValue));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenUserIsDeactivated_Fails()
    {
        var user = await _dbContext.Users.SingleAsync(u => u.Id == _userId);
        user.Deactivate();
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new RefreshCommand(_refreshTokenValue));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenAssignedBranchIsDeactivated_Fails()
    {
        var branch = Branch.Create(Guid.NewGuid(), "Downtown", FixedUtcNow);
        _dbContext.Branches.Add(branch);
        var user = User.CreateAdmin(
            "manager@donpicaso.dev", "password-hash", "Branch Manager",
            UserRole.BranchManager, branch.BrandId, branch.Id, FixedUtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        branch.Deactivate();
        await _dbContext.SaveChangesAsync();

        var refreshTokenValue = _tokenService.GenerateRefreshTokenValue();
        _dbContext.RefreshTokens.Add(RefreshToken.Create(
            user.Id, _tokenService.HashRefreshToken(refreshTokenValue), FixedUtcNow.AddDays(7)));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new RefreshCommand(refreshTokenValue));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenAssignedBrandIsDeactivated_Fails()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        var user = User.CreateAdmin(
            "owner@donpicaso.dev", "password-hash", "Brand Owner",
            UserRole.BrandOwner, brand.Id, branchId: null, FixedUtcNow);
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        brand.Deactivate();
        await _dbContext.SaveChangesAsync();

        var refreshTokenValue = _tokenService.GenerateRefreshTokenValue();
        _dbContext.RefreshTokens.Add(RefreshToken.Create(
            user.Id, _tokenService.HashRefreshToken(refreshTokenValue), FixedUtcNow.AddDays(7)));
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new RefreshCommand(refreshTokenValue));

        result.IsSuccess.Should().BeFalse();
    }
}
