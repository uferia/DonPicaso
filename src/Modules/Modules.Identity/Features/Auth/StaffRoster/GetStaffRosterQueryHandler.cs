using Microsoft.EntityFrameworkCore;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Auth.StaffRoster;

public sealed class GetStaffRosterQueryHandler(IdentityDbContext dbContext)
{
    public async Task<IReadOnlyList<StaffRosterMember>> HandleAsync(
        GetStaffRosterQuery query, CancellationToken cancellationToken = default)
    {
        var branch = await dbContext.Branches.FirstOrDefaultAsync(b => b.Id == query.BranchId, cancellationToken);
        if (branch is null || !branch.IsActive)
        {
            return [];
        }

        var brand = await dbContext.Brands.FirstOrDefaultAsync(b => b.Id == branch.BrandId, cancellationToken);
        if (brand is null || !brand.IsActive)
        {
            return [];
        }

        return await dbContext.Users
            .Where(u => u.BranchId == query.BranchId && u.PinHash != null && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new StaffRosterMember(u.Id, u.DisplayName))
            .ToListAsync(cancellationToken);
    }
}
