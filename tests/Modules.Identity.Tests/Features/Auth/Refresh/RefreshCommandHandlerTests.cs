using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Auth.Refresh;
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
    private string _refreshTokenValue = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        var tokenService = new JwtTokenService(new JwtOptions
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

        _refreshTokenValue = tokenService.GenerateRefreshTokenValue();
        _dbContext.RefreshTokens.Add(RefreshToken.Create(
            user.Id, tokenService.HashRefreshToken(_refreshTokenValue), FixedUtcNow.AddDays(7)));
        await _dbContext.SaveChangesAsync();

        _handler = new RefreshCommandHandler(_dbContext, new RefreshCommandValidator(), tokenService, _timeProviderMock.Object);
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
}
