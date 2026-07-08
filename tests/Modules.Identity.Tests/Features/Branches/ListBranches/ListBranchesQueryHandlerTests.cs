using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Branches.ListBranches;
using Modules.Identity.Features.Brands;
using Modules.Identity.Persistence;

namespace Modules.Identity.Tests.Features.Branches.ListBranches;

[TestClass]
public sealed class ListBranchesQueryHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    private IdentityDbContext _dbContext = null!;
    private ListBranchesQueryHandler _handler = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-tests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new IdentityDbContext(options);
        _handler = new ListBranchesQueryHandler(_dbContext);
    }

    [TestCleanup]
    public void TestCleanup() => _dbContext.Dispose();

    [TestMethod]
    public async Task HandleAsync_ReturnsOnlyBranchesUnderTheRequestedBrand_OrderedByName()
    {
        var brandA = Brand.Create("Brand A", FixedUtcNow);
        var brandB = Brand.Create("Brand B", FixedUtcNow);
        var zed = Branch.Create(brandA.Id, "Zed Branch", FixedUtcNow);
        var alpha = Branch.Create(brandA.Id, "Alpha Branch", FixedUtcNow);
        var otherBrandBranch = Branch.Create(brandB.Id, "Other Brand Branch", FixedUtcNow);
        _dbContext.Brands.AddRange(brandA, brandB);
        _dbContext.Branches.AddRange(zed, alpha, otherBrandBranch);
        await _dbContext.SaveChangesAsync();

        var result = await _handler.HandleAsync(brandA.Id);

        result.Select(b => b.Name).Should().Equal("Alpha Branch", "Zed Branch");
    }
}
