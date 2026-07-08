using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Users.ResetCredential;

public sealed class ResetCredentialCommandHandler(
    IdentityDbContext dbContext,
    IValidator<ResetCredentialCommand> validator,
    IPasswordHasher<User> passwordHasher)
{
    public async Task<UserResult> HandleAsync(
        RequestingUserContext requester, Guid userId, ResetCredentialCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return UserResult.Failed(UserOperationError.NotFound);
        }

        if (!UserProvisioningRules.CanAssign(requester, user.Role, user.BrandId, user.BranchId))
        {
            return UserResult.Failed(UserOperationError.Forbidden);
        }

        if (user.Role == UserRole.Staff)
        {
            if (command.NewPin is null)
            {
                return UserResult.Failed(UserOperationError.InvalidRoleAssignment);
            }
            user.ResetPin(passwordHasher.HashPassword(user, command.NewPin));
        }
        else
        {
            if (command.NewPassword is null)
            {
                return UserResult.Failed(UserOperationError.InvalidRoleAssignment);
            }
            user.ResetPassword(passwordHasher.HashPassword(user, command.NewPassword));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return UserResult.Success(CreateUserCommandHandler.ToResponse(user));
    }
}
