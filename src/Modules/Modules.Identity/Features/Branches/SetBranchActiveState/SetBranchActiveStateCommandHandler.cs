using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches.CreateBranch;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Branches.SetBranchActiveState;

public sealed class SetBranchActiveStateCommandHandler(IdentityDbContext dbContext)
{
    public async Task<BranchResult?> HandleAsync(
        Guid brandId, Guid branchId, bool isActive, CancellationToken cancellationToken = default)
    {
        var branch = await dbContext.Branches
            .FirstOrDefaultAsync(b => b.Id == branchId && b.BrandId == brandId, cancellationToken);
        if (branch is null)
        {
            return null;
        }

        if (isActive)
        {
            branch.Reactivate();
        }
        else
        {
            branch.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return CreateBranchCommandHandler.ToResult(branch);
    }
}
