using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth;
using Modules.Identity.Features.Auth.Logout;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Auth.Logout;

[TestClass]
public sealed class LogoutCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private LogoutCommandHandler _handler = null!;
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

        var user = User.CreateAdmin(
            "corporate@donpicaso.dev", "password-hash", "Corporate Admin",
            UserRole.Corporate, brandId: null, branchId: null, FixedUtcNow);
        _dbContext.Users.Add(user);

        _refreshTokenValue = tokenService.GenerateRefreshTokenValue();
        _dbContext.RefreshTokens.Add(RefreshToken.Create(
            user.Id, tokenService.HashRefreshToken(_refreshTokenValue), FixedUtcNow.AddDays(7)));
        await _dbContext.SaveChangesAsync();

        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        _handler = new LogoutCommandHandler(_dbContext, tokenService, timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithAKnownToken_RevokesIt()
    {
        await _handler.HandleAsync(new LogoutCommand(_refreshTokenValue));

        var token = await _dbContext.RefreshTokens.SingleAsync();
        token.RevokedAtUtc.Should().Be(FixedUtcNow);
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownToken_DoesNotThrow()
    {
        var act = () => _handler.HandleAsync(new LogoutCommand("not-a-real-token"));

        await act.Should().NotThrowAsync();
    }
}
