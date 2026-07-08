using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Identity.Features.Branches.CreateBranch;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Branches.UpdateBranch;

public sealed class UpdateBranchCommandHandler(
    IdentityDbContext dbContext,
    IValidator<UpdateBranchCommand> validator)
{
    public async Task<BranchResult?> HandleAsync(
        Guid brandId, Guid branchId, UpdateBranchCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var branch = await dbContext.Branches
            .FirstOrDefaultAsync(b => b.Id == branchId && b.BrandId == brandId, cancellationToken);
        if (branch is null)
        {
            return null;
        }

        branch.Rename(command.Name);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateBranchCommandHandler.ToResult(branch);
    }
}
