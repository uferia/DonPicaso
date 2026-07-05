using Microsoft.EntityFrameworkCore;
using Modules.Sales.Features.Orders;

namespace Modules.Sales.Persistence;

/// <summary>
/// EF Core context for the Sales module. Started as a bare skeleton for DI;
/// DbSets are added only when a vertical slice requires them (feature-first).
/// The <see cref="Orders"/> set was introduced by Features/Orders/CreateOrder.
/// </summary>
public sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    public const string Schema = "sales";

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Picks up IEntityTypeConfiguration<T> implementations that live inside
        // each vertical slice folder, keeping mapping co-located with the feature.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDbContext).Assembly);
    }
}
