using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Users.CreateUser;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Users.UpdateUser;

public sealed class UpdateUserCommandHandler(
    IdentityDbContext dbContext,
    IValidator<UpdateUserCommand> validator,
    IPasswordHasher<User> passwordHasher)
{
    public async Task<UserResult> HandleAsync(
        RequestingUserContext requester, Guid userId, UpdateUserCommand command, CancellationToken cancellationToken = default)
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

        if (!UserProvisioningRules.CanAssign(requester, command.Role, command.BrandId, command.BranchId))
        {
            return UserResult.Failed(UserOperationError.Forbidden);
        }

        var changingToStaff = command.Role == UserRole.Staff;
        var changingCredentialType = changingToStaff != (user.Role == UserRole.Staff);

        if (changingCredentialType && changingToStaff && command.NewPin is null)
        {
            return UserResult.Failed(UserOperationError.InvalidRoleAssignment);
        }

        if (changingCredentialType && !changingToStaff && command.NewPassword is null)
        {
            return UserResult.Failed(UserOperationError.InvalidRoleAssignment);
        }

        if (!changingToStaff && command.Email is null && user.Email is null)
        {
            return UserResult.Failed(UserOperationError.InvalidRoleAssignment);
        }

        var newCredentialHash = changingCredentialType
            ? passwordHasher.HashPassword(user, (changingToStaff ? command.NewPin : command.NewPassword)!)
            : null;

        user.Rename(command.DisplayName);
        user.ChangeRole(command.Role, command.BrandId, command.BranchId, command.Email, newCredentialHash);
        await dbContext.SaveChangesAsync(cancellationToken);

        return UserResult.Success(CreateUserCommandHandler.ToResponse(user));
    }
}
