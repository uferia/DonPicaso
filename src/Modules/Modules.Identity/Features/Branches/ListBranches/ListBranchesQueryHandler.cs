using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches.CreateBranch;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Branches.ListBranches;

public sealed class ListBranchesQueryHandler(IdentityDbContext dbContext)
{
    public async Task<IReadOnlyList<BranchResult>> HandleAsync(Guid brandId, CancellationToken cancellationToken = default) =>
        await dbContext.Branches
            .Where(b => b.BrandId == brandId)
            .OrderBy(b => b.Name)
            .Select(b => new BranchResult(b.Id, b.BrandId, b.Name, b.IsActive, b.CreatedAtUtc))
            .ToListAsync(cancellationToken);
}
