using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Users;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth;

/// <summary>
/// A user is "effectively active" only if they themselves are active, AND
/// (when assigned to a branch/brand) that branch/brand is also active.
/// Shared by Login, StaffLogin, and Refresh so deactivating a user, their
/// branch, or their brand consistently blocks both new logins and refreshing
/// an already-issued session.
/// </summary>
internal static class EffectiveActiveCheck
{
    public static async Task<bool> IsEffectivelyActiveAsync(
        IdentityDbContext dbContext, User user, CancellationToken cancellationToken)
    {
        if (!user.IsActive)
        {
            return false;
        }

        if (user.BranchId is { } branchId)
        {
            var branch = await dbContext.Branches.FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
            if (branch is null || !branch.IsActive)
            {
                return false;
            }
        }

        if (user.BrandId is { } brandId)
        {
            var brand = await dbContext.Brands.FirstOrDefaultAsync(b => b.Id == brandId, cancellationToken);
            if (brand is null || !brand.IsActive)
            {
                return false;
            }
        }

        return true;
    }
}
