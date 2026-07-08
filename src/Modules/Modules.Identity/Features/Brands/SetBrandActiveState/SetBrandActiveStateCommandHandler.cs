using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Brands.CreateBrand;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Brands.SetBrandActiveState;

public sealed class SetBrandActiveStateCommandHandler(IdentityDbContext dbContext)
{
    public async Task<BrandResult?> HandleAsync(Guid brandId, bool isActive, CancellationToken cancellationToken = default)
    {
        var brand = await dbContext.Brands.FirstOrDefaultAsync(b => b.Id == brandId, cancellationToken);
        if (brand is null)
        {
            return null;
        }

        if (isActive)
        {
            brand.Reactivate();
        }
        else
        {
            brand.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return CreateBrandCommandHandler.ToResult(brand);
    }
}
