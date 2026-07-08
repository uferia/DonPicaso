using FluentValidation;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Brands.CreateBrand;

public sealed class CreateBrandCommandHandler(
    IdentityDbContext dbContext,
    IValidator<CreateBrandCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<BrandResult> HandleAsync(CreateBrandCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var brand = Brand.Create(command.Name, timeProvider.GetUtcNow());
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(brand);
    }

    internal static BrandResult ToResult(Brand brand) => new(brand.Id, brand.Name, brand.IsActive, brand.CreatedAtUtc);
}

public sealed record BrandResult(Guid Id, string Name, bool IsActive, DateTimeOffset CreatedAtUtc);
