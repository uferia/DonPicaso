using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Brands.CreateBrand;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Brands.UpdateBrand;

public sealed class UpdateBrandCommandHandler(
    IdentityDbContext dbContext,
    IValidator<UpdateBrandCommand> validator)
{
    public async Task<BrandResult?> HandleAsync(
        Guid brandId, UpdateBrandCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var brand = await dbContext.Brands.FirstOrDefaultAsync(b => b.Id == brandId, cancellationToken);
        if (brand is null)
        {
            return null;
        }

        brand.Rename(command.Name);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateBrandCommandHandler.ToResult(brand);
    }
}
