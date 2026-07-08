using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Auth.StaffRoster;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Users;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Auth.StaffRoster;

[TestClass]
public sealed class GetStaffRosterQueryHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private GetStaffRosterQueryHandler _handler = null!;
    private Brand _brand = null!;
    private Branch _branch = null!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new GetStaffRosterQueryHandler(_dbContext);

        _brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        _branch = Branch.Create(_brand.Id, "Downtown", FixedUtcNow);
        _dbContext.Brands.Add(_brand);
        _dbContext.Branches.Add(_branch);
        await _dbContext.SaveChangesAsync();
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_ExcludesADeactivatedStaffMember()
    {
        var active = User.CreateStaff("pin-hash-1", "Active Staff", _brand.Id, _branch.Id, FixedUtcNow);
        var inactive = User.CreateStaff("pin-hash-2", "Deactivated Staff", _brand.Id, _branch.Id, FixedUtcNow);
        inactive.Deactivate();
        _dbContext.Users.AddRange(active, inactive);
        await _dbContext.SaveChangesAsync();

        var roster = await _handler.HandleAsync(new GetStaffRosterQuery(_branch.Id));

        roster.Should().ContainSingle(m => m.UserId == active.Id);
    }

    [TestMethod]
    public async Task HandleAsync_WhenBranchIsDeactivated_ReturnsEmpty()
    {
        var staff = User.CreateStaff("pin-hash", "Staff Member", _brand.Id, _branch.Id, FixedUtcNow);
        _dbContext.Users.Add(staff);
        _branch.Deactivate();
        await _dbContext.SaveChangesAsync();

        var roster = await _handler.HandleAsync(new GetStaffRosterQuery(_branch.Id));

        roster.Should().BeEmpty();
    }

    [TestMethod]
    public async Task HandleAsync_WhenBrandIsDeactivated_ReturnsEmpty()
    {
        var staff = User.CreateStaff("pin-hash", "Staff Member", _brand.Id, _branch.Id, FixedUtcNow);
        _dbContext.Users.Add(staff);
        _brand.Deactivate();
        await _dbContext.SaveChangesAsync();

        var roster = await _handler.HandleAsync(new GetStaffRosterQuery(_branch.Id));

        roster.Should().BeEmpty();
    }
}
