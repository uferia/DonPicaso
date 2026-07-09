using Microsoft.EntityFrameworkCore;
using Modules.Menu.Features.Catalog;

namespace Modules.Menu.Persistence;

/// <summary>
/// Seeds a sample menu for the given brand so the POS ordering screen is
/// exercisable before menu admin CRUD exists (a deferred sub-project).
/// Dev/test convenience only — not for production use. ImageUrl stays null:
/// the POS renders initials placeholders until image storage exists.
/// </summary>
public static class MenuSeeder
{
    public static async Task SeedAsync(MenuDbContext dbContext, Guid brandId)
    {
        if (await dbContext.Categories.AnyAsync())
        {
            return;
        }

        var coffee = Category.Create(brandId, "Coffee", 1);
        var beverages = Category.Create(brandId, "Beverages", 2);
        var snacks = Category.Create(brandId, "Snacks", 3);
        var desserts = Category.Create(brandId, "Desserts", 4);

        Product[] products =
        [
            Product.Create(brandId, coffee.Id, "Espresso", 2.50m, 1),
            Product.Create(brandId, coffee.Id, "Cappuccino", 3.75m, 2),
            Product.Create(brandId, coffee.Id, "Caffe Latte", 4.25m, 3),
            Product.Create(brandId, coffee.Id, "Mocha", 4.75m, 4),
            Product.Create(brandId, beverages.Id, "Fresh Orange Juice", 3.50m, 1),
            Product.Create(brandId, beverages.Id, "Iced Tea", 2.75m, 2),
            Product.Create(brandId, beverages.Id, "Sparkling Water", 2.00m, 3),
            Product.Create(brandId, snacks.Id, "Club Sandwich", 6.50m, 1),
            Product.Create(brandId, snacks.Id, "Quesadilla", 5.95m, 2),
            Product.Create(brandId, snacks.Id, "French Fries", 3.25m, 3),
            Product.Create(brandId, desserts.Id, "Tiramisu", 5.50m, 1),
            Product.Create(brandId, desserts.Id, "Cheesecake", 5.25m, 2),
        ];

        dbContext.Categories.AddRange(coffee, beverages, snacks, desserts);
        dbContext.Products.AddRange(products);

        await dbContext.SaveChangesAsync();
    }
}
