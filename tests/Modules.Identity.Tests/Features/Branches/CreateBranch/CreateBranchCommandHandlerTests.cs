using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches.CreateBranch;
using Modules.Identity.Features.Brands;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Branches.CreateBranch;

[TestClass]
public sealed class CreateBranchCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private CreateBranchCommandHandler _handler = null!;
    private Guid _brandId;

    [TestInitialize]
    public async Task TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);

        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        _brandId = brand.Id;
        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync();

        var timeProviderMock = new Moq.Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        _handler = new CreateBranchCommandHandler(_dbContext, new CreateBranchCommandValidator(), timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithAValidName_PersistsAnActiveBranchUnderTheBrand()
    {
        var result = await _handler.HandleAsync(_brandId, new CreateBranchCommand("Downtown"));

        result.BrandId.Should().Be(_brandId);
        result.Name.Should().Be("Downtown");
        result.IsActive.Should().BeTrue();
    }

    [TestMethod]
    public async Task HandleAsync_WithAnEmptyName_ThrowsValidationException()
    {
        var act = () => _handler.HandleAsync(_brandId, new CreateBranchCommand(""));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }
}
