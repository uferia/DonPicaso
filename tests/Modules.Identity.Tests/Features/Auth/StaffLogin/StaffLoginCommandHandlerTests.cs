using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth.StaffLogin;
using Modules.Identity.Features.Users;
using Modules.Identity.Infrastructure;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Auth.StaffLogin;

[TestClass]
public sealed class StaffLoginCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
    private const string Pin = "1234";

    private IdentityDbContext _dbContext = null!;
    private Guid _branchId;
    private Guid _staffUserId;
    private StaffLoginCommandHandler _handler = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        var passwordHasher = new PasswordHasher<User>();
        _branchId = Guid.NewGuid();

        var staff = User.CreateStaff(
            passwordHasher.HashPassword(null!, Pin), "Staff Member",
            brandId: Guid.NewGuid(), branchId: _branchId, FixedUtcNow);
        _staffUserId = staff.Id;
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        _handler = new StaffLoginCommandHandler(
            _dbContext,
            new StaffLoginCommandValidator(),
            passwordHasher,
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
    public async Task HandleAsync_WithCorrectPin_ReturnsTokens()
    {
        var result = await _handler.HandleAsync(new StaffLoginCommand(_branchId, _staffUserId, Pin));

        result.IsSuccess.Should().BeTrue();
        result.AccessToken.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public async Task HandleAsync_WithWrongPin_Fails()
    {
        var result = await _handler.HandleAsync(new StaffLoginCommand(_branchId, _staffUserId, "9999"));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenUserBelongsToADifferentBranch_Fails()
    {
        var result = await _handler.HandleAsync(new StaffLoginCommand(Guid.NewGuid(), _staffUserId, Pin));

        result.IsSuccess.Should().BeFalse();
    }
}
