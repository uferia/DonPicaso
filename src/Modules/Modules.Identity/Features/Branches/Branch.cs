namespace Modules.Identity.Features.Branches;

public sealed class Branch
{
    public Guid Id { get; private set; }

    public Guid BrandId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Branch()
    {
        // EF Core materialization.
    }

    public static Branch Create(Guid brandId, string name, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            BrandId = brandId,
            Name = name,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        };

    public void Rename(string name) => Name = name;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
