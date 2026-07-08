namespace Modules.Identity.Features.Brands;

public sealed class Brand
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Brand()
    {
        // EF Core materialization.
    }

    public static Brand Create(string name, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        };

    public void Rename(string name) => Name = name;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
