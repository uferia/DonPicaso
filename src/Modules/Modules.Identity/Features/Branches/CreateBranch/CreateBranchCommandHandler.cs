using FluentValidation;
using Modules.Identity.Persistence;

namespace Modules.Identity.Features.Branches.CreateBranch;

public sealed class CreateBranchCommandHandler(
    IdentityDbContext dbContext,
    IValidator<CreateBranchCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<BranchResult> HandleAsync(
        Guid brandId, CreateBranchCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var branch = Branch.Create(brandId, command.Name, timeProvider.GetUtcNow());
        dbContext.Branches.Add(branch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResult(branch);
    }

    internal static BranchResult ToResult(Branch branch) =>
        new(branch.Id, branch.BrandId, branch.Name, branch.IsActive, branch.CreatedAtUtc);
}

public sealed record BranchResult(Guid Id, Guid BrandId, string Name, bool IsActive, DateTimeOffset CreatedAtUtc);
