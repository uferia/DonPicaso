using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Authorization;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Users.CreateUser;

public sealed class CreateUserCommandHandler(
    IdentityDbContext dbContext,
    IValidator<CreateUserCommand> validator,
    IPasswordHasher<User> passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<UserResult> HandleAsync(
        RequestingUserContext requester, CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        if (!UserProvisioningRules.CanAssign(requester, command.Role, command.BrandId, command.BranchId))
        {
            return UserResult.Failed(UserOperationError.Forbidden);
        }

        if (command.BrandId is not null && command.BranchId is not null)
        {
            var branchBelongsToBrand = await dbContext.Branches.AnyAsync(
                b => b.Id == command.BranchId && b.BrandId == command.BrandId, cancellationToken);
            if (!branchBelongsToBrand)
            {
                return UserResult.Failed(UserOperationError.Forbidden);
            }
        }

        var now = timeProvider.GetUtcNow();
        var user = command.Role == UserRole.Staff
            ? User.CreateStaff(
                passwordHasher.HashPassword(null!, command.Pin!), command.DisplayName,
                command.BrandId!.Value, command.BranchId!.Value, now)
            : User.CreateAdmin(
                command.Email!, passwordHasher.HashPassword(null!, command.Password!), command.DisplayName,
                command.Role, command.BrandId, command.BranchId, now);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return UserResult.Success(ToResponse(user));
    }

    internal static UserResponse ToResponse(User user) => new(
        user.Id, user.Email, user.DisplayName, user.Role, user.BrandId, user.BranchId, user.IsActive, user.CreatedAtUtc);
}

public enum UserOperationError
{
    None,
    NotFound,
    Forbidden,
    InvalidRoleAssignment,
}

public sealed record UserResponse(
    Guid Id,
    string? Email,
    string DisplayName,
    UserRole Role,
    Guid? BrandId,
    Guid? BranchId,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

public sealed record UserResult(UserOperationError Error, UserResponse? User)
{
    public static UserResult Success(UserResponse user) => new(UserOperationError.None, user);

    public static UserResult Failed(UserOperationError error) => new(error, null);
}
