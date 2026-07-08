namespace Modules.Identity.Features.Users.UpdateUser;

public sealed record UpdateUserCommand(
    string DisplayName,
    UserRole Role,
    Guid? BrandId,
    Guid? BranchId,
    string? Email,
    string? NewPassword,
    string? NewPin);
