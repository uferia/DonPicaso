namespace Modules.Menu.Features.Catalog;

/// <summary>
/// A brand-scoped menu section (Coffee, Snacks, ...). The tenancy model shares
/// a Brand's menu across all of its Branches, so there is no BranchId here.
/// </summary>
public sealed class Category
{
    public Guid Id { get; private set; }

    public Guid BrandId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    private Category()
    {
        // EF Core materialization.
    }

    public static Category Create(Guid brandId, string name, int displayOrder) =>
        new()
        {
            Id = Guid.NewGuid(),
            BrandId = brandId,
            Name = name,
            DisplayOrder = displayOrder,
            IsActive = true,
        };
}
