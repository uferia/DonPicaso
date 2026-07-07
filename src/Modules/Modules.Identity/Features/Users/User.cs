namespace Modules.Identity.Features.Users;

public sealed class User
{
    public Guid Id { get; private set; }

    public string? Email { get; private set; }

    public string? PasswordHash { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string? PinHash { get; private set; }

    public UserRole Role { get; private set; }

    public Guid? BrandId { get; private set; }

    public Guid? BranchId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private User()
    {
        // EF Core materialization.
    }

    /// <summary>Corporate, BrandOwner, or BranchManager — logs in with email+password.</summary>
    public static User CreateAdmin(
        string email,
        string passwordHash,
        string displayName,
        UserRole role,
        Guid? brandId,
        Guid? branchId,
        DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = displayName,
            Role = role,
            BrandId = brandId,
            BranchId = branchId,
            CreatedAtUtc = createdAtUtc,
        };

    /// <summary>Staff — logs in with a 4-digit PIN on a branch-scoped POS tablet.</summary>
    public static User CreateStaff(
        string pinHash,
        string displayName,
        UserRole role,
        Guid brandId,
        Guid branchId,
        DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            PinHash = pinHash,
            DisplayName = displayName,
            Role = role,
            BrandId = brandId,
            BranchId = branchId,
            CreatedAtUtc = createdAtUtc,
        };
}
