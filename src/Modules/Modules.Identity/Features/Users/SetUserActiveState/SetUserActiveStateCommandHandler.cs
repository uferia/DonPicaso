using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Users.SetUserActiveState;

public sealed class SetUserActiveStateCommandHandler(IdentityDbContext dbContext)
{
    public async Task<UserResult> HandleAsync(
        RequestingUserContext requester, Guid userId, bool isActive, CancellationToken cancellationToken = default)
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

        if (isActive)
        {
            user.Reactivate();
        }
        else
        {
            user.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return UserResult.Success(CreateUserCommandHandler.ToResponse(user));
    }
}
