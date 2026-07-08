using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Brands.CreateBrand;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Brands.CreateBrand;

[TestClass]
public sealed class CreateBrandCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private CreateBrandCommandHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);

        var timeProviderMock = new Moq.Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(FixedUtcNow);

        _handler = new CreateBrandCommandHandler(_dbContext, new CreateBrandCommandValidator(), timeProviderMock.Object);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithAValidName_PersistsAnActiveBrand()
    {
        var result = await _handler.HandleAsync(new CreateBrandCommand("Don Picaso Original"));

        result.Name.Should().Be("Don Picaso Original");
        result.IsActive.Should().BeTrue();
        (await _dbContext.Brands.CountAsync()).Should().Be(1);
    }

    [TestMethod]
    public async Task HandleAsync_WithAnEmptyName_ThrowsValidationException()
    {
        var act = () => _handler.HandleAsync(new CreateBrandCommand(""));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }
}
