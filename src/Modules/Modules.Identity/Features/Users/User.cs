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

    public bool IsActive { get; private set; }

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
        DateTimeOffset createdAtUtc)
    {
        if (role == UserRole.Staff)
        {
            throw new ArgumentException("Staff accounts must be created via CreateStaff.", nameof(role));
        }

        return new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = displayName,
            Role = role,
            BrandId = brandId,
            BranchId = branchId,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        };
    }

    /// <summary>Staff — logs in with a 4-digit PIN on a branch-scoped POS tablet. Role is always Staff; there is no room for a caller to pass a mismatched role.</summary>
    public static User CreateStaff(
        string pinHash,
        string displayName,
        Guid brandId,
        Guid branchId,
        DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            PinHash = pinHash,
            DisplayName = displayName,
            Role = UserRole.Staff,
            BrandId = brandId,
            BranchId = branchId,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
        };

    public void Rename(string displayName) => DisplayName = displayName;

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    public void ResetPassword(string newPasswordHash)
    {
        if (Role == UserRole.Staff)
        {
            throw new InvalidOperationException("Staff accounts authenticate with a PIN, not a password.");
        }

        PasswordHash = newPasswordHash;
    }

    public void ResetPin(string newPinHash)
    {
        if (Role != UserRole.Staff)
        {
            throw new InvalidOperationException("Only Staff accounts authenticate with a PIN.");
        }

        PinHash = newPinHash;
    }

    /// <summary>
    /// Reassigns role and brand/branch scope. When the change crosses the
    /// Staff/admin-tier credential-type boundary, <paramref name="newCredentialHash"/>
    /// must carry the freshly-hashed replacement (PIN hash when moving to
    /// Staff, password hash otherwise) — the caller is responsible for
    /// hashing it first, same as CreateAdmin/CreateStaff. When the role
    /// stays within the same credential tier, the existing hash is left
    /// untouched and <paramref name="newCredentialHash"/> is ignored.
    /// </summary>
    public void ChangeRole(UserRole newRole, Guid? brandId, Guid? branchId, string? email, string? newCredentialHash)
    {
        var wasStaff = Role == UserRole.Staff;
        var willBeStaff = newRole == UserRole.Staff;

        if (willBeStaff)
        {
            if (!wasStaff)
            {
                if (newCredentialHash is null)
                {
                    throw new ArgumentException(
                        "A new PIN is required when changing a user's role to Staff.", nameof(newCredentialHash));
                }

                PinHash = newCredentialHash;
                PasswordHash = null;
                Email = null;
            }
        }
        else
        {
            if (wasStaff)
            {
                if (newCredentialHash is null)
                {
                    throw new ArgumentException(
                        "A new password is required when changing a user's role away from Staff.", nameof(newCredentialHash));
                }

                PasswordHash = newCredentialHash;
                PinHash = null;
            }

            if (email is null && Email is null)
            {
                throw new ArgumentException(
                    "An email is required when changing a user's role away from Staff.", nameof(email));
            }

            Email = email ?? Email;
        }

        Role = newRole;
        BrandId = brandId;
        BranchId = branchId;
    }
}
