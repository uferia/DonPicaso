namespace Modules.Identity.Features.Auth.StaffLogin;

public sealed record StaffLoginCommand(Guid BranchId, Guid UserId, string Pin);
