using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Brands.CreateBrand;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Brands.ListBrands;

public sealed class ListBrandsQueryHandler(IdentityDbContext dbContext)
{
    public async Task<IReadOnlyList<BrandResult>> HandleAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Brands
            .OrderBy(b => b.Name)
            .Select(b => new BrandResult(b.Id, b.Name, b.IsActive, b.CreatedAtUtc))
            .ToListAsync(cancellationToken);
}
