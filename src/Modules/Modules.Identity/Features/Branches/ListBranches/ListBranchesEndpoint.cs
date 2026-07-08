using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Identity.Authorization;
using Modules.Identity.Features.Branches.CreateBranch;

namespace Modules.Identity.Features.Branches.ListBranches;

public static class ListBranchesEndpoint
{
    public static IEndpointRouteBuilder MapListBranches(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/brands/{brandId:guid}/branches", async (
                Guid brandId, ListBranchesQueryHandler handler, CancellationToken cancellationToken) =>
                Results.Ok(await handler.HandleAsync(brandId, cancellationToken)))
            .RequireAuthorization(AuthorizationPolicies.RequireBrandOwnerOrAboveForOwnBrand)
            .WithName("ListBranches")
            .WithTags("Branches")
            .Produces<IReadOnlyList<BranchResult>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden);

        return app;
    }
}
