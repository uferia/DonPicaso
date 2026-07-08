using Modules.Identity.Authorization;

namespace Modules.Identity.Features.Users;

/// <summary>
/// "Each tier creates/edits the tier(s) below it, within its own scope" —
/// reused for both creating a new user (against the proposed role/scope)
/// and editing an existing one (checked once against the user's current
/// role/scope, and again against the proposed new role/scope).
/// </summary>
public static class UserProvisioningRules
{
    public static bool CanAssign(
        RequestingUserContext requester, UserRole targetRole, Guid? targetBrandId, Guid? targetBranchId) =>
        requester.Role switch
        {
            UserRole.Corporate => true,
            UserRole.BrandOwner => targetRole is UserRole.BranchManager or UserRole.Staff
                && targetBrandId is not null && targetBrandId == requester.BrandId,
            UserRole.BranchManager => targetRole == UserRole.Staff
                && targetBranchId is not null && targetBranchId == requester.BranchId,
            _ => false,
        };
}
