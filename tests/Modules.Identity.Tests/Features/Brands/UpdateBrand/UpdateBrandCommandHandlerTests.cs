using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Brands;
using Modules.Identity.Features.Brands.UpdateBrand;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Brands.UpdateBrand;

[TestClass]
public sealed class UpdateBrandCommandHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private UpdateBrandCommandHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new UpdateBrandCommandHandler(_dbContext, new UpdateBrandCommandValidator());
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_WithAnExistingBrandId_RenamesIt()
    {
        var brand = Brand.Create("Don Picaso Original", FixedUtcNow);
        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brand.Id, new UpdateBrandCommand("Don Picaso Renamed"));

        result.Should().NotBeNull();
        result!.Name.Should().Be("Don Picaso Renamed");
        (await _dbContext.Brands.SingleAsync(b => b.Id == brand.Id)).Name.Should().Be("Don Picaso Renamed");
    }

    [TestMethod]
    public async Task HandleAsync_WithAnUnknownBrandId_ReturnsNull()
    {
        var result = await _handler.HandleAsync(Guid.NewGuid(), new UpdateBrandCommand("Anything"));

        result.Should().BeNull();
    }
}
