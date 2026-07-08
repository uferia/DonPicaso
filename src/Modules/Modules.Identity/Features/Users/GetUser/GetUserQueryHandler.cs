using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Users.GetUser;

public sealed class GetUserQueryHandler(IdentityDbContext dbContext)
{
    public async Task<UserResult> HandleAsync(
        RequestingUserContext requester, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return UserResult.Failed(UserOperationError.NotFound);
        }

        if (!UserProvisioningRules.CanAssign(requester, user.Role, user.BrandId, user.BranchId))
        {
            return UserResult.Failed(UserOperationError.Forbidden);
        }

        return UserResult.Success(CreateUserCommandHandler.ToResponse(user));
    }
}
