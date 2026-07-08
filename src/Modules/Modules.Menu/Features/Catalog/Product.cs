namespace Modules.Menu.Features.Catalog;

public sealed class Product
{
    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    /// <summary>
    /// Denormalized from Category so brand-scoped product queries never join.
    /// </summary>
    public Guid BrandId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    /// <summary>
    /// Null until real image storage exists (deferred); the POS renders a
    /// styled initials placeholder when absent.
    /// </summary>
    public string? ImageUrl { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    private Product()
    {
        // EF Core materialization.
    }

    public static Product Create(
        Guid brandId, Guid categoryId, string name, decimal price, int displayOrder, string? imageUrl = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            BrandId = brandId,
            CategoryId = categoryId,
            Name = name,
            Price = price,
            ImageUrl = imageUrl,
            DisplayOrder = displayOrder,
            IsActive = true,
        };
}
