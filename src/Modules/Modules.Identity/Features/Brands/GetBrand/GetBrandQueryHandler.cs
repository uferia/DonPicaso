using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Brands.CreateBrand;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Brands.GetBrand;

public sealed class GetBrandQueryHandler(IdentityDbContext dbContext)
{
    public async Task<BrandResult?> HandleAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var brand = await dbContext.Brands.FirstOrDefaultAsync(b => b.Id == brandId, cancellationToken);
        return brand is null ? null : CreateBrandCommandHandler.ToResult(brand);
    }
}
