namespace Modules.Identity.Features.Auth.StaffRoster;

public sealed record GetStaffRosterQuery(Guid BranchId);

public sealed record StaffRosterMember(Guid UserId, string DisplayName);
