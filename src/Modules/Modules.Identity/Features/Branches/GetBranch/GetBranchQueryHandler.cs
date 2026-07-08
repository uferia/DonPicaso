using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches.CreateBranch;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Branches.GetBranch;

public sealed class GetBranchQueryHandler(IdentityDbContext dbContext)
{
    public async Task<BranchResult?> HandleAsync(Guid brandId, Guid branchId, CancellationToken cancellationToken = default)
    {
        var branch = await dbContext.Branches
            .FirstOrDefaultAsync(b => b.Id == branchId && b.BrandId == brandId, cancellationToken);

        return branch is null ? null : CreateBranchCommandHandler.ToResult(branch);
    }
}
