using System.Security.Claims;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Authorization;

/// <summary>
/// The caller's own role/brand/branch, read from JWT claims — used by Users
/// handlers to enforce tenancy scope manually (see UserProvisioningRules),
/// since the declarative TenancyScope infrastructure can't bridge a
/// BrandOwner's brandId claim to a branch-scoped route. Mirrors the claim
/// parsing already done inline in MeEndpoint.
/// </summary>
public sealed record RequestingUserContext(UserRole Role, Guid? BrandId, Guid? BranchId)
{
    public static RequestingUserContext FromPrincipal(ClaimsPrincipal principal)
    {
        var role = Enum.Parse<UserRole>(principal.FindFirstValue("role")!);
        var brandId = principal.FindFirstValue("brandId") is { } b ? Guid.Parse(b) : (Guid?)null;
        var branchId = principal.FindFirstValue("branchId") is { } br ? Guid.Parse(br) : (Guid?)null;

        return new RequestingUserContext(role, brandId, branchId);
    }
}
