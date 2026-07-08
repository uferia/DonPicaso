using Microsoft.EntityFrameworkCore;
using Modules.Menu.Features.Catalog;

namespace Modules.Menu.Persistence;

public sealed class MenuDbContext(DbContextOptions<MenuDbContext> options) : DbContext(options)
{
    public const string Schema = "menu";

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MenuDbContext).Assembly);
    }
}
