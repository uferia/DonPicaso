using Microsoft.EntityFrameworkCore;
using Modules.Menu.Persistence;

namespace Modules.Menu.Features.Catalog.GetMenu;

/// <summary>
/// Read model for the POS ordering screen: the caller's brand menu plus the
/// tax rate the cart must apply. Two set-based queries, grouped in memory —
/// menus are small (tens of rows), so no join gymnastics.
/// </summary>
public sealed class GetMenuQueryHandler(MenuDbContext dbContext, MenuOptions options)
{
    public async Task<MenuResult> HandleAsync(Guid brandId, CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.Categories
            .Where(c => c.BrandId == brandId && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        var products = await dbContext.Products
            .Where(p => p.BrandId == brandId && p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

        var categoryResults = categories
            .Select(c => new MenuCategoryResult(
                c.Id,
                c.Name,
                products
                    .Where(p => p.CategoryId == c.Id)
                    .Select(p => new MenuProductResult(p.Id, p.Name, p.Price, p.ImageUrl))
                    .ToList()))
            .ToList();

        return new MenuResult(categoryResults, options.TaxRatePercent);
    }
}

public sealed record MenuProductResult(Guid Id, string Name, decimal Price, string? ImageUrl);

public sealed record MenuCategoryResult(Guid Id, string Name, IReadOnlyList<MenuProductResult> Products);

public sealed record MenuResult(IReadOnlyList<MenuCategoryResult> Categories, decimal TaxRatePercent);
