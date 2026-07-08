using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Users;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Users.CreateUser;

[TestClass]
public sealed class CreateUserCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private CreateUserCommandHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);

        var timeProviderMock = new Moq.Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        _handler = new CreateUserCommandHandler(
            _dbContext, new CreateUserCommandValidator(), new PasswordHasher<User>(), timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_CorporateCreatingABrandOwner_Succeeds()
    {
        var requester = new RequestingUserContext(UserRole.Corporate, BrandId: null, BranchId: null);
        var command = new CreateUserCommand(
            "owner@donpicaso.dev", "Brand Owner", UserRole.BrandOwner, Guid.NewGuid(), null, "Password123!", null);

        var result = await _handler.HandleAsync(requester, command);

        result.Error.Should().Be(UserOperationError.None);
        result.User!.Role.Should().Be(UserRole.BrandOwner);
        (await _dbContext.Users.CountAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task HandleAsync_BrandOwnerCreatingStaffWithinOwnBrand_Succeeds()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var branch = Branch.Create(brand.Id, "Downtown", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        _dbContext.Branches.Add(branch);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BrandOwner, brand.Id, BranchId: null);
        var command = new CreateUserCommand(null, "Staff Member", UserRole.Staff, brand.Id, branch.Id, null, "1234");

        var result = await _handler.HandleAsync(requester, command);

        result.Error.Should().Be(UserOperationError.None);
        result.User!.Role.Should().Be(UserRole.Staff);
    }

    [TestMethod]
    public async Task HandleAsync_BrandOwnerCreatingStaffInADifferentBrand_ReturnsForbidden()
    {
        var requester = new RequestingUserContext(UserRole.BrandOwner, Guid.NewGuid(), BranchId: null);
        var command = new CreateUserCommand(null, "Staff Member", UserRole.Staff, Guid.NewGuid(), Guid.NewGuid(), null, "1234");

        var result = await _handler.HandleAsync(requester, command);

        result.Error.Should().Be(UserOperationError.Forbidden);
        (await _dbContext.Users.CountAsync()).Should().Be(0);
    }

    [TestMethod]
    public async Task HandleAsync_BranchManagerCreatingABranchManager_ReturnsForbidden()
    {
        var brandId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var requester = new RequestingUserContext(UserRole.BranchManager, brandId, branchId);
        var command = new CreateUserCommand(
            "manager@donpicaso.dev", "Another Manager", UserRole.BranchManager, brandId, branchId, "Password123!", null);

        var result = await _handler.HandleAsync(requester, command);

        result.Error.Should().Be(UserOperationError.Forbidden);
    }

    [TestMethod]
    public async Task HandleAsync_BrandOwnerCreatingStaffWithARealBranchBelongingToADifferentBrand_ReturnsForbidden()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        var otherBrand = Brand.Create("Rival Brand", FixedUtcNow);
        var otherBrandBranch = Branch.Create(otherBrand.Id, "Rival Branch", FixedUtcNow);
        _dbContext.Brands.AddRange(brand, otherBrand);
        _dbContext.Branches.Add(otherBrandBranch);
        await _dbContext.SaveChangesAsync();

        var requester = new RequestingUserContext(UserRole.BrandOwner, brand.Id, BranchId: null);
        var command = new CreateUserCommand(null, "Staff Member", UserRole.Staff, brand.Id, otherBrandBranch.Id, null, "1234");

        var result = await _handler.HandleAsync(requester, command);

        result.Error.Should().Be(UserOperationError.Forbidden);
        (await _dbContext.Users.CountAsync()).Should().Be(0);
    }
}
