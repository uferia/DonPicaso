namespace Modules.Identity.Features.Users.CreateUser;

public sealed record CreateUserCommand(
    string? Email,
    string DisplayName,
    UserRole Role,
    Guid? BrandId,
    Guid? BranchId,
    string? Password,
    string? Pin);
