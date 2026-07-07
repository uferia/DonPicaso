namespace Modules.Identity.Features.Users;

/// <summary>
/// Ordered highest-to-lowest: the authorization handler (Task 4) compares
/// these numeric ranks directly, so declaration order is load-bearing.
/// </summary>
public enum UserRole
{
    Corporate,
    BrandOwner,
    BranchManager,
    Staff,
}
