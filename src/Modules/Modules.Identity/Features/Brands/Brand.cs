namespace Modules.Identity.Features.Brands;

public sealed class Brand
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

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
            CreatedAtUtc = createdAtUtc,
        };
}
