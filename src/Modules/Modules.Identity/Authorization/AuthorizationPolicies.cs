using Microsoft.AspNetCore.Authorization;
using Modules.Identity.Features.Users;

namespace Modules.Identity.Authorization;

public static class AuthorizationPolicies
{
    public const string RequireStaffOrAbove = nameof(RequireStaffOrAbove);
    public const string RequireBranchManagerOrAbove = nameof(RequireBranchManagerOrAbove);
    public const string RequireBrandOwnerOrAbove = nameof(RequireBrandOwnerOrAbove);
    public const string RequireCorporate = nameof(RequireCorporate);

    public static AuthorizationOptions AddIdentityPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireStaffOrAbove, p => p.Requirements.Add(new RoleRequirement(UserRole.Staff)));
        options.AddPolicy(RequireBranchManagerOrAbove, p => p.Requirements.Add(new RoleRequirement(UserRole.BranchManager)));
        options.AddPolicy(RequireBrandOwnerOrAbove, p => p.Requirements.Add(new RoleRequirement(UserRole.BrandOwner)));
        options.AddPolicy(RequireCorporate, p => p.Requirements.Add(new RoleRequirement(UserRole.Corporate)));
        return options;
    }
}
