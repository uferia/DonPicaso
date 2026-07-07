using Microsoft.EntityFrameworkCore;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.StaffRoster;

public sealed class GetStaffRosterQueryHandler(IdentityDbContext dbContext)
{
    public async Task<IReadOnlyList<StaffRosterMember>> HandleAsync(
        GetStaffRosterQuery query, CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .Where(u => u.BranchId == query.BranchId && u.PinHash != null)
            .OrderBy(u => u.DisplayName)
            .Select(u => new StaffRosterMember(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);
}
