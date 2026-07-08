using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Branches;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Users.ListUsers;

public sealed class ListUsersQueryHandler(IdentityDbContext dbContext)
{
    public async Task<UserListResult> HandleAsync(
        RequestingUserContext requester, Guid? brandIdFilter, Guid? branchIdFilter, CancellationToken cancellationToken = default)
    {
        IQueryable<User> query = dbContext.Users;

        switch (requester.Role)
        {
            case UserRole.Corporate:
                if (brandIdFilter is { } brand)
                {
                    query = query.Where(u => u.BrandId == brand);
                }
                if (branchIdFilter is { } branch)
                {
                    query = query.Where(u => u.BranchId == branch);
                }
                break;

            case UserRole.BrandOwner:
                query = query.Where(u => u.BrandId == requester.BrandId);
                if (branchIdFilter is { } requestedBranchId)
                {
                    var branchBelongsToBrand = await dbContext.Branches.AnyAsync(
                        b => b.Id == requestedBranchId && b.BrandId == requester.BrandId, cancellationToken);
                    if (!branchBelongsToBrand)
                    {
                        return UserListResult.Forbidden();
                    }
                    query = query.Where(u => u.BranchId == requestedBranchId);
                }
                break;

            case UserRole.BranchManager:
                query = query.Where(u => u.BranchId == requester.BranchId);
                break;

            default:
                return UserListResult.Forbidden();
        }

        var users = await query
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserResponse(u.Id, u.Email, u.DisplayName, u.Role, u.BrandId, u.BranchId, u.IsActive, u.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return UserListResult.Success(users);
    }
}

public sealed record UserListResult(bool IsForbidden, IReadOnlyList<UserResponse> Users)
{
    public static UserListResult Forbidden() => new(true, []);

    public static UserListResult Success(IReadOnlyList<UserResponse> users) => new(false, users);
}
