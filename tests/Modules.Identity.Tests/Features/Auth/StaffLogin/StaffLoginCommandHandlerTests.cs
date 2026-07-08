using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Modules.Identity.Features.Auth.StaffLogin;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
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

        var brand = Brand.Create("Test Brand", FixedUtcNow);
        var branch = Branch.Create(brand.Id, "Test Branch", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();

        _branchId = branch.Id;
        var staff = User.CreateStaff(
            passwordHasher.HashPassword(null!, Pin), "Staff Member",
            brandId: brand.Id, branchId: branch.Id, FixedUtcNow);
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

    [TestMethod]
    public void DummyUserAndDummyPasswordHash_AreWellFormed()
    {
        // The "user/branch not found" path verifies against these fixed
        // dummy values instead of short-circuiting, so the not-found and
        // wrong-PIN paths cost the same amount of hashing work (a timing
        // side-channel fix - see HandleAsync). This asserts the static
        // fields the fix depends on are actually usable: a real Staff user
        // instance and a hash produced by the real hasher.
        var dummyUserField = typeof(StaffLoginCommandHandler)
            .GetField("DummyUser", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var dummyHashField = typeof(StaffLoginCommandHandler)
            .GetField("DummyPasswordHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var dummyUser = dummyUserField!.GetValue(null).Should().BeOfType<User>().Subject;
        var dummyHash = dummyHashField!.GetValue(null).Should().BeOfType<string>().Subject;
        var passwordHasher = new PasswordHasher<User>();

        dummyUser.Role.Should().Be(UserRole.Staff);
        dummyHash.Should().NotBeNullOrWhiteSpace();
        passwordHasher.VerifyHashedPassword(dummyUser, dummyHash, "dummy-pin-for-timing-safety")
            .Should().Be(PasswordVerificationResult.Success);
    }

    [TestMethod]
    public async Task HandleAsync_WhenStaffUserIsDeactivated_Fails()
    {
        var staff = await _dbContext.Users.SingleAsync(u => u.Id == _staffUserId);
        staff.Deactivate();
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new StaffLoginCommand(_branchId, _staffUserId, Pin));

        result.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task HandleAsync_WhenBranchIsDeactivated_Fails()
    {
        var branch = Branch.Create(Guid.NewGuid(), "Downtown", FixedUtcNow);
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();
        branch.Deactivate();
        await _dbContext.SaveChangesAsync();

        var passwordHasher = new PasswordHasher<User>();
        var staff = User.CreateStaff(passwordHasher.HashPassword(null!, Pin), "Other Staff", branch.BrandId, branch.Id, FixedUtcNow);
        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(new StaffLoginCommand(branch.Id, staff.Id, Pin));

        result.IsSuccess.Should().BeFalse();
    }
}
